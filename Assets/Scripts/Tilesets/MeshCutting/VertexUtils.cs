// Inspired by https://github.com/OpenDroneMap/Obj2Tiles/blob/d5970e610a2c284c623642a82a74c701f128d2c7/Obj2Tiles.Library/Geometry/VertexUtils.cs
// Original License: GNU Affero General Public License v3.0

using MeshCutting;
using UnityEngine;

public enum Axis
{
    X,
    Y,
    Z
}

public interface IVertexUtils
{
    Vertex CutEdge(Vertex va, Vertex vb, double q);
    double GetDimension(Vertex v);

    Axis Axis { get; }
}

public class VertexUtilsX : IVertexUtils
{
    public Vertex CutEdge(Vertex va, Vertex vb, double q)
    {
        var a = va.position;
        var b = vb.position;
        var dx = b.x - a.x;
        var my = (b.y - a.y) / dx;
        var mz = (b.z - a.z) / dx;

        Debug.Assert(double.IsFinite(my));
        Debug.Assert(double.IsFinite(mz));

        // Distance from a.x to q on the x dimension
        double dq = (q - a.x);

        // ratio of q position over the x dimension length
        // ie. tell that q is p % from va to vb
        var p = dq / dx;

        var uvPosition = Vector2.Lerp(va.uv, vb.uv, (float) p);
        var cutPosition = new Vector3((float) q, (float) (my * (q - a.x) + a.y), (float) (mz * (q - a.x) + a.z));
        return new Vertex(cutPosition, uvPosition);
    }

    public double GetDimension(Vertex v)
    {
        return v.position.x;
    }

    public Axis Axis => Axis.X;
}

public class VertexUtilsY : IVertexUtils
{

    public Vertex CutEdge(Vertex va, Vertex vb, double q)
    {
        var a = va.position;
        var b = vb.position;
        var dy = b.y - a.y;
        var mx = (b.x - a.x) / dy;
        var mz = (b.z - a.z) / dy;

        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(mz));

        // Distance from a.y to q on the y dimension
        double dq = (q - a.y);

        // ratio of q position over the y dimension length
        // ie. tell that q is p % from va to vb
        var p = dq / dy;

        var uvPosition = Vector2.Lerp(va.uv, vb.uv, (float) p);
        return new Vertex(new Vector3((float) (mx * (q - a.y) + a.x), (float) q, (float) (mz * (q - a.y) + a.z)), uvPosition);
    }

    public double GetDimension(Vertex v)
    {
        return v.position.y;
    }

    public Axis Axis => Axis.Y;

}

public class VertexUtilsZ : IVertexUtils
{
    public Vertex CutEdge(Vertex va, Vertex vb, double q)
    {
        var a = va.position;
        var b = vb.position;
        var dz = b.z - a.z;
        var mx = (b.x - a.x) / dz;
        var my = (b.y - a.y) / dz;

        Debug.Assert(double.IsFinite(mx));
        Debug.Assert(double.IsFinite(my));

        // Distance from a.z to q on the z dimension
        double dq = (q - a.z);

        // ratio of q position over the z dimension length
        // ie. tell that q is p % from va to vb
        var p = dq / dz;

        var uvPosition = Vector2.Lerp(va.uv, vb.uv, (float) p);
        return new Vertex(new Vector3((float) (mx * (q - a.z) + a.x), (float) (my * (q - a.z) + a.y), (float) q), uvPosition);
    }

    public double GetDimension(Vertex v)
    {
        return v.position.z;
    }

    public Axis Axis => Axis.Z;

}