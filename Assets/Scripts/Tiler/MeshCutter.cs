using System;
using System.Collections.Generic;
using MeshCutting;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Vr2Planets.Maths;

namespace Tiler
{
    public class MeshCutter : MonoBehaviour
    {
        public MeshFilter meshFilter;
        public GameObject bounds;
        public int textureSize;

        public void Go()
        {
            var center = bounds.transform.position;
            var halfSize = bounds.transform.lossyScale * 0.5f;

            var newMesh = Cut(
                meshFilter.sharedMesh,
                new AABB(
                    center - halfSize,
                    center + halfSize
                )
            );

            var go = new GameObject();
            var newMeshFilter = go.AddComponent<MeshFilter>();
            newMeshFilter.sharedMesh = newMesh;
            go.AddComponent<MeshRenderer>();
        }

        public Mesh Cut(Mesh meshToCut, AABB boundingBox)
        {
            // Only fetch these arrays ones as it is expensive 
            var triangles = meshToCut.triangles;
            var vertices = meshToCut.vertices;
            var uvs = meshToCut.uv;

            var verticesList = new List<Vector3>();
            var triangleList = new List<int>();
            var submeshList = new List<int>();
            var uvList = new List<Vector2>();
            var uvRectList = new List<UVRect>();

            for (int i = 0; i < meshToCut.subMeshCount; i++)
            {
                var submesh = meshToCut.GetSubMesh(i);
                var indexStart = submesh.indexStart;

                // Combine all the submeshes into one mesh
                if (submesh.topology == MeshTopology.Triangles)
                {
                    for (int triIndex = 0; triIndex < submesh.indexCount; triIndex += 3)
                    {
                        var idxStart = indexStart + triIndex;

                        var index1 = triangles[idxStart];
                        var index2 = triangles[idxStart + 1];
                        var index3 = triangles[idxStart + 2];

                        var vert1 = vertices[index1];
                        var vert2 = vertices[index2];
                        var vert3 = vertices[index3];

                        var uv1 = uvs[index1];
                        var uv2 = uvs[index2];
                        var uv3 = uvs[index3];

                        // Include include any triangle that intersects with the bounding box this is not correct. What
                        // needs to be done is to clip each triangle by each face of the box and then if there is
                        // anything left add that to the final mesh  
                        if (boundingBox.Contains(vert1)
                            && boundingBox.Contains(vert2)
                            && boundingBox.Contains(vert3))
                        {
                            var start = verticesList.Count;

                            verticesList.Add(vert1);
                            verticesList.Add(vert2);
                            verticesList.Add(vert3);

                            triangleList.Add(start);
                            triangleList.Add(start + 1);
                            triangleList.Add(start + 2);

                            uvList.Add(uv1);
                            uvList.Add(uv2);
                            uvList.Add(uv3);

                            submeshList.Add(i);
                            submeshList.Add(i);
                            submeshList.Add(i);

                            // Create a list off rectangles that correspond to where this triangle references the
                            // texture. This is wasteful as the rectangle is bigger than the triangle however it makes
                            // the algorithm easier :). At the end of the process we get a list of lots of little
                            // rectangles showing what parts of what textures are being used.
                            uvRectList.Add(new UVRect(
                                i,
                                math.min(math.min(uv1, uv2), uv3),
                                math.max(math.max(uv1, uv2), uv3)
                            ));
                        }
                        else
                        {
                            float3 min = new float3(
                                Math.Min(vert1.x, Math.Min(vert2.x, vert3.x)),
                                Math.Min(vert1.y, Math.Min(vert2.y, vert3.y)),
                                Math.Min(vert1.z, Math.Min(vert2.z, vert3.z))
                            );
                            
                            float3 max = new float3(
                                Math.Max(vert1.x, Math.Max(vert2.x, vert3.x)),
                                Math.Max(vert1.y, Math.Max(vert2.y, vert3.y)),
                                Math.Max(vert1.z, Math.Max(vert2.z, vert3.z))
                            );

                            var triBoundingBox = new AABB(min, max);

                            if (triBoundingBox.Intersects(boundingBox))
                            {
                                var start = verticesList.Count;

                                verticesList.Add(vert1);
                                verticesList.Add(vert2);
                                verticesList.Add(vert3);

                                triangleList.Add(start);
                                triangleList.Add(start + 1);
                                triangleList.Add(start + 2);

                                uvList.Add(uv1);
                                uvList.Add(uv2);
                                uvList.Add(uv3);

                                submeshList.Add(i);
                                submeshList.Add(i);
                                submeshList.Add(i);

                                uvRectList.Add(new UVRect(
                                    i,
                                    math.min(math.min(uv1, uv2), uv3),
                                    math.max(math.max(uv1, uv2), uv3)
                                ));
                            }
                        }
                    }
                }
            }

            // Create the new mesh and let unity clean up any problems
            var newMesh = new Mesh
            {
                indexFormat = verticesList.Count > UInt16.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = verticesList.ToArray(),
                triangles = triangleList.ToArray(),
                uv = uvList.ToArray()
            };

            newMesh.Optimize();

            return newMesh;
        }
    }
}

