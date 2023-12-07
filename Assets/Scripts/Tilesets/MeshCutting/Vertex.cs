using System;
using UnityEngine;

namespace MeshCutting
{
    /// <summary>
    /// Class the represents a square in a texture
    /// </summary>
    public struct Vertex
    {
        public Vector3 position;
        public Vector2 uv;

        public Vertex(Vector3 position, Vector2 uv)
        {
            this.position = position;
            this.uv = uv;
        }

        public bool Equals(Vertex other)
        {
            return position.Equals(other.position) && uv.Equals(other.uv);
        }
        public override bool Equals(object obj)
        {
            return obj is Vertex other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(position, uv);
        }
    }
}