using UnityEngine;

// Scripted validation of the furnace block entity: places a real furnace block
// through the world API (event -> manager creates the BE), inserts cobblestone
// + coal through the module API, ticks the smelting loop and verifies the
// stone output. Also round-trips BE serialization and checks access policies.
// Attach to any GameObject in the scene (like StoneTester).
public class FurnaceTester : MonoBehaviour
{
    private int passed = 0, failed = 0;

    private void Start()
    {
        Run();
    }

    private void Run()
    {
        if(!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId("minecraft:furnace", out ushort furnaceBlockId)) { Fail("furnace 方块未注册"); return; }
        ushort furnaceState = ResourceSystem.Instance.GetDefaultState(furnaceBlockId);
        ushort cobblestoneItem = ResolveItem("minecraft:cobblestone");
        ushort coalItem = ResolveItem("minecraft:coal");
        ushort stoneItem = ResolveItem("minecraft:stone");

        // 1. Place the furnace through the normal world API (spawns its BE).
        Vector3Int pos = FindAirBlock();
        bool placed = WorldManager.Instance.TryPlaceBlock(Player.Instance.DimensionId, pos, furnaceState, true);
        Check(placed, "放置熔炉方块");
        var be = BlockEntityManager.Instance.GetBlockEntity(Player.Instance.DimensionId, pos);
        Check(be != null, "熔炉 BE 由事件创建");
        if(be == null) return;

        var input = be.GetModule<InventoryModule>("input");
        var fuel = be.GetModule<InventoryModule>("fuel");
        var output = be.GetModule<InventoryModule>("output");
        var process = be.GetModule<ProcessingModule>("processing");
        Check(input != null && fuel != null && output != null && process != null, "四个模块挂载成功");
        if(input == null || fuel == null || output == null || process == null) return;

        // 2. Access policy checks.
        bool fuelRejected = !fuel.TryInsert(new ItemStack { itemId = stoneItem, amount = 1 }, InventoryAccess.Player);
        Check(fuelRejected, "燃料槽拒绝非燃料物品(stone)");
        bool outputRejected = !output.TryInsert(new ItemStack { itemId = coalItem, amount = 1 }, InventoryAccess.Player);
        Check(outputRejected, "输出槽拒绝玩家存入(coal)");

        // 3. Insert cobblestone + coal, then run ~2.2s of ticks (recipe time = 1s).
        bool inputOk = input.TryInsert(new ItemStack { itemId = cobblestoneItem, amount = 1 }, InventoryAccess.Player);
        bool fuelOk = fuel.TryInsert(new ItemStack { itemId = coalItem, amount = 1 }, InventoryAccess.Player);
        Check(inputOk && fuelOk, "输入原料与燃料插入成功");
        for(int i = 0; i < 140; i++) be.Tick(1f / 60f);

        var result = output.Inventory.GetItemStackAt(0);
        Check(result != null && result.itemId == stoneItem && result.amount == 1, "熔炼产出 1 个 stone");
        var inputLeft = input.Inventory.GetItemStackAt(0);
        Check(inputLeft == null || inputLeft.IsEmpty(), "输入原料已消耗");
        var fuelLeft = fuel.Inventory.GetItemStackAt(0);
        Check(fuelLeft == null || fuelLeft.IsEmpty(), "燃料已消耗");
        Check(Mathf.Approximately(process.Progress, 0f), "进度已重置");

        // 4. Serialization round-trip: snapshot -> fresh BE -> restore.
        var snapshot = be.BuildModuleSaveData();
        Check(snapshot != null && snapshot.Count == 4, "BE 序列化快照生成(4 个模块)");
        var be2 = be.Definition.CreateNewBlockEntity(be.Position, be.StateId);
        be2.DeserializeFromSave(snapshot);
        var out2 = be2.GetModule<InventoryModule>("output").Inventory.GetItemStackAt(0);
        Check(out2 != null && out2.itemId == stoneItem && out2.amount == 1, "反序列化恢复输出物品");

        // 5. Break the furnace: BE must be removed via the event path.
        bool broken = WorldManager.Instance.TryBreakBlockAt(Player.Instance.DimensionId, pos, true);
        Check(broken, "破坏熔炉方块");
        Check(BlockEntityManager.Instance.GetBlockEntity(Player.Instance.DimensionId, pos) == null, "熔炉 BE 随破坏移除");

        Debug.Log($"==== FurnaceTester: {passed} passed, {failed} failed ====");
    }

    // Finds a guaranteed-air position near the player (fallback: walk up).
    private Vector3Int FindAirBlock()
    {
        var dim = Player.Instance.DimensionId;
        Vector3Int start = Dimension.WorldPosToDimensionCoord(Player.Instance.Position);
        for(int y = 0; y < 8; y++)
        {
            Vector3Int pos = start + new Vector3Int(0, y + 1, 0);
            if(WorldManager.Instance.TryGetDimension(dim, out var dimension) && dimension.GetBlockAt(pos) == 0)
                return pos;
        }
        return start + Vector3Int.up;
    }

    private ushort ResolveItem(string fullName)
    {
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(fullName, out ushort id)) Fail($"物品未注册: {fullName}");
        return id;
    }

    private void Check(bool condition, string what)
    {
        if(condition) { passed++; Debug.Log($"[PASS] {what}"); }
        else { failed++; Debug.LogError($"[FAIL] {what}"); }
    }

    private void Fail(string what) => failed++;
}
