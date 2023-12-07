namespace Model
{
    public class Sphere : BoundingVolume
    {
        public double[] values;

        public Sphere(double[] dbls)
        {
            values = dbls;
        }
    }
}