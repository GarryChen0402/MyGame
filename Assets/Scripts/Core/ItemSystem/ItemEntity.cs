using UnityEngine;

// Item drop entity: pure C# data + per-frame physics/merge/lifetime logic.
// Data ownership lives in the owning chunk (Chunk.ItemEntities, see design doc
// Docs/掉落物ItemEntity实现方案.md §3.1); ItemEntityManager keeps a live
// reference set only and drives OnUpdate once per rendered frame.
public class ItemEntity : Entity
{
    public ItemStack Stack;          // carried whole stack (one stack = one drop)
    public float LifeTime;           // seconds alive: despawn timer + bob phase
    public float PickupDelay;        // seconds before pickup allowed (break 0.5 / toss 2.0)
    public float DespawnTime = 300f; // 5 minutes (vanilla 6000 ticks)
    public float MergeInterval = 1.25f;  // merge scan period (vanilla 25 ticks)
    public float MergeDistance = 0.75f;  // vanilla merge range (meters)
    public Chunk OwnerChunk;         // set by Chunk.RegisterItemEntity; null once removed

    private float mergeTimer;
    private bool wasOnGround;        // landing-frame detection for ground friction
    private const float VoidY = -64f;    // below the generated floor (default MinSubChunkIndex -4 * 16)

    public ItemEntity()
    {
        AABBs.Add(new AABB()
        {
            MinRange = new Vector3(-0.125f, -0.125f, -0.125f),
            MaxRange = new Vector3( 0.125f,  0.125f,  0.125f),
        });
    }

    // Translates the collision box so the feet-center pivot (Entity.Position)
    // lands on position; called by the manager right after construction.
    public void SetPosition(Vector3 position)
    {
        Vector3 delta = position - Position;
        for(int i = 0; i < AABBs.Count; i++)
            AABBs[i] = AABBs[i].ApplyMotion(delta);
    }

    // Per-frame update, deltaTime in seconds (design doc §5). All quantities
    // are seconds-based, so the integration is frame-rate independent.
    // Physics runs inside base.OnUpdate -> TickPhysics (overridden below).
    // The Entity ctor registers every drop into EntityManager, so drops tick
    // exactly once per frame here (single-path tick; ItemEntityManager no
    // longer calls OnUpdate - v1 double-ticked drops from both managers).
    public override void OnUpdate(float dt)
    {
        if(PickupDelay > 0f)PickupDelay = Mathf.Max(0f, PickupDelay - dt);
        base.OnUpdate(dt);
        TickMerge(dt);
        TryPickup();

        LifeTime += dt;
        if(LifeTime >= DespawnTime || Position.y < VoidY)
        {
            ItemEntityManager.Instance.DespawnItemEntity(this);
            return;
        }
    }

    // Pickup (design doc §5 step 5): the pickup box is the player box extended
    // 1.0 horizontally (boundaries inclusive) and 0.5 up/down (boundaries
    // exclusive). Whole-stack transfer on success (TryAddItemStack empties the
    // stack), hover and retry when the inventory has no room (vanilla).
    private void TryPickup()
    {
        Player player = Player.Instance;
        if(player == null || PickupDelay > 0f)return;
        AABB item = MainBox;
        AABB p = player.MainBox;
        if(item.MaxRange.x < p.MinRange.x - 1.0f || item.MinRange.x > p.MaxRange.x + 1.0f)return;
        if(item.MaxRange.z < p.MinRange.z - 1.0f || item.MinRange.z > p.MaxRange.z + 1.0f)return;
        if(item.MaxRange.y <= p.MinRange.y - 0.5f || item.MinRange.y >= p.MaxRange.y + 0.5f)return;
        if(!player.inventory.TryAddItemStack(Stack))return;   // no room: hover
        ItemEntityManager.Instance.DespawnItemEntity(this);
    }

    // Drop physics: air drag and landing friction rules differ from
    // player/mob (drag 0.667/s = vanilla 0.98 per tick; hard 0.3 cut on the
    // touchdown frame then 0.05/s decay while resting), so the whole set is
    // overridden instead of calling base.TickPhysics.
    protected override void TickPhysics(float dt)
    {
        // Air drag applies to the pre-gravity velocity, then gravity adds in
        // (vanilla order): drag 0.667/s = vanilla 0.98 per tick (0.98^20).
        Motion *= Mathf.Pow(0.667f, dt);
        Motion.y -= Gravity * dt;   // gravity 16 m/s^2 (vanilla 0.04 /tick^2)

        bool onGround = MoveAndSettle(dt);
        OnGround = onGround;
        if(onGround)
        {
            // Landing friction (design doc §5 note, hand-tuned): one hard cut
            // on the touchdown frame, then a slow decay while resting.
            if(wasOnGround)
            {
                Motion.x *= Mathf.Pow(0.05f, dt);
                Motion.z *= Mathf.Pow(0.05f, dt);
            }
            else
            {
                Motion.x *= 0.3f;
                Motion.z *= 0.3f;
            }
        }
        wasOnGround = onGround;
    }

    // Sweeps the displacement (PhysicsManager axis-separated) and zeroes the
    // vertical speed on ground contact so resting drops stop falling.
    private bool MoveAndSettle(float dt)
    {
        MoveResult result = PhysicsManager.Instance.MoveEntity(this, Motion * dt);
        if(!result.OnGround)return false;
        Motion.y = 0f;
        return true;
    }

    // Merge scan: every MergeInterval seconds, fold any same-item stack within
    // MergeDistance into this one (partial fold when capacity is smaller) and
    // keep the younger lifetime (vanilla keeps the smaller Age). v1 looks only
    // inside the owning chunk - two drops 0.75m apart across a chunk border
    // stay separate (accepted simplification; vanilla scans the neighborhood).
    private void TickMerge(float dt)
    {
        mergeTimer += dt;
        if(mergeTimer < MergeInterval)return;
        mergeTimer = 0f;

        Chunk chunk = OwnerChunk;
        if(chunk == null)return;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(Stack.itemId, out var def))return;
        int capacity = def.MaxStack;
        if(Stack.amount >= capacity)return;

        // ToArray: folding despawns others, which mutates the chunk's list.
        foreach(var other in chunk.ItemEntities.ToArray())
        {
            if(other == this)continue;
            if(other.Stack.amount <= 0 || !other.Stack.Equals(Stack))continue;
            if((other.Position - Position).sqrMagnitude > MergeDistance * MergeDistance)continue;
            int take = Mathf.Min(capacity - Stack.amount, other.Stack.amount);
            Stack.amount += take;
            other.Stack.amount -= take;
            LifeTime = Mathf.Min(LifeTime, other.LifeTime);
            if(other.Stack.amount == 0)
                ItemEntityManager.Instance.DespawnItemEntity(other);
            if(Stack.amount >= capacity)break;
        }
    }
}
