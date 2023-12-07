using Unity.Mathematics;

public struct TilePoint
{
    public double x;
    public double y;
    public double z;

    public static implicit operator TilePoint(double3 v)
    {
        return new TilePoint
        {
            x = v.x,
            y = v.y,
            z = v.z
        };
    }
}

public class TileBoundingBox
{
    public TilePoint min = new TilePoint();
    public TilePoint max = new TilePoint();

    public double[] GetBoundingBox()
    {
        var minPt = new double3(
            min.x,
            min.y,
            min.z
        );

        var maxPt = new double3(
            max.x,
            max.y,
            max.z
        );

        var center = (minPt + maxPt) / 2;
        var size = (maxPt - minPt) / 2;

        return new[]
        {
            -center.x, -center.z, center.y,
            -size.x, 0, 0,
            0, -size.z, 0,
            0, 0, size.y,
        };
    }
}