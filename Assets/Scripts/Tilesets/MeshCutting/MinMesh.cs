using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMeshSimplifier;

namespace MeshCutting
{
    
    /// <summary>
    /// Minimal representation of a 3D mesh : Vertices, triangles and submeshes.
    /// A Vertex holds position and uv information.
    /// </summary>
    public class MinMesh
    {
        public IList<Vertex> Vertices;
        public IList<int> Triangles;
        public IList<SubMeshDescriptor> Submeshes;
        public int SubMeshCount => Submeshes?.Count ?? 0;
        public SubMeshDescriptor GetSubMesh(int iSubmesh) => Submeshes[iSubmesh];

        public Bounds CalculateBounds()
        {
            // Initialize the bounds to the first vertex in the list
            var bMin = Vertices[0].position;
            var bMax = Vertices[0].position;

            // Iterate through the rest of the vertices and update the bounds
            for (int i = 1; i < Vertices.Count; i++)
            {
                Vector3 vertex = Vertices[i].position;

                if (vertex.x < bMin.x) bMin.x = vertex.x;
                if (vertex.y < bMin.y) bMin.y = vertex.y;
                if (vertex.z < bMin.z) bMin.z = vertex.z;

                if (vertex.x > bMax.x) bMax.x = vertex.x;
                if (vertex.y > bMax.y) bMax.y = vertex.y;
                if (vertex.z > bMax.z) bMax.z = vertex.z;
            }

            return new Bounds()
            {
                min = bMin,
                max = bMax
            };
        }


        public Mesh ToMesh()
        {
            var mesh = new Mesh()
            {
                vertices = Vertices.Select(v => v.position).ToArray(),
                uv = Vertices.Select(v => v.uv).ToArray()
            };
            mesh.SetIndexBufferParams(Triangles.Count, IndexFormat.UInt32);
            mesh.subMeshCount = Submeshes.Count;
            for (int iSubmesh = 0; iSubmesh < Submeshes.Count; iSubmesh++)
            {
                var smd = Submeshes[iSubmesh];
                int[] triangles = Triangles.Skip(smd.indexStart).Take(smd.indexCount).ToArray();
                mesh.SetTriangles(triangles, iSubmesh);
            }
            return mesh;
        }

        public MeshSimplifier ToMeshSimplifier(SimplificationOptions options)
        {
            var ms = new MeshSimplifier();
            ms.SimplificationOptions = options;
            ms.Vertices = Vertices.Select(v => v.position).ToArray();
            ms.SetUVs(0, Vertices.Select(v => v.uv).ToArray());
            foreach (var smd in Submeshes)
            {
                ms.AddSubMeshTriangles(Triangles.Skip(smd.indexStart).Take(smd.indexCount).ToArray());
            }
            return ms;
        }


        public static MinMesh FromMesh(Mesh mesh)
        {
            var submeshes = new List<SubMeshDescriptor>();
            for (int i = 0; i < mesh.subMeshCount; i++) submeshes.Add(mesh.GetSubMesh(i));
            return new MinMesh
            {
                Vertices = mesh.vertices.Zip(mesh.uv ?? new Vector2[mesh.vertices.Length], (pos, uv) => new Vertex(pos, uv)).ToList(),
                Triangles = mesh.triangles,
                Submeshes = submeshes
            };
        }

        public static MinMesh FromSimplifier(MeshSimplifier simplifier)
        {
            int[][] triangles = simplifier.GetAllSubMeshTriangles();
            var submeshes = new List<SubMeshDescriptor>();
            var firstIndex = 0;
            for (var iSubmesh = 0; iSubmesh < triangles.Length; iSubmesh++)
            {
                int submeshTrianglesCount = triangles[iSubmesh].Length;
                submeshes.Add(new SubMeshDescriptor(firstIndex, submeshTrianglesCount));
                firstIndex += submeshTrianglesCount;
            }

            return new MinMesh
            {
                Vertices = simplifier.Vertices.Zip(simplifier.UV1 ?? new Vector2[simplifier.Vertices.Length], (pos, uv) => new Vertex(pos, uv))
                    .ToList(),
                Triangles = triangles.SelectMany(t => t).ToList(),
                Submeshes = submeshes
            };
        }
    }
}