/*
private void Cut(Mesh mesh, DoubleBounds3 b, int targetTextureSize)
{
    var vertices = mesh.vertices;
    var triangles = mesh.triangles;
    var submeshIndexes = new int[vertices.Length];

    // Source textures might be of different size, and we want to pixel size to be the same in the output, whatever the input resolution is.
    // Ie. we don't want an uv that target 16x16, and a same size uv that target 8kx8k to have the same size in the output texture because
    // it doesn't cover the same amount of pixels.
    // The ratio indicated how big the texture is relative to the target.
    // As such UVRect Weighted sizes will use this ratios so that all UVRect share the same pixel size.
    Vector2[] ratios = textures
        .Select(t =>
            t == null
                ? Vector2.one
                : new Vector2((float)t.width / targetTextureSize, (float)t.height / targetTextureSize))
        .ToArray();

    // uvRectList is calculated here rather than during splitting because splitting is operated 3 times (once per axis)
    // so it should be more efficient to do it only once on the final result.
    // We also enlarge the each square by a few pixels so the bilinear filtering works.
    List<UVRect> uvRectList = new List<UVRect>();
    for (int iSubmesh = 0; iSubmesh < mesh.subMeshCount; iSubmesh++)
    {
        var submesh = mesh.GetSubMesh(iSubmesh);
        int indexStart = submesh.indexStart;

        // For bilinear filtering to work, we need a 1 pixel margin on the target texture
        // So we calculate the required margin on the source offset so that it results in
        // a one pixel margin on target.
        int pixelsMarginInTarget = 2;
        Vector2Int pixelsMarginInSource = new Vector2Int(
            (int)Math.Ceiling(pixelsMarginInTarget * ratios[iSubmesh].x),
            (int)Math.Ceiling(pixelsMarginInTarget * ratios[iSubmesh].y)
        );

        if (submesh.topology == MeshTopology.Triangles)
        {
            for (int vIndex = 0; vIndex < submesh.indexCount; vIndex += 3)
            {
                var sourceTextureSize = Vector2Int.RoundToInt(targetTextureSize * ratios[iSubmesh]);

                var idxStart = indexStart + vIndex;

                int iVertex1 = triangles[idxStart];
                int iVertex2 = triangles[idxStart + 1];
                int iVertex3 = triangles[idxStart + 2];

                submeshIndexes[iVertex1] = iSubmesh;
                submeshIndexes[iVertex2] = iSubmesh;
                submeshIndexes[iVertex3] = iSubmesh;

                // Get the closest source pixel from the uv position
                var px1 = Vector2Int.FloorToInt(vertices[iVertex1].uv * sourceTextureSize);
                var px2 = Vector2Int.FloorToInt(vertices[iVertex2].uv * sourceTextureSize);
                var px3 = Vector2Int.FloorToInt(vertices[iVertex3].uv * sourceTextureSize);

                var px1max = Vector2Int.CeilToInt(vertices[iVertex1].uv * sourceTextureSize);
                var px2max = Vector2Int.CeilToInt(vertices[iVertex2].uv * sourceTextureSize);
                var px3max = Vector2Int.CeilToInt(vertices[iVertex3].uv * sourceTextureSize);

                // get the square regrouping all the pixels with a 1 pixel margin
                var pxMin = Toolbox.Min(px1, px2, px3) - pixelsMarginInSource;
                var pxMax = Toolbox.Max(px1max, px2max, px3max) + pixelsMarginInSource;

                // calculate the matching uv position
                var uvMin = new double2(
                    (double)pxMin.x / sourceTextureSize.x,
                    (double)pxMin.y / sourceTextureSize.y
                    );
                var uvMax = new double2(
                    (double)pxMax.x / sourceTextureSize.x,
                    (double)pxMax.y / sourceTextureSize.y
                    );

                uvRectList.Add(new UVRect(
                    iSubmesh,
                    uvMin,
                    uvMax
                ));
            }
        }
    }
}
}
*/
        
    /*
    public void Cut(Mesh mesh, DoubleBounds3 b)
    {
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var submeshIndexes = new int[vertices.Length];

        // Source textures might be of different size, and we want to pixel size to be the same in the output, whatever the input resolution is.
        // Ie. we don't want an uv that target 16x16, and a same size uv that target 8kx8k to have the same size in the output texture because
        // it doesn't cover the same amount of pixels.
        // The ratio indicated how big the texture is relative to the target.
        // As such UVRect Weighted sizes will use this ratios so that all UVRect share the same pixel size.
        Vector2[] ratios = textures
            .Select(t => t == null ? Vector2.one : new Vector2((float) t.width / targetTextureSize, (float) t.height / targetTextureSize))
            .ToArray();

        // uvRectList is calculated here rather than during splitting because splitting is operated 3 times (once per axis)
        // so it should be more efficient to do it only once on the final result.
        // We also enlarge the each square by a few pixels so the bilinear filtering works.
        List<UVRect> uvRectList = new List<UVRect>();
        for (int iSubmesh = 0; iSubmesh < mesh.SubMeshCount; iSubmesh++)
        {
            var submesh = mesh.GetSubMesh(iSubmesh);
            int indexStart = submesh.indexStart;

            // For bilinear filtering to work, we need a 1 pixel margin on the target texture
            // So we calculate the required margin on the source offset so that it results in a one pixel margin on target.
            int pixelsMarginInTarget = 2;
            Vector2Int pixelsMarginInSource = new Vector2Int(
                (int) Math.Ceiling(pixelsMarginInTarget * ratios[iSubmesh].x),
                (int) Math.Ceiling(pixelsMarginInTarget * ratios[iSubmesh].y)
            );

            if (submesh.topology == MeshTopology.Triangles)
            {
                for (int vIndex = 0; vIndex < submesh.indexCount; vIndex += 3)
                {
                    var sourceTextureSize = Vector2Int.RoundToInt(targetTextureSize * ratios[iSubmesh]);
                    var idxStart = indexStart + vIndex;
                    int iVertex1 = triangles[idxStart];
                    int iVertex2 = triangles[idxStart + 1];
                    int iVertex3 = triangles[idxStart + 2];
                    submeshIndexes[iVertex1] = iSubmesh;
                    submeshIndexes[iVertex2] = iSubmesh;
                    submeshIndexes[iVertex3] = iSubmesh;
                    // Get the closest source pixel from the uv position
                    var px1 = Vector2Int.FloorToInt(vertices[iVertex1].uv * sourceTextureSize);
                    var px2 = Vector2Int.FloorToInt(vertices[iVertex2].uv * sourceTextureSize);
                    var px3 = Vector2Int.FloorToInt(vertices[iVertex3].uv * sourceTextureSize);

                    var px1max = Vector2Int.CeilToInt(vertices[iVertex1].uv * sourceTextureSize);
                    var px2max = Vector2Int.CeilToInt(vertices[iVertex2].uv * sourceTextureSize);
                    var px3max = Vector2Int.CeilToInt(vertices[iVertex3].uv * sourceTextureSize);

                    // get the square regrouping all the pixels with a 1 pixel margin
                    var pxMin = Toolbox.Min(px1, px2, px3) - pixelsMarginInSource;
                    var pxMax = Toolbox.Max(px1max, px2max, px3max) + pixelsMarginInSource;

                    // calculate the matching uv position
                    var uvMin = new double2((double) pxMin.x / sourceTextureSize.x, (double) pxMin.y / sourceTextureSize.y);
                    var uvMax = new double2((double) pxMax.x / sourceTextureSize.x, (double) pxMax.y / sourceTextureSize.y);
                    uvRectList.Add(new UVRect(
                        iSubmesh,
                        uvMin,
                        uvMax
                    ));
                }
            }
        }

        // At the moment we have lots of little rectangles that show what squares of each texture are being
        // used. If two squares overlap then we need to group them together as otherwise we will have seams
        // between them. ClusterUvRects groups any overlapping squares into bigger squares.
        var clusteredUvs = ClusterUvRects(uvRectList);

        // Next we order the squares be height so we can use the UVPacking algorithm described in the document
        // folder
        clusteredUvs.Sort((a, b) => b.WeightedHeight(ratios).CompareTo(a.WeightedHeight(ratios)));

        // Pack the clusters into a squarish layout to serve as target texture destinations
        var (packedRects, scale) = PackClustersIntoRectangles(clusteredUvs, ratios);

        // Create the final texture
        var newTexture = CreatePackedTexture(textures, textureScales, textureOffsets,
            clusteredUvs, packedRects, targetTextureSize, scale);

        // Since we have created a new texture we need to remap all the uvs of the mesh to point to the correct
        // place in the final mesh rather than keeping track of what triangles are in what square it seems to
        // be faster just to text each triangle against each square and if it is in that square map the uvs to
        // the new location. There should be a way of speeding this up :)
        var newVertices = vertices.Select((v, j) => {
            var uv = v.uv;
            var submeshIndex = submeshIndexes[j];

            for (int k = 0; k < clusteredUvs.Count && k < packedRects.Count; k++)
            {
                var sourceRect = clusteredUvs[k];
                var destinationRect = packedRects[k];

                if (sourceRect.Contains(submeshIndex, uv))
                {
                    var px = (uv.x - sourceRect.min.x) / sourceRect.Width;
                    var py = (uv.y - sourceRect.min.y) / sourceRect.Height;

                    return new Vertex(v.position, new Vector2(
                        (float) (destinationRect.min.x + (px * destinationRect.Width)),
                        (float) (destinationRect.min.y + (py * destinationRect.Height))
                    ));
                }
            }
            return v;
        });

        // Create the new mesh and let unity clean up any problems
        var newMinMesh = new MinMesh
        {
            Vertices = newVertices.ToList(),
            Triangles = triangles.ToList(),
            Submeshes = new List<SubMeshDescriptor>
                { new SubMeshDescriptor(0, triangles.Count) }
        };

        return (newMinMesh, newTexture, scale);
    }
}*/