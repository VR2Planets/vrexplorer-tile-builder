namespace Model
{
    public class Box : BoundingVolume
    {
        public double[] values;

        public Box(double[] dbls)
        {
            values = dbls;
        }
    }
}