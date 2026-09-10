using System;
using UnityEngine;

// Session-level command face of the world (Part B §3.1): the enter-world
// sequence talks to this seam instead of touching slot / dimension / ring work
// directly. V1 ships one in-process implementation (InlineWorldServer); a
// later server (dedicated process / network) replaces the Instance without
// touching the session layer. Main thread throughout: RequestEnterWorld and
// Poll are called from the main thread, WorldEntryReady fires inside Poll.
public abstract class WorldServer
{
    private static WorldServer instance = null;

    // Lazy singleton seam; the first access builds the V1 implementation.
    public static WorldServer Instance => instance ??= new InlineWorldServer();

    // Main thread: starts the enter-world pipeline. The result (success or
    // failure) arrives asynchronously via WorldEntryReady.
    public abstract void RequestEnterWorld(WorldEntryRequest request);

    // Main thread pump (GameLoopDriver, once per render frame): delivers a
    // finished worker result as WorldEntryReady.
    public abstract void Poll();

    public event Action<WorldEntryResult> WorldEntryReady;
    protected void RaiseWorldEntryReady(WorldEntryResult result) => WorldEntryReady?.Invoke(result);
}

// One enter-world request. SlotFolder null/empty = direct-play debug path: the
// server picks (or creates) the default slot itself.
public class WorldEntryRequest
{
    public string SlotFolder;
    // Ready-gate radius in chunks around the landing chunk (1 = the 3x3
    // landing square). Forwarded to the session's ChunkLoadTracker.
    public float LandingRegionRadius;
}

// Everything the session needs to finish the entry, produced off the main
// thread where possible. The field set follows the doc's §3.2 draft; the
// slot's display fields (name / createdTime) fold into Meta instead of being
// duplicated here.
public class WorldEntryResult
{
    public bool Success;
    public string FailureStage;        // set when Success == false ("w2: ..." etc.)
    public string Folder;              // resolved slot folder (always set)
    public WorldSaveData Meta;         // null on a fresh/v1 slot: defaults apply
    public int Seed;                   // meta seed, or WorldManager.DefaultSeed
    public PlayerSaveData PlayerData;  // null when the slot has no player save
    public Vector3 WorldSpawn;         // decision B: the world's own spawn
    public Vector3 SpawnPosition;      // landing position (player save or world spawn)
    public string StartDimensionFullName;
    public ushort StartDimensionId;
    public Dimension BuiltDimension;   // w4 product; the session registers it
    public Vector2Int[] PendingChunkCoords;
    public bool[] PendingChunkIsLoad;
    public float LandingRegionRadius;
}
