namespace Model
{
    public class Tileset
    {
        public Asset asset;
        public double geometricError;
        public Tile root;

        public Tileset()
        {
            asset = new Asset
            {
                version = "1.0"
            };
            geometricError = 0;
        }
    }
}