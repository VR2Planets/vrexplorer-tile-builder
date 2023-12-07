/// <summary>
/// Hold the additional information associated with a tile and that might be required for the export.
/// </summary>
public class TileMetadata
{
    public float geometricErrorPixelSize = 1;
    public float geometricErrorTextureDownscale = 1;
    public float geometricErrorTileDepth = 1;
    public float geometricErrorHausdorff = 1;
    public float geometricErrorThreshold = 1;
    public int triangleCounts = 0;
    public TileBoundingBox boundingBox = new TileBoundingBox();
}