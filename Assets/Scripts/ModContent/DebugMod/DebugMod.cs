using UnityEngine;
public class DebugMod : IMod
{
    public string ModId => "DebugMod";

    public int LoadPriority => 0;

    public void RegisterAllResources()
    {
        // throw new System.NotImplementedException();
        EventBus.Instance.Subscribe<BlockChangedEvent>(DebugFunc1_summonEntity);
    }

    private void DebugFunc1_summonEntity(BlockChangedEvent evt)
    {
        EntityManager.SummonMobEntity(Player.Instance.Position + new Vector3(4, 0, 0), "minecraft:zombie");
    }
}