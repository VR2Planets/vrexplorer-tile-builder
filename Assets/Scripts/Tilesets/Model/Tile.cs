using System.Collections.Generic;

namespace Model
{
    public class Tile
    {
        public Box boundingVolume;
        public double geometricError;
        public double geometricErrorPixelSize = 1;
        public double geometricErrorTextureDownscale = 1;
        public double geometricErrorTileDepth = 1;
        public double geometricErrorHausdorff = 1;
        public double geometricErrorThreshold = 1;
        public Content content;
        public List<Tile> children = new List<Tile>();
        public string refine;
        public double[] transform = null;

        public Tile()
        {
        }

        public Tile(Tile t)
        {
            boundingVolume = t.boundingVolume;
            geometricError = t.geometricError;
            geometricErrorPixelSize = t.geometricErrorPixelSize;
            geometricErrorTextureDownscale = t.geometricErrorTextureDownscale;
            geometricErrorTileDepth = t.geometricErrorTileDepth;
            geometricErrorHausdorff = t.geometricErrorHausdorff;
            geometricErrorThreshold = t.geometricErrorThreshold;
            content = t.content;
            refine = t.refine;
            transform = t.transform;
        }
    }
}