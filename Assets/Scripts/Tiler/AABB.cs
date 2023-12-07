using Unity.Mathematics;

namespace Tiler
{
    /// <summary>
    /// class representing an axis aligned bounding box
    /// </summary>
    public struct AABB
    {
        public float3 min;
        public float3 max;

        public AABB(float3 min, float3 max)
        {
            this.min = min;
            this.max = max;
        }

        public bool Contains(float3 pt)
        {
            return pt.x >= min.x 
                   && pt.y >= min.y
                   && pt.z >= min.z
                   && pt.x <= max.x
                   && pt.y <= max.y
                   && pt.z <= max.z;
        }

        public bool Intersects(AABB other)
        {
            return (double) min.x <= other.max.x 
                   && (double) max.x >= other.min.x 
                   && (double) min.y <= other.max.y 
                   && (double) max.y >= other.min.y 
                   && (double) min.z <= other.max.z 
                   && (double) max.z >= other.min.z;
        }
    }
}