public interface IMod
{
    public void RegisterAllResources();
    public string ModId {get;}
    public int LoadPriority{get;}
}