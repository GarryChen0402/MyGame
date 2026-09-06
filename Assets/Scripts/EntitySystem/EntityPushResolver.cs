using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// Entity-entity horizontal push-apart, one unified pass per frame once every
// entity finished its own move + block collision (design doc 生物实体碰撞-代码
// 设计.md). Participants are flattened into read-only float arrays before the
// pass so worker tasks never touch entity objects (Unity main-thread rule).
// Neighbors come from a per-frame space hash: entities bucket by horizontal
// center - one bucket per entity, so candidates never repeat - and the query
// expands the covered cells by one ring. Correctness holds while CellSize
// covers the widest entity box; oversized boxes brute-force instead. Each
// overlapping pair yields a half-overlap intent per side; intents are computed
// in chunks (one pool task per chunk) and an entity is written only by its own
// task, so no locks are needed. The main thread then walks the intents through
// PhysicsManager.MoveEntity, whose block sweep limits every move: nothing is
// ever pushed into a block.
public static class EntityPushResolver
{
    private const int MinParallelEntities = 32;   // below this: inline pass (pool overhead > gain)
    private const int ChunkSize = 64;             // entities per pool task
    private const float CellSize = 8f;            // hash cell (power of two: exact cell-edge division)
    private const float HalfOverlap = 0.5f;       // each side yields half the overlap (symmetric)

    private static readonly List<LivingEntity> participants = new();
    private static float[] boxes;                 // 6 floats each: minX, minY, minZ, maxX, maxY, maxZ
    private static float[] intents;               // 2 floats each: horizontal intent (dx, dz)

    private static readonly Dictionary<long, List<int>> hash = new();
    private static readonly Stack<List<int>> bucketPool = new();
    private static readonly List<long> usedKeys = new();
    private static readonly List<int> oversized = new();   // boxes wider than CellSize: brute-force candidates
    private static volatile Exception workerError;

    public static void RunPass(ICollection<Entity> all)
    {
        participants.Clear();
        foreach(var e in all)
            if(e is LivingEntity le && !le.IsDead)
                participants.Add(le);             // corpses and items sit out (rules R2/R6)

        int n = participants.Count;
        if(n == 0)return;

        EnsureCapacity(n);
        FlattenBoxes(n);
        Array.Clear(intents, 0, n * 2);
        BuildHash(n);

        try
        {
            if(n <= MinParallelEntities)ComputeChunk(0, n);
            else RunParallel(n);
        }
        catch(Exception e)
        {
            Debug.LogError("[EntityPushResolver] pass aborted: " + e);
            ClearHash();
            return;                               // intents unapplied: positions stay put this frame
        }
        if(workerError != null)
        {
            Debug.LogError("[EntityPushResolver] worker failed: " + workerError);
            workerError = null;
            ClearHash();
            return;
        }

        ApplyIntents(n);
        ClearHash();
    }

    private static void EnsureCapacity(int n)
    {
        int need = n * 6;
        if(boxes == null || boxes.Length < need)boxes = new float[need];
        need = n * 2;
        if(intents == null || intents.Length < need)intents = new float[need];
    }

    private static void FlattenBoxes(int n)
    {
        for(int i = 0; i < n; i++)
        {
            var e = participants[i];
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach(var b in e.AABBs)
            {
                if(b.MinRange.x < minX)minX = b.MinRange.x;
                if(b.MinRange.y < minY)minY = b.MinRange.y;
                if(b.MinRange.z < minZ)minZ = b.MinRange.z;
                if(b.MaxRange.x > maxX)maxX = b.MaxRange.x;
                if(b.MaxRange.y > maxY)maxY = b.MaxRange.y;
                if(b.MaxRange.z > maxZ)maxZ = b.MaxRange.z;
            }
            int o = i * 6;
            boxes[o] = minX; boxes[o + 1] = minY; boxes[o + 2] = minZ;
            boxes[o + 3] = maxX; boxes[o + 4] = maxY; boxes[o + 5] = maxZ;
        }
    }

    private static void BuildHash(int n)
    {
        oversized.Clear();
        for(int i = 0; i < n; i++)
        {
            int o = i * 6;
            if(boxes[o + 3] - boxes[o] > CellSize || boxes[o + 5] - boxes[o + 2] > CellSize)
            {
                oversized.Add(i);                 // ring query would miss its neighbors
                continue;
            }
            long key = Key(FloorToCell((boxes[o] + boxes[o + 3]) * 0.5f),
                           FloorToCell((boxes[o + 2] + boxes[o + 5]) * 0.5f));
            if(!hash.TryGetValue(key, out var bucket))
            {
                bucket = bucketPool.Count > 0 ? bucketPool.Pop() : new List<int>(4);
                hash.Add(key, bucket);
                usedKeys.Add(key);
            }
            bucket.Add(i);
        }
    }

