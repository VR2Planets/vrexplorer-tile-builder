using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Debugging;
using JetBrains.Annotations;
using Model;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityMeshSimplifier;
using Object = UnityEngine.Object;

namespace MeshCutting
{

    /// <summary>
    /// Various tools required to slice a 3D model into several tiles with recursive LOD generation of mesh and textures.
    /// </summary>
    public static class MeshCuttingHelper
    {
        private static TimeMeasure _timeMeasure;
        private static Texture2DPool _texture2DPool;
        private static MeshGameObjectPool _internalPool;

        private static CopyRegionShader _crs;
        public static CopyRegionShader CopyRegionShader {
            get {
                if (_crs == null) _crs = new CopyRegionShader();
                return _crs;
            }
        }

        public readonly static IVertexUtils Yutils3 = new VertexUtilsY();
        public readonly static IVertexUtils Xutils3 = new VertexUtilsX();
        public readonly static IVertexUtils Zutils3 = new VertexUtilsZ();

        /// <summary>
        /// 1. Simplify and export for current LOD
        /// 2. Split and recurse if the triangle count and texture detail of each slice is still higher to the target
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="localToWorldMatrix">Matrix to transform an object local coordinate to a world coordinate</param>
        /// <param name="sourceMesh">Mesh to cut and export</param>
        /// <param name="materials">List of materials. There should be one per submesh in the submesh.</param>
        /// <param name="getSplitPoint">Given a mesh, estimate the best split point</param>
        /// <param name="trianglesTargetCount">The max number of triangles that a cube should contains</param>
        /// <param name="maxLodLevel">If this LOD is reached, stop the recursion.</param>
        /// <param name="simplificationOptions">PreserveBorderEdges is mandatory for correct results.</param>
        /// <param name="targetTextureSize">Size of the texture for each tile. Default 1024.</param>
        /// <param name="blindExportPath">Should be null to have the regular preview.
        /// If not null, any cut will be immediately exported then disposed to free memory. No preview will be available but it can help work on larger files.</param>
        /// <param name="outChunkExports">output of the generated chunks</param>
        /// <param name="previewCamera"></param>
        /// <param name="name">Name of the block</param>
        /// <param name="lodLevel">Current tile level of detail being exported.</param>
        /// <param name="timeMeasure"></param>
        /// <param name="log"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static async Task<Tile> ExportLODAndSplitRecursive(GameObject parent,
            Matrix4x4 localToWorldMatrix,
            MinMesh sourceMesh,
            Material[] materials,
            Func<MinMesh, Vector3> getSplitPoint,
            int trianglesTargetCount,
            int maxLodLevel,
            SimplificationOptions simplificationOptions,
            int targetTextureSize,
            [CanBeNull] string blindExportPath,
            IDictionary<string, ExportEntry> outChunkExports,
            [CanBeNull] Camera previewCamera,
            string name = "",
            int lodLevel = 0,
            TimeMeasure timeMeasure = null,
            [CanBeNull] Action<string> log = null,
            [CanBeNull] Action<float, string> progress = null)
        {
            bool isBlindExport = !string.IsNullOrEmpty(blindExportPath);

            if (_texture2DPool == null)
            {
                _texture2DPool = new Texture2DPool(TextureFormat.ARGB32, false);
            }

            progress?.Invoke(lodLevel / (maxLodLevel + 1f), $"Cutting chunks of level {lodLevel} / {(maxLodLevel)}");
            await Task.Yield();
            _timeMeasure = timeMeasure ?? new TimeMeasure();
            // Debug.Log($"Exporting and cutting {name} at lod {lodLevel}");
            if (materials.Length != sourceMesh.Submeshes.Count)
            {
                Debug.LogWarning(
                    $"Materials and submeshes mismatch: {materials.Length} materials and {sourceMesh.Submeshes.Count} submesh. Some textures might be lost in the process.");
            }

            var splitTiles = new List<Tile>();

            // 1. Export for current LOD
            var exportedMesh = sourceMesh;

            bool isTriangleCountReached = (sourceMesh.Triangles.Count / 3) <= trianglesTargetCount;
            bool isMostDetailed = isTriangleCountReached;
            float hausdorffDistance = 0;
            // Is the triangle count in the mesh is higher than target, simplify before exporting
            if (!isMostDetailed)
            {
                _timeMeasure.StartMeasure($"CreateLOD");
                //await Task.Run(() => {
                exportedMesh = LODGenerator.CreateLOD(
                    sourceMesh, 
                    simplificationOptions, 
                    (float) trianglesTargetCount / sourceMesh.Triangles.Count);
                //});
                _timeMeasure.StopMeasure("CreateLOD"); // CreateLOD
            }

            // We want to keep only one texture for this mesh: merge the submeshes into one mesh and the textures into one texture
            // material information is lost
            var textures = materials.Select(m => m.mainTexture).ToArray();
            var textureScales = materials.Select(m => m.mainTextureScale).ToArray();
            var textureOffsets = materials.Select(m => m.mainTextureOffset).ToArray();
            
            MinMesh outputMesh = exportedMesh;
            Texture2D outputTexture = textures.Cast<Texture2D>().FirstOrDefault();
            double appliedTextureScaling = 1f;
            bool isTouputTextureTemporary = false;
            if (textures.Length > 1 && exportedMesh.Submeshes.Count > 1
                || textures.Length == 1 && (textures[0].width > targetTextureSize || textures[0].height > targetTextureSize))
            {
                _timeMeasure.StartMeasure($"Merge Submeshes");
                (outputMesh, outputTexture, appliedTextureScaling) = MergeSubmeshes(textures, 
                    textureScales, textureOffsets, 
                    exportedMesh, targetTextureSize);
                isTouputTextureTemporary = isBlindExport;
                _timeMeasure.StopMeasure("Merge Submeshes"); // Merge Submeshes
            }
            bool isTextureSizeReached = Math.Abs(appliedTextureScaling - 1) < 0.00001f;
            isMostDetailed &= isTextureSizeReached;
            _timeMeasure.StartMeasure($"PrepareExportChunk");
            var chunkExport = PrepareExport(parent,
                name,
                lodLevel,
                outputMesh,
                outputTexture,
                isMostDetailed,
                (sourceMesh.Triangles.Count / 3).ToString(),
                (float) appliedTextureScaling,
                hausdorffDistance,
                !isBlindExport,
                previewCamera
            );

            _timeMeasure.StopMeasure("PrepareExportChunk");

            if (isBlindExport)
            {
                _timeMeasure.StartMeasure($"ExportChunk");
                await TilesExporter.ExportChunkUnit(
                    chunkExport.Model,
                    Path.Combine(blindExportPath, $"{chunkExport.Name}.glb"),
                    Path.Combine(blindExportPath, $"{chunkExport.Name}.json"),
                    chunkExport.TileMetadata
                );
                _internalPool.Release(chunkExport.Model.GetComponent<MeshFilter>());
                if (isTouputTextureTemporary) _texture2DPool.Return(outputTexture);
                _timeMeasure.StopMeasure($"ExportChunk");
            }
            outChunkExports[chunkExport.Name] = chunkExport;

            // 2. Split and recurse
            if (lodLevel + 1 > maxLodLevel)
            {
                if (!isMostDetailed)
                {
                    var reason = "";
                    if (!isTextureSizeReached)
                    {
                        reason += $"\nThe texture best quality was not reached (last scale applied was {appliedTextureScaling}). "
                            + $"You might increase the max texture size per tile or the max depth and retry the tile cutting.";
                    }
                    if (!isTriangleCountReached)
                    {
                        reason +=
                            $"\nThe target number of triangles was not reached and finest level of details that does not match original quality."
                            + $" You might increase the max triangles per tile or the max depth and retry the tile cutting.";
                    }
                    log?.Invoke(
                        $"Max lod level reached early ({maxLodLevel}). {reason}");
                }
                return null;
            }
            // Split and recurse if we have not yet reach the target quality
            if (!isMostDetailed)
            {
                try
                {
                    _timeMeasure.StartMeasure($"Choosing split point");
                    var splitPoint = getSplitPoint(sourceMesh);
                    _timeMeasure.StopMeasure("Choosing split point");

                    var sourceMeshSplits = OctreeSplit(sourceMesh, splitPoint);
                    var names = new[] { "TLN", "TLF", "BLN", "BLF", "TRN", "TRF", "BRN", "BRF" };

                    // When blind exporting, created a new object for each tile because we want to dispose them individually as soon as it is exported
                    // Otherwise compose a recursive tree for easier browsing in editor
                    GameObject parentGameObject = isBlindExport ? null : chunkExport.Model;
                    if (parentGameObject == null)
                    {
                        parentGameObject = new GameObject("LOD" + (lodLevel + 1));
                        parentGameObject.transform.parent = parent.transform;
                        parentGameObject.transform.localPosition = Vector3.zero;
                        parentGameObject.transform.localRotation = Quaternion.identity;
                        parentGameObject.transform.localScale = Vector3.one;
                    }

                    // export and recurse each slice
                    for (int i = 0; i < sourceMeshSplits.Length; i++)
                    {
                        var mesh = sourceMeshSplits[i];
                        if (mesh.Vertices.Count > 0)
                        {
                            var chunkname = name + "_" + names[i];
                            // schedule recursive splitting
                            splitTiles.Add(await ExportLODAndSplitRecursive(parentGameObject,
                                    localToWorldMatrix,
                                    mesh,
                                    materials,
                                    getSplitPoint,
                                    trianglesTargetCount,
                                    maxLodLevel,
                                    simplificationOptions,
                                    targetTextureSize,
                                    blindExportPath,
                                    outChunkExports,
                                    previewCamera,
                                    chunkname,
                                    lodLevel + 1,
                                    timeMeasure,
                                    log,
                                    progress
                                )
                            );
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    throw;
                }
            }

            try
            {
                // Finally, compose the 3D tile information to build the tileset.json
                var chunkDetail = chunkExport.TileMetadata;

                var m = GenerateTilesetHelper.UnityMatrixTo3DTiles(localToWorldMatrix);

                var tile = new Tile
                {
                    boundingVolume = new Box(chunkDetail.boundingBox.GetBoundingBox()),
                    content = new Content
                    {
                        uri = TilesExporter.GetExportName(name, lodLevel) + ".glb"
                    },
                    geometricError = chunkDetail.geometricErrorPixelSize,
                    geometricErrorPixelSize = chunkDetail.geometricErrorPixelSize,
                    geometricErrorTextureDownscale = chunkDetail.geometricErrorTextureDownscale,
                    geometricErrorTileDepth = chunkDetail.geometricErrorTileDepth,
                    geometricErrorHausdorff = chunkDetail.geometricErrorHausdorff,
                    geometricErrorThreshold = chunkDetail.geometricErrorThreshold,
                    refine = "REPLACE",
                    transform = new[]
                    {
                        m.c0.x, m.c0.y, m.c0.z, m.c0.w,
                        m.c1.x, m.c1.y, m.c1.z, m.c1.w,
                        m.c2.x, m.c2.y, m.c2.z, m.c2.w,
                        m.c3.x, m.c3.y, m.c3.z, m.c3.w
                    }
                };

                tile.children = splitTiles.Where(t => t != null).ToList();

                return tile;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        /// <summary>
        /// Compose a GameObject with the 3D Models and compile the informations required for export.
        /// In this step we also attach debug informations (preview off boundaries and errors).
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        /// <param name="lodLevel"></param>
        /// <param name="exportedMesh"></param>
        /// <param name="texture"></param>
        /// <param name="mostDetailed"></param>
        /// <param name="fullResolutionTrianglesCountDebug"></param>
        /// <param name="textureScaling"></param>
        /// <param name="hausdorffDistance"></param>
        /// <param name="previewCamera"></param>
        /// <returns>Geometric error of the LOD</returns>
        private static ExportEntry PrepareExport(GameObject parent, string name, int lodLevel, MinMesh exportedMesh,
            Texture2D texture,
            bool mostDetailed,
            string fullResolutionTrianglesCountDebug,
            float textureScaling,
            float hausdorffDistance,
            bool displayDebugText,
            [CanBeNull] Camera previewCamera)
        {

            var chunkObj = CreateObject(parent, exportedMesh, texture, $"{lodLevel}_{name}");
            var chunkDetails = GetTileData(
                mostDetailed, 
                chunkObj, 
                Math.Min(texture.height, texture.width), 
                textureScaling, 
                lodLevel, 
                hausdorffDistance);
            var d = ShowBoundariesDebug.CreateGameObject(parent.transform, $"{lodLevel}_{name}_DEBUG");
            d.Recalculate(
                parent.transform,
                chunkObj.GetComponent<MeshFilter>(),
                chunkObj.GetComponent<Renderer>(),
                lodLevel,
                fullResolutionTrianglesCountDebug,
                (1 / textureScaling).ToString("F2"),
                $"{chunkDetails.geometricErrorPixelSize * 1000:F2}"
                + $"/{chunkDetails.geometricErrorTextureDownscale * 1000:F2}"
                + $"/{chunkDetails.geometricErrorTileDepth * 1000:F2}"
                + $"/{chunkDetails.geometricErrorThreshold * 1000:F2}"
                + $"/{chunkDetails.geometricErrorHausdorff * 1000:F2}",
                previewCamera,
                mostDetailed,
                displayDebugText
            );

            return new(TilesExporter.GetExportName(name, lodLevel), chunkDetails, chunkObj);
        }

        /// <summary>
        /// Split a MinMesh into 8 sub MinMesh to form an Octree.
        /// This step creates additional vertex points on the edges.
        /// </summary>
        /// <param name="mesh">Mesh to split.</param>
        /// <param name="splitPoint">Must be inside the boundaries of the Mesh.</param>
        /// <returns></returns>
        private static MinMesh[] OctreeSplit(MinMesh mesh, Vector3 splitPoint)
        {
            _timeMeasure.StartMeasure($"OctreeSplit");

            Split(mesh.Vertices, mesh.Triangles, mesh.Submeshes, splitPoint.x, Xutils3,
                out var left, out var leftTriangles, out var leftSubmeshes,
                out var right, out var rightTriangles, out var rightSubmeshes
            );

            Split(left, leftTriangles, leftSubmeshes, splitPoint.y, Yutils3,
                out var bottomleft, out var bottomleftTriangles, out var bottomleftSubmeshes,
                out var topleft, out var topleftTriangles, out var topleftSubmeshes
            );
            Split(right, rightTriangles, rightSubmeshes, splitPoint.y, Yutils3,
                out var bottomright, out var bottomrightTriangles, out var bottomrightSubmeshes,
                out var topright, out var toprightTriangles, out var toprightSubmeshes
            );

            Split(topleft, topleftTriangles, topleftSubmeshes, splitPoint.z, Zutils3,
                out var topleftnear, out var topleftnearTriangles, out var topleftnearSubmeshes,
                out var topleftfar, out var topleftfarTriangles, out var topleftfarSubmeshes
            );
            Split(bottomleft, bottomleftTriangles, bottomleftSubmeshes, splitPoint.z, Zutils3,
                out var bottomleftnear, out var bottomleftnearTriangles, out var bottomleftnearSubmeshes,
                out var bottomleftfar, out var bottomleftfarTriangles, out var bottomleftfarSubmeshes
            );
            Split(topright, toprightTriangles, toprightSubmeshes, splitPoint.z, Zutils3,
                out var toprightnear, out var toprightnearTriangles, out var toprightnearSubmeshes,
                out var toprightfar, out var toprightfarTriangles, out var toprightfarSubmeshes
            );
            Split(bottomright, bottomrightTriangles, bottomrightSubmeshes, splitPoint.z, Zutils3,
                out var bottomrightnear, out var bottomrightnearTriangles, out var bottomrightnearSubmeshes,
                out var bottomrightfar, out var bottomrightfarTriangles, out var bottomrightfarSubmeshes
            );
            var meshes = new[]
            {
                new MinMesh { Vertices = topleftnear, Triangles = topleftnearTriangles, Submeshes = topleftnearSubmeshes },
                new MinMesh { Vertices = topleftfar, Triangles = topleftfarTriangles, Submeshes = topleftfarSubmeshes },
                new MinMesh { Vertices = bottomleftnear, Triangles = bottomleftnearTriangles, Submeshes = bottomleftnearSubmeshes },
                new MinMesh { Vertices = bottomleftfar, Triangles = bottomleftfarTriangles, Submeshes = bottomleftfarSubmeshes },
                new MinMesh { Vertices = toprightnear, Triangles = toprightnearTriangles, Submeshes = toprightnearSubmeshes },
                new MinMesh { Vertices = toprightfar, Triangles = toprightfarTriangles, Submeshes = toprightfarSubmeshes },
                new MinMesh { Vertices = bottomrightnear, Triangles = bottomrightnearTriangles, Submeshes = bottomrightnearSubmeshes },
                new MinMesh { Vertices = bottomrightfar, Triangles = bottomrightfarTriangles, Submeshes = bottomrightfarSubmeshes }
            };
            _timeMeasure.StopMeasure($"OctreeSplit");
            return meshes;
        }

        /// <summary>
        /// Split a mesh in 2 halves.
        /// Rebuild left and right lists of vertex (position + uv), triangles and submeshes.
        /// This step creates additional vertex points on the edges, uv are calculated for this new points based on the split edge.
        /// </summary>
        /// <param name="meshVertices"></param>
        /// <param name="meshTriangles"></param>
        /// <param name="submeshesList"></param>
        /// <param name="q"></param>
        /// <param name="utils"></param>
        /// <param name="left"></param>
        /// <param name="leftTriangles"></param>
        /// <param name="leftSubmeshes"></param>
        /// <param name="right"></param>
        /// <param name="rightTriangles"></param>
        /// <param name="rightSubmeshes"></param>
        /// <returns>Intersections count = number of new vertices created.</returns>
        public static int Split(
            IList<Vertex> meshVertices, IList<int> meshTriangles, IList<SubMeshDescriptor> submeshesList, double q, IVertexUtils utils,
            out List<Vertex> left, out List<int> leftTriangles, out List<SubMeshDescriptor> leftSubmeshes,
            out List<Vertex> right, out List<int> rightTriangles, out List<SubMeshDescriptor> rightSubmeshes)
        {
            //_timeMeasure.StartMeasure($"Split");
            int subMeshCount = submeshesList.Count;

            var leftVertices = new List<Vertex>(meshVertices.Count);
            var rightVertices = new List<Vertex>(meshVertices.Count);

            var leftFaces = new List<int>(meshTriangles.Count);
            var rightFaces = new List<int>(meshTriangles.Count);

            var leftSubMeshesBuilder = new SubMeshDescriptorBuilder(subMeshCount, meshTriangles.Count);
            var rightSubMeshesBuilder = new SubMeshDescriptorBuilder(subMeshCount, meshTriangles.Count);

            var count = 0;

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                var submesh = submeshesList[subMeshIndex];
                var indexStart = submesh.indexStart;

                // Combine all the submeshes into one mesh
                if (submesh.topology == MeshTopology.Triangles)
                {
                    for (int iSubTri = 0; iSubTri < submesh.indexCount; iSubTri += 3)
                    {
                        var index = iSubTri + indexStart;
                        var indexA = 0;
                        try
                        {
                            indexA = meshTriangles[index];
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        var indexB = meshTriangles[index + 1];
                        var indexC = meshTriangles[index + 2];

                        var vA = meshVertices[indexA];
                        var vB = meshVertices[indexB];
                        var vC = meshVertices[indexC];

                        var aSide = utils.GetDimension(vA) < q;
                        var bSide = utils.GetDimension(vB) < q;
                        var cSide = utils.GetDimension(vC) < q;

                        if (aSide)
                        {
                            if (bSide)
                            {
                                if (cSide)
                                {
                                    // All on the left
                                    leftVertices.Add(vA);
                                    var indexALeft = leftVertices.Count - 1;
                                    leftVertices.Add(vB);
                                    var indexBLeft = leftVertices.Count - 1;
                                    leftVertices.Add(vC);
                                    var indexCLeft = leftVertices.Count - 1;

                                    leftFaces.Add(indexALeft);
                                    leftFaces.Add(indexBLeft);
                                    leftFaces.Add(indexCLeft);
                                    leftSubMeshesBuilder.AddFace(subMeshIndex, leftFaces.Count - 3, leftFaces.Count - 1);
                                }
                                else
                                {
                                    IntersectRight2D(meshVertices, utils, q, indexC, indexA, indexB, leftVertices, rightVertices,
                                        leftFaces, rightFaces, subMeshIndex, leftSubMeshesBuilder, rightSubMeshesBuilder);
                                    count++;
                                }
                            }
                            else
                            {
                                if (cSide)
                                {
                                    IntersectRight2D(meshVertices, utils, q, indexB, indexC, indexA, leftVertices, rightVertices,
                                        leftFaces, rightFaces, subMeshIndex, leftSubMeshesBuilder, rightSubMeshesBuilder);
                                    count++;
                                }
                                else
                                {
                                    IntersectLeft2D(meshVertices, utils, q, indexA, indexB, indexC, leftVertices, rightVertices,
                                        leftFaces, rightFaces, subMeshIndex, leftSubMeshesBuilder, rightSubMeshesBuilder);
                                    count++;
                                }
                            }
                        }
                        else
                        {
                            if (bSide)
                            {
                                if (cSide)
                                {
                                    IntersectRight2D(meshVertices, utils, q, indexA, indexB, indexC, leftVertices, rightVertices,
                                        leftFaces, rightFaces, subMeshIndex, leftSubMeshesBuilder, rightSubMeshesBuilder);
                                    count++;
                                }
                                else
                                {
                                    IntersectLeft2D(meshVertices, utils, q, indexB, indexC, indexA, leftVertices, rightVertices,
                                        leftFaces, rightFaces, subMeshIndex, leftSubMeshesBuilder, rightSubMeshesBuilder);
                                    count++;
                                }
                            }
                            else
                            {
                                if (cSide)
                                {
                                    IntersectLeft2D(meshVertices, utils, q, indexC, indexA, indexB, leftVertices, rightVertices,
                                        leftFaces, rightFaces, subMeshIndex, leftSubMeshesBuilder, rightSubMeshesBuilder);
                                    count++;
                                }
                                else
                                {
                                    // All on the right
                                    rightVertices.Add(vA);
                                    var indexARight = rightVertices.Count - 1;
                                    rightVertices.Add(vB);
                                    var indexBRight = rightVertices.Count - 1;
                                    rightVertices.Add(vC);
                                    var indexCRight = rightVertices.Count - 1;
                                    rightFaces.Add(indexARight);
                                    rightFaces.Add(indexBRight);
                                    rightFaces.Add(indexCRight);
                                    rightSubMeshesBuilder.AddFace(subMeshIndex, rightFaces.Count - 3, rightFaces.Count - 1);
                                }
                            }
                        }
                    }
                }

            }

            left = leftVertices;
            right = rightVertices;

            leftTriangles = leftFaces;
            rightTriangles = rightFaces;

            leftSubmeshes = leftSubMeshesBuilder.Complete();
            rightSubmeshes = rightSubMeshesBuilder.Complete();

            //_timeMeasure.StopMeasure($"Split");
            //_timeMeasure.StartMeasure($"Compiling split");

            CheckSubmeshes(leftSubmeshes, leftTriangles.Count);
            CheckSubmeshes(rightSubmeshes, rightTriangles.Count);

            //_timeMeasure.StopMeasure($"Compiling split");

            return count;
        }

        /// <summary>
        /// Validate the submesh have been rebuilt without error
        /// </summary>
        private static void CheckSubmeshes(List<SubMeshDescriptor> submeshes, int trianglesCount)
        {
            for (int i = 0; i < submeshes.Count; i++)
            {
                var submesh = submeshes[i];
                var indexStart = submesh.indexStart;
                if (submesh.topology == MeshTopology.Triangles)
                {
                    for (int iSubTri = 0; iSubTri < submesh.indexCount; iSubTri += 3)
                    {
                        var index = iSubTri + indexStart;
                        if (index + 2 > trianglesCount)
                        {
                            Debug.LogError($"wrong submesh at {i}");
                        }
                    }
                }
            }
        }

        private static void IntersectLeft2D(
            IList<Vertex> meshVertices, IVertexUtils utils, double q, int indexVL, int indexVR1, int indexVR2,
            IList<Vertex> leftVertices, IList<Vertex> rightVertices,
            ICollection<int> leftFaces, ICollection<int> rightFaces, int subMeshIndex,
            SubMeshDescriptorBuilder leftDB, SubMeshDescriptorBuilder rightDB
        )
        {
            var vL = meshVertices[indexVL];
            var vR1 = meshVertices[indexVR1];
            var vR2 = meshVertices[indexVR2];

            leftVertices.Add(vL);
            var indexVLLeft = leftVertices.Count - 1;

            if (Math.Abs(utils.GetDimension(vR1) - q) < double.Epsilon &&
                Math.Abs(utils.GetDimension(vR2) - q) < double.Epsilon)
            {
                // Right Vertices are on the line
                leftVertices.Add(vR1);
                var indexVR1Left = leftVertices.Count - 1;
                leftVertices.Add(vR2);
                var indexVR2Left = leftVertices.Count - 1;

                leftFaces.Add(indexVLLeft);
                leftFaces.Add(indexVR1Left);
                leftFaces.Add(indexVR2Left);
                return;
            }
            rightVertices.Add(vR1);
            var indexVR1Right = rightVertices.Count - 1;
            rightVertices.Add(vR2);
            var indexVR2Right = rightVertices.Count - 1;

            // a on the left, b and c on the right

            // First intersection
            var t1 = utils.CutEdge(vL, vR1, q);
            leftVertices.Add(t1);
            var indexT1Left = leftVertices.Count - 1;
            rightVertices.Add(t1);
            var indexT1Right = rightVertices.Count - 1;

            // Second intersection
            var t2 = utils.CutEdge(vL, vR2, q);
            leftVertices.Add(t2);
            var indexT2Left = leftVertices.Count - 1;
            rightVertices.Add(t2);
            var indexT2Right = rightVertices.Count - 1;

            leftFaces.Add(indexVLLeft);
            leftFaces.Add(indexT1Left);
            leftFaces.Add(indexT2Left);
            leftDB.AddFace(subMeshIndex, leftFaces.Count - 3, leftFaces.Count - 1);

            rightFaces.Add(indexT1Right);
            rightFaces.Add(indexVR1Right);
            rightFaces.Add(indexVR2Right);
            rightDB.AddFace(subMeshIndex, rightFaces.Count - 3, rightFaces.Count - 1);

            rightFaces.Add(indexT1Right);
            rightFaces.Add(indexVR2Right);
            rightFaces.Add(indexT2Right);
            rightDB.AddFace(subMeshIndex, rightFaces.Count - 3, rightFaces.Count - 1);
        }

        private static void IntersectRight2D(IList<Vertex> meshVertices, IVertexUtils utils, double q, int indexVR, int indexVL1,
            int indexVL2, IList<Vertex> leftVertices, IList<Vertex> rightVertices,
            ICollection<int> leftFaces, ICollection<int> rightFaces,
            int subMeshIndex, SubMeshDescriptorBuilder leftDB, SubMeshDescriptorBuilder rightDB
        )
        {
            var vR = meshVertices[indexVR];
            var vL1 = meshVertices[indexVL1];
            var vL2 = meshVertices[indexVL2];
            rightVertices.Add(vR);
            var indexVRRight = rightVertices.Count - 1;

            if (Math.Abs(utils.GetDimension(vL1) - q) < double.Epsilon &&
                Math.Abs(utils.GetDimension(vL2) - q) < double.Epsilon)
            {
                // Left Vertices are on the line
                rightVertices.Add(vL1);
                var indexVL1Right = rightVertices.Count - 1;
                rightVertices.Add(vL2);
                var indexVL2Right = rightVertices.Count - 1;

                rightFaces.Add(indexVRRight);
                rightFaces.Add(indexVL1Right);
                rightFaces.Add(indexVL2Right);
                rightDB.AddFace(subMeshIndex, rightFaces.Count - 3, rightFaces.Count - 1);

                return;
            }
            leftVertices.Add(vL1);
            var indexVL1Left = leftVertices.Count - 1;
            leftVertices.Add(vL2);
            var indexVL2Left = leftVertices.Count - 1;

            // a on the right, b and c on the left

            // first intersection
            var t1 = utils.CutEdge(vR, vL1, q);
            leftVertices.Add(t1);
            var indexT1Left = leftVertices.Count - 1;
            rightVertices.Add(t1);
            var indexT1Right = rightVertices.Count - 1;

            // second intersection
            var t2 = utils.CutEdge(vR, vL2, q);
            leftVertices.Add(t2);
            var indexT2Left = leftVertices.Count - 1;
            rightVertices.Add(t2);
            var indexT2Right = rightVertices.Count - 1;

            rightFaces.Add(indexVRRight);
            rightFaces.Add(indexT1Right);
            rightFaces.Add(indexT2Right);
            rightDB.AddFace(subMeshIndex, rightFaces.Count - 3, rightFaces.Count - 1);

            leftFaces.Add(indexT2Left);
            leftFaces.Add(indexVL1Left);
            leftFaces.Add(indexVL2Left);
            leftDB.AddFace(subMeshIndex, leftFaces.Count - 3, leftFaces.Count - 1);

            leftFaces.Add(indexT2Left);
            leftFaces.Add(indexT1Left);
            leftFaces.Add(indexVL1Left);
            leftDB.AddFace(subMeshIndex, leftFaces.Count - 3, leftFaces.Count - 1);
        }

        /// <summary>
        /// Compile information about the created tile.
        /// </summary>
        /// <param name="mostDetailed">Is this a leaf tile</param>
        /// <param name="chunk">The GameObject containing the cut mesh of the tile</param>
        /// <param name="textureSize">Size of the tile texture (side of square, multiple of 2)</param>
        /// <param name="textureScaling">How much the texture was downsized from the original</param>
        /// <param name="lodHighestThreshold"></param>
        /// <param name="lodLevel"></param>
        /// <param name="hausdorffDistance">Not calculated, do not used</param>
        /// <returns></returns>
        private static TileMetadata GetTileData(bool mostDetailed, GameObject chunk, int textureSize, 
            float textureScaling, int lodLevel, float hausdorffDistance)
        {
            var meshFilter = chunk.GetComponent<MeshFilter>();

            // We save the bounds of the exported model and the geometric error for later
            var bounds = meshFilter.sharedMesh.bounds;
            TileMetadata tileMetadata = new TileMetadata
            {
                geometricErrorPixelSize = mostDetailed ? 0 : CalculatePixelError(chunk, textureSize),
                geometricErrorTextureDownscale = mostDetailed || Math.Abs(textureScaling - 1f) < 0.00001f ? 0 : 1 - textureScaling,
                geometricErrorThreshold = 0,
                geometricErrorHausdorff = hausdorffDistance,
                geometricErrorTileDepth = mostDetailed ? 0 : lodLevel == 0 ? 1f : 1f / (2 * lodLevel * lodLevel),
                boundingBox = new TileBoundingBox
                {
                    min = new double3 { x = bounds.min.x, y = bounds.min.y, z = bounds.min.z },
                    max = new double3 { x = bounds.max.x, y = bounds.max.y, z = bounds.max.z }
                },
                triangleCounts = meshFilter.sharedMesh.triangles.Length / 3
            };
            return tileMetadata;
        }

        /// <summary>
        /// This calculates the geometric error for this model. This is the largest distance of error introduced by
        /// this approximation of the model over the more detailed children. This error can come from two problems
        /// 1) Distortion of the mesh due to simplifiing
        /// 2) Having a lower resolution texture
        /// The problem is the of simplifier does not give us and idea of the error produced by the simplification
        /// process We might be able to get this however for now I am only considering the error from a lower
        /// resolution texture since that is easier to calculate. The way we are cutting up the model means that the
        /// resolution of the texture is doubling each lod level. This means that the error introduced is going to
        /// be around the size of a pixel form a having a texture that is twice the resolution. This is a very
        /// approximate calculation but it seems to look about right :). To calculate the average pixel size we
        /// calculate the pixel size along each side of a triangle and fine the average.
        /// 
        /// The important thing is not to focus too much on an exact figure for this error but more on something
        /// that looks good (The higher resolution tiles get loaded when needed) Read more about this value here
        /// 
        /// https://github.com/CesiumGS/3d-tiles/tree/main/specification#geometric-error
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <param name="textureSize"></param>
        /// <returns></returns>
        private static float CalculatePixelError(GameObject model, int textureSize)
        {
            var meshFilter = model.GetComponent<MeshFilter>();

            var mesh = meshFilter.sharedMesh;

            var triangles = mesh.triangles;
            var vertices = mesh.vertices;
            var uvs = mesh.uv;

            if (uvs.Length != vertices.Length)
            {
                Debug.LogWarning($"Uvs length ({uvs.Length}) should be the same than vertices length ({vertices.Length}).");
                return 1;
            }
            if (mesh.subMeshCount != 1)
            {
                Debug.LogWarning($"mesh.subMeshCount ({mesh.subMeshCount}) should be 1.");
                return 1;
            }

            float error = 0;
            float sampleCount = 0;

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var submesh = mesh.GetSubMesh(i);
                var indexStart = submesh.indexStart;

                if (submesh.topology == MeshTopology.Triangles)
                {
                    for (int iSubTri = 0; iSubTri < submesh.indexCount; iSubTri += 3)
                    {
                        var idxStart = indexStart + iSubTri;

                        var index1 = triangles[idxStart];
                        var index2 = triangles[idxStart + 1];
                        var index3 = triangles[idxStart + 2];

                        var vert1 = vertices[index1];
                        var vert2 = vertices[index2];
                        var vert3 = vertices[index3];

                        var uv1 = uvs[index1];
                        var uv2 = uvs[index2];
                        var uv3 = uvs[index3];

                        var dist1 = Vector3.Distance(vert1, vert2);
                        var dist2 = Vector3.Distance(vert2, vert3);
                        var dist3 = Vector3.Distance(vert3, vert1);


                        // This size of a pixel is the distance between the vertex divided by the number of pixels
                        // Drawn along it
                        if (Vector2.Distance(uv1, uv2) > 0)
                        {
                            error += dist1 / (Vector2.Distance(uv1, uv2) * textureSize);
                            sampleCount++;
                        }
                        if (Vector2.Distance(uv2, uv3) > 0)
                        {
                            error += dist2 / (Vector2.Distance(uv2, uv3) * textureSize);
                            sampleCount++;
                        }
                        if (Vector2.Distance(uv3, uv1) > 0)
                        {
                            error += dist3 / (Vector2.Distance(uv3, uv1) * textureSize);
                            sampleCount++;
                        }
                    }
                }
            }

            return (error / sampleCount);
        }

        /// <summary>
        /// Create a game object that renders a mesh with an unlit texture
        /// </summary>
        /// <param name="parentGameObject">parent for the new gameobject</param>
        /// <param name="newMesh">mesh to render</param>
        /// <param name="texture">Texture to apply to mesh using an Unlit material</param>
        /// <param name="goName">Name for the new gameobject</param>
        /// <returns></returns>
        public static GameObject CreateObject(GameObject parentGameObject, MinMesh newMesh, Texture2D texture, string goName)
        {

            if (_internalPool == null)
            {
                _internalPool = new MeshGameObjectPool(parentGameObject.transform);
            }
            var mf = _internalPool.Get();
            var go = mf.gameObject;
            go.name = goName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.transform.SetParent(parentGameObject.transform, false);

            var m = newMesh.ToMesh();
            m.Optimize();
            m.RecalculateBounds();
            mf.sharedMesh = m;
            var mr = go.GetComponent<MeshRenderer>();

            var material = new Material(Shader.Find("Unlit/Texture"));
            material.mainTexture = texture;
            mr.materials = new[]
            {
                material
            };

            return go;
        }

        /// <summary>
        /// Take all the submeshes and associated textures: merge the submeshes into one and repack a texture to take only the required pixels.
        /// </summary>
        /// <param name="textures"></param>
        /// <param name="mesh"></param>
        /// <param name="targetTextureSize"></param>
        /// <returns>Merged mesh, packed texture, and textureScaling ]0;1] = how much the texture was reduce.</returns>
        private static (MinMesh, Texture2D, double textureScaling) MergeSubmeshes(Texture[] textures, 
            Vector2[] textureScales, Vector2[] textureOffsets, MinMesh mesh, int targetTextureSize)
        {
            IList<Vertex> vertices = mesh.Vertices;
            IList<int> triangles = mesh.Triangles;
            int[] submeshIndexes = new int[vertices.Count];

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

        /// <summary>
        /// Perform the packing operation to optimize space in the target texture.
        /// </summary>
        /// <param name="clusteredUvs"></param>
        /// <param name="ratios">Ratios of Source textures size relative to target output size. Used to calculate UVRect weighted size.</param>
        /// <param name="scale">Output: </param>
        /// <returns>List<UVRect> that is the same Length as the input one but packed in a rectangular shape</returns>
        private static (List<UVRect>, double scale) PackClustersIntoRectangles(List<UVRect> clusteredUvs, Vector2[] ratios)
        {
            int bestChunkWidth;

            // Since the target texture needs to be square we need to run the UVPacking algorithm several times
            // till we know how many squares to put on the top to give us the most square texture 
            var packedRects = new List<UVRect>();
            double aspectRatio = 0;
            bestChunkWidth = 1;

            for (int j = 1; j <= clusteredUvs.Count; j++)
            {
                var (size, _) = PackRectangles(clusteredUvs, ratios, j, ref packedRects);
                var newAspect = size.x / size.y;
                if (math.abs(newAspect - 1) < math.abs(aspectRatio - 1))
                {
                    aspectRatio = newAspect;
                    bestChunkWidth = j;
                }
            }

            // When we have the optimal number of squares on the top level do the real packing
            var (_, scale) = PackRectangles(clusteredUvs, ratios, bestChunkWidth, ref packedRects);
            return (packedRects, scale);
        }

        /// <summary>
        /// Read input pixels from textures and srcRects to create a new texture where is srcRect is output at destRect location.
        /// If the scale is != 1, use a bilinear filter copy to get a better approximation.
        /// If the scale == 1, use a point filter copy to get an accurate output.
        /// </summary>
        /// <param name="textures">input textures</param>
        /// <param name="srcRects">UVRect targeting input textures</param>
        /// <param name="dstRects">UVRect targeting output texture expected positions</param>
        /// <param name="targetTextureSize">Size of the new texture</param>
        /// <param name="scale">How much the pixels are scaled from the original to the target.</param>
        /// <returns></returns>
        private static Texture2D CreatePackedTexture(Texture[] textures, Vector2[] textureScales, Vector2[] textureOffsets, 
            List<UVRect> srcRects, [CanBeNull] List<UVRect> dstRects, int targetTextureSize, double scale)
        {
            int width = targetTextureSize;
            int height = targetTextureSize;

            // check if we can reduce the texture by half and still fit all the dst rects
            // it is the case where the dstRects doesn't need all the space and are packs in the origin corner
            var wasResized = false;
            do
            {
                wasResized = false;
                var maxX = dstRects.Max(r => r.max.x);
                var maxY = dstRects.Max(r => r.max.y);
                var max = Math.Max(maxX, maxY);
                if (max <= 0.5f)
                {
                    width /= 2;
                    height /= 2;
                    for (int iRect = 0; iRect < dstRects.Count; iRect++)
                    {
                        var r = dstRects[iRect];
                        r.min *= 2;
                        r.max *= 2;
                        dstRects[iRect] = r;
                    }
                    wasResized = true;
                }
            } while (wasResized);

            var rt = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );
            rt.enableRandomWrite = true;
            var prt = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prt;

            for (int i = 0; i < srcRects.Count && i < dstRects.Count; i++)
            {
                var sourceRect = srcRects[i];
                var texture = textures[sourceRect.submesh];
                var textureScale = textureScales[sourceRect.submesh];
                var textureOffset = textureOffsets[sourceRect.submesh];
                
                var sourceTextureTransform = new Vector4(
                    textureScale.x,
                    textureScale.y,
                    textureOffset.x,
                    textureOffset.y
                );
                    
                if (texture != null)
                {
                    var destinationRect = dstRects[i];

                    int dstX = (int) Math.Floor(width * destinationRect.min.x);
                    int dstY = (int) Math.Floor(height * destinationRect.min.y);
                    int dstWidth = (int) Math.Max(1, Math.Round(width * destinationRect.Width));
                    int dstHeight = (int) Math.Max(1, Math.Round(height * destinationRect.Height));

                    // If the scale is 1, use point filtering to copy the texture so that the result exactly match the source.
                    // Using bilinear copying for the final copy results in blurred and slightly offseted texture.
                    if (Math.Abs(scale - 1) < Double.Epsilon)
                    {
                        CopyRegionShader.DispatchPoint(
                            texture,
                            sourceTextureTransform,
                            (float) sourceRect.min.x,
                            (float) sourceRect.min.y,
                            (float) sourceRect.Width,
                            (float) sourceRect.Height,
                            rt,
                            dstX,
                            dstY,
                            dstWidth,
                            dstHeight
                        );
                    }
                    // If the scale is different than 1, use bilinear filtering so that the result approximate better the source.
                    // This can't be correct by definition and the result is often mostly better when using bilinear.
                    else
                    {
                        CopyRegionShader.DispatchBilinear(
                            texture,
                            sourceTextureTransform,
                            (float) sourceRect.min.x,
                            (float) sourceRect.min.y,
                            (float) sourceRect.Width,
                            (float) sourceRect.Height,
                            rt,
                            dstX,
                            dstY,
                            dstWidth,
                            dstHeight
                        );
                    }
                }
            }

            var output = _texture2DPool.Get(width);

            Graphics.CopyTexture(rt, output);
            RenderTexture.ReleaseTemporary(rt);

            return output;
        }


        /// <summary>
        /// This creates a UVPacking based on the skyline bottom left algorithm. Rather than packing to a fixed sized square you specify the number of
        /// squares in the top row of the texture and then this is used for the texture width. This lets do a UVmapping
        /// where all the coordinates are between 0 and 1 so we can resize the mapping onto any texture size 
        /// </summary>
        /// <param name="sourceChunks">List of squared to pack</param>
        /// <param name="ratios">Ratios of Source textures size relative to target output size. Used to calculate UVRect weighted size.</param>
        /// <param name="slotsOfFirstRow">Number of squares in the top row</param>
        /// <param name="destinations">Final positions of the squares</param>
        /// <returns>The size of the packing scaled so all coordinates are between 0 and 1; and the scale applied ]0;1].</returns>
        private static (double2, double scale) PackRectangles(List<UVRect> sourceChunks, Vector2[] ratios, int slotsOfFirstRow,
            ref List<UVRect> destinations)
        {
            destinations.Clear();

            // Calculate the width and height of the first row
            double firstLineX = 0;
            double y = 0;
            for (int i = 0; i < slotsOfFirstRow; i++)
            {
                var currentChunk = sourceChunks[i];
               
                double chunkWidth = currentChunk.WeightedWidth(ratios);
                double chunkHeight = currentChunk.WeightedHeight(ratios);

                destinations.Add(new UVRect(
                    0,
                    new double2(firstLineX, 0),
                    new double2(firstLineX + chunkWidth, chunkHeight)
                ));

                firstLineX += chunkWidth;
                y = math.max(y, chunkHeight);
            }


            // Now we know the width of the target texture add in the rest of the row creating a new row if the 
            // square will make the current row wider than the top row
            double currentLineWidth = 0;
            double currentLineHeight = 0;
            for (int i = slotsOfFirstRow; i < sourceChunks.Count; i++)
            {
                var currentChunk = sourceChunks[i];
                double chunkWidth = currentChunk.WeightedWidth(ratios);
                double chunkHeight = currentChunk.WeightedHeight(ratios);

                // first item of the new row
                if (currentLineWidth + chunkWidth > firstLineX)
                {
                    y += currentLineHeight;

                    destinations.Add(new UVRect(
                        0,
                        new double2(0, y),
                        new double2(chunkWidth, y + chunkHeight)
                    ));

                    currentLineWidth = chunkWidth;
                    currentLineHeight = chunkHeight;
                }
                else
                {
                    // next items
                    destinations.Add(new UVRect(
                        0,
                        new double2(currentLineWidth, y),
                        new double2(currentLineWidth + chunkWidth, y + chunkHeight)
                    ));

                    currentLineWidth += chunkWidth;
                    currentLineHeight = math.max(currentLineHeight, chunkHeight);
                }
            }

            y += currentLineHeight;

            // Scale the packing so all coordinates are between 0 and 1
            double maxUv = math.max(firstLineX, y);
            var scale = Math.Clamp(1f / maxUv, 0, 1);
            for (int i = 0; i < destinations.Count; i++)
            {
                destinations[i] = new UVRect(
                    0,
                    destinations[i].min * scale,
                    destinations[i].max * scale
                );
            }

            // Return the width and height of the packing scaled between 0 and 1
            return (
                new double2(firstLineX * scale, y * scale),
                scale
                );
        }

        /// <summary>
        /// This takes a list of rectangles and if two overlap then combine them into a bigger rectangle. Continue
        /// to do this until there are no overlapping rectangles. 
        /// </summary>
        /// <param name="sourceUvList">List of rectangles to be clustered</param>
        /// <returns>List of clustered rectangles</returns>
        private static List<UVRect> ClusterUvRects(IEnumerable<UVRect> sourceUvList)
        {
            var inputList = new List<UVRect>(sourceUvList);
            var clusteredList = new List<UVRect>();
            if (inputList.Count > 0)
            {
                while (true)
                {
                    bool isMerging = false;

                    for (var srcIdx = 0; srcIdx < inputList.Count; srcIdx++)
                    {
                        var rectToAdd = inputList[srcIdx];

                        // Find if this rectangle overlaps with any of the previously added rectangles
                        // If it overlaps then increase the size of the rectangle already in the list
                        bool found = false;
                        for (int dstIdx = 0; dstIdx < clusteredList.Count; dstIdx++)
                        {
                            var mergeRect = clusteredList[dstIdx];

                            if (rectToAdd.IsOverlapping(mergeRect) && rectToAdd.submesh == mergeRect.submesh)
                            {
                                clusteredList[dstIdx] = rectToAdd.Union(mergeRect);
                                isMerging = true;
                                found = true;
                                break;
                            }
                        }

                        // If we have not found any overlapping rectangles then add this one to the list 
                        if (!found)
                        {
                            clusteredList.Add(rectToAdd);
                        }
                    }

                    // Keep repeating the merging algorithm until no changes have been made
                    // We need to do this as when we enlarge a rectangle it might then start overlapping with another
                    // rectangle already added. So we need to keep repeating the clustering process until no changes
                    // have been made
                    if (isMerging)
                    {
                        inputList.Clear();
                        inputList.AddRange(clusteredList);
                        clusteredList.Clear();
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return clusteredList;
        }

        /// <summary>
        /// Currently returns only the middle.
        /// TODO: test a few values to avoid very unbalanced split with tiles almost empty.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Vector3 GetSplitPoint(MinMesh mesh)
        {
            var bounds = mesh.CalculateBounds();

            // ensure the cubes remains mostly regular
            var maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var xVal = bounds.size.x < maxDimension * 0.5f ? bounds.min.x : bounds.center.x;
            var yVal = bounds.size.y < maxDimension * 0.5f ? bounds.min.y : bounds.center.y;
            var zVal = bounds.size.z < maxDimension * 0.5f ? bounds.min.z : bounds.center.z;
            return new Vector3(xVal, yVal, zVal);
        }
    }
}