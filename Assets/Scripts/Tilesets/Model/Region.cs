namespace Model
{
    public class Region : BoundingVolume
    {
        public double[] values;

        public Region(double[] dbls)
        {
            values = dbls;
        }
    }
}