    private static void RunParallel(int n)
    {
        int taskCount = (n + ChunkSize - 1) / ChunkSize;
        workerError = null;
        using var done = new CountdownEvent(taskCount);
        for(int t = 0; t < taskCount; t++)
        {
            int start = t * ChunkSize;
            int end = Math.Min(start + ChunkSize, n);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { ComputeChunk(start, end); }
                catch(Exception e) { workerError = e; }
                finally { done.Signal(); }
            });
        }
        done.Wait();
    }

    private static void ComputeChunk(int start, int end)
    {
        for(int i = start; i < end; i++)
        {
            int io = i * 6;
            float minX = boxes[io], minY = boxes[io + 1], minZ = boxes[io + 2];
            float maxX = boxes[io + 3], maxY = boxes[io + 4], maxZ = boxes[io + 5];

            // +-1 cell ring: a neighbor's center always falls inside once
            // CellSize covers the widest entity box.
            int cx0 = FloorToCell(minX) - 1, cx1 = FloorToCell(maxX) + 1;
            int cz0 = FloorToCell(minZ) - 1, cz1 = FloorToCell(maxZ) + 1;
            for(int cx = cx0; cx <= cx1; cx++)
            for(int cz = cz0; cz <= cz1; cz++)
            {
                if(!hash.TryGetValue(Key(cx, cz), out var bucket))continue;
                foreach(int j in bucket)Accumulate(i, io, minX, minY, minZ, maxX, maxY, maxZ, j);
            }
            foreach(int j in oversized)Accumulate(i, io, minX, minY, minZ, maxX, maxY, maxZ, j);
        }
    }

    // One directed pair: records "i must move half the overlap away from j".
    // The symmetric half for j is produced by j's own task, so no write ever
    // races (an entity is touched only inside its own chunk).
    private static void Accumulate(int i, int io, float minX, float minY, float minZ,
        float maxX, float maxY, float maxZ, int j)
    {
        if(j == i)return;
        int jo = j * 6;
        float jMinX = boxes[jo], jMinY = boxes[jo + 1], jMinZ = boxes[jo + 2];
        float jMaxX = boxes[jo + 3], jMaxY = boxes[jo + 4], jMaxZ = boxes[jo + 5];

        if(minY >= jMaxY || maxY <= jMinY)return;             // vertical overlap must be strict (rule R4)
        float ox = Mathf.Min(maxX, jMaxX) - Mathf.Max(minX, jMinX);
        if(ox <= 0f)return;
        float oz = Mathf.Min(maxZ, jMaxZ) - Mathf.Max(minZ, jMinZ);
        if(oz <= 0f)return;

        // Separate along the dominant axis of the horizontal center difference
        // (vanilla Mth.absMax alike). A zero difference falls back to a
        // deterministic index split so both sides still move apart; the sign
        // rule is anti-symmetric, keeping the j-side intent opposite.
        float dx = (minX + maxX) - (jMinX + jMaxX);           // *0.5 omitted: sign only
        float dz = (minZ + maxZ) - (jMinZ + jMaxZ);
        int ii = i * 2;
        if(Mathf.Abs(dx) >= Mathf.Abs(dz))
        {
            float s = dx != 0f ? Mathf.Sign(dx) : (i < j ? 1f : -1f);
            intents[ii] += s * ox * HalfOverlap;
        }
        else
        {
            float s = dz != 0f ? Mathf.Sign(dz) : (i < j ? 1f : -1f);
            intents[ii + 1] += s * oz * HalfOverlap;
        }
    }

    private static void ApplyIntents(int n)
    {
        for(int i = 0; i < n; i++)
        {
            int o = i * 2;
            float dx = intents[o], dz = intents[o + 1];
            if(dx == 0f && dz == 0f)continue;
            PhysicsManager.Instance.MoveEntity(participants[i], new Vector3(dx, 0f, dz));
        }
    }

    private static void ClearHash()
    {
        foreach(var k in usedKeys)
        {
            if(hash.TryGetValue(k, out var bucket))
            {
                bucket.Clear();
                bucketPool.Push(bucket);
            }
            hash.Remove(k);
        }
        usedKeys.Clear();
    }

    private static int FloorToCell(float v) => Mathf.FloorToInt(v / CellSize);

    // Cell key: x in the upper 32 bits, z in the lower (works for negatives too).
    private static long Key(int cx, int cz) => ((long)cx << 32) | (uint)cz;
}
