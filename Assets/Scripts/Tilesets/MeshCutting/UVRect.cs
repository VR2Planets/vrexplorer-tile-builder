using Unity.Mathematics;
using UnityEngine;

namespace MeshCutting
{
    /// <summary>
    /// Class the represents a square in a texture
    /// </summary>
    public struct UVRect
    {
        public int submesh;
        public double2 min;
        public double2 max;

        public double Width => max.x - min.x;
        public double Height => max.y - min.y;

        /// <param name="ratios">Source texture sizes divided by the target output size.</param>
        /// <returns>A size adjusted so that UVRect shares the same pixel size</returns>
        public double WeightedHeight(Vector2[] ratios) => Height * ratios[submesh].y;

        /// <param name="ratios">Source texture sizes divided by the target output size.</param>
        /// <returns>A size adjusted so that UVRect shares the same pixel size</returns>
        public double WeightedWidth(Vector2[] ratios) => Width * ratios[submesh].x;

        public bool IsOverlapping(UVRect r)
        {
            return submesh == r.submesh
                && min.x <= r.max.x
                && min.y <= r.max.y
                && r.min.x <= max.x
                && r.min.y <= max.y;
        }

        public UVRect(int submesh, double2 min, double2 max)
        {
            this.submesh = submesh;
            this.min = min;
            this.max = max;
        }

        public UVRect Union(UVRect b)
        {
            return new UVRect(
                submesh,
                math.min(min, b.min),
                math.max(max, b.max)
            );
        }

        public bool Contains(int submesh, Vector2 uv)
        {
            return this.submesh == submesh
                && uv.x >= min.x
                && uv.y >= min.y
                && uv.x <= max.x
                && uv.y <= max.y;
        }

        public override string ToString()
        {
            return $"UV({submesh},{min},{max})";
        }
    }
}