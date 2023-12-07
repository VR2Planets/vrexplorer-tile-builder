using System.Collections.Generic;
using UnityEngine;
using UnityMeshSimplifier;

namespace MeshCutting
{
    public static class LODGenerator
    {
        public static MinMesh CreateLOD(MinMesh sourceMesh, SimplificationOptions simplificationOptions, float quality)
        {
            var simplifier = sourceMesh.ToMeshSimplifier(simplificationOptions);
            simplifier.SimplifyMesh(quality);
            var simplifiedMesh = MinMesh.FromSimplifier(simplifier);
            return simplifiedMesh;
        }

        public static float CalculateHausdorffError(IList<Vertex> originalVertices, IList<Vertex> simplifiedVertices)
        {
            float maxDistance = 0f;

            foreach (var simplifiedVertex in simplifiedVertices)
            {
                float minDistance = float.MaxValue;

                foreach (var originalVertex in originalVertices)
                {
                    float distance = Vector3.Distance(simplifiedVertex.position, originalVertex.position);
                    minDistance = Mathf.Min(minDistance, distance);
                }

                maxDistance = Mathf.Max(maxDistance, minDistance);
            }

            return maxDistance;
        }
    }
}