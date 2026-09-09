
public class DimensionDefinition : ResourceType
{
    public int MinSubChunkIndex;
    public int MaxSubChunkIndex;

    public string DimensionGeneratorName;

    // Content-declared "start dimension" marker: the enter-world sequence scans
    // the registry for it (never hardcodes a mod string) and pins fresh worlds
    // to it explicitly (design §A.4.7).
    public bool IsStartDimension;
}