using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
//using Draco;
//using Draco.Encoder;
//using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeshExporter
{
    public static class SimpleMeshExporter
    {
        public static Task<bool> Export(string modelName, Mesh mesh, Texture texture, string modelPath)
        {
            /*
            var flippedMesh = FlipMesh(mesh);
            
            flippedMesh.RecalculateBounds();
            flippedMesh.RecalculateNormals();
            flippedMesh.RecalculateTangents();
                
            var encodedResult = DracoEncoder.EncodeMesh(flippedMesh)[0];
            var meshBounds = flippedMesh.bounds;
            
            GameObject.DestroyImmediate(flippedMesh);
            
            var destRenderTexture = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB,
                1,
                RenderTextureMemoryless.Depth
            );
            
            Graphics.Blit(texture, destRenderTexture);
            
            var exportTexture = new Texture2D(
                texture.width,
                texture.height,
                TextureFormat.ARGB32,
                1,
                false,
                true
            );
            exportTexture.ReadPixels(new Rect(0, 0, destRenderTexture.width, destRenderTexture.height), 0, 0);
            RenderTexture.ReleaseTemporary(destRenderTexture);
            exportTexture.Apply();
            
            var textureData = exportTexture.EncodeToJPG(60);

            var dataLength = encodedResult.data.Length + textureData.Length;

            var maxSb = new StringBuilder();
            maxSb.Append(meshBounds.max.x.ToString(CultureInfo.InvariantCulture));
            maxSb.Append(",");
            maxSb.Append(meshBounds.max.y.ToString(CultureInfo.InvariantCulture));
            maxSb.Append(",");
            maxSb.Append(meshBounds.max.z.ToString(CultureInfo.InvariantCulture));
            
            var minSb = new StringBuilder();
            minSb.Append(meshBounds.min.x.ToString(CultureInfo.InvariantCulture));
            minSb.Append(",");
            minSb.Append(meshBounds.min.y.ToString(CultureInfo.InvariantCulture));
            minSb.Append(",");
            minSb.Append(meshBounds.min.z.ToString(CultureInfo.InvariantCulture));
            
            var json =
                $"{{\"asset\":{{\"version\":\"2.0\",\"generator\":\"Vr2Planets Mesh Cutter\"}}," +
                $"\"nodes\":[{{\"name\":\"{modelName}\",\"mesh\":0}}]," +
                $"\"extensionsRequired\":[\"KHR_materials_unlit\",\"KHR_draco_mesh_compression\"]," +
                $"\"extensionsUsed\":[\"KHR_materials_unlit\",\"KHR_draco_mesh_compression\"]," +
                $"\"buffers\":[{{\"byteLength\":{dataLength}}}]," +
                $"\"bufferViews\":[" +
                    $"{{\"buffer\":0,\"byteOffset\":0,\"byteLength\":{encodedResult.data.Length}}}," +
                    $"{{\"buffer\":0,\"byteOffset\":{encodedResult.data.Length},\"byteLength\":{textureData.Length}}}" +
                $"]," +
                $"\"accessors\":[" +
                    $"{{\"componentType\":5125,\"count\":{encodedResult.indexCount},\"type\":\"SCALAR\"}}," +    
                    $"{{\"componentType\":5126,\"count\":{encodedResult.vertexCount},\"type\":\"VEC3\",\"max\":[{maxSb}],\"min\":[{minSb}]}}," +
                    $"{{\"componentType\":5126,\"count\":{encodedResult.vertexCount},\"type\":\"VEC2\"}}" +
                $"]," +
                $"\"images\":[{{\"name\":\"texture.jpeg\",\"mimeType\":\"image/jpeg\",\"bufferView\":1}}]," +
                $"\"materials\":[{{\"name\":\"Unlit/Texture (Instance)\",\"pbrMetallicRoughness\":{{\"metallicFactor\":0,\"baseColorTexture\":{{\"index\":0}}}},\"extensions\":{{\"KHR_materials_unlit\":{{}}}}}}]," +
                $"\"meshes\": [{{\"primitives\": [{{\"attributes\": {{\"POSITION\": 1,\"TEXCOORD_0\": 2}},\"indices\": 0,\"material\": 0,\"mode\": 4,\"extensions\": {{\"KHR_draco_mesh_compression\": {{\"bufferView\": 0,\"attributes\": {{\"POSITION\": 0,\"TEXCOORD_0\": 1}}}}}}}}]}}]," +
                $"\"samplers\":[{{\"wrapS\":{GLTFWrappingMode(texture.wrapModeU)},\"wrapT\":{GLTFWrappingMode(texture.wrapModeV)}}}]," +
                $"\"scene\":0," +
                $"\"scenes\":[{{\"nodes\":[0]}}]," +
                $"\"textures\":[{{\"source\":0,\"sampler\":0}}]}}";

            var jsonData = Encoding.UTF8.GetBytes(json);
            
            using (var fs = new FileStream(modelPath, FileMode.CreateNew))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.UTF8.GetBytes("glTF"));
                bw.Write(2);
                bw.Write(Aligned(jsonData.Length) + Aligned(dataLength));
                
                bw.Write(Aligned(jsonData.Length));
                bw.Write(0x4E4F534Au);
                bw.Write(jsonData);
                for (int i = 0; i < Padding(jsonData.Length); i++)
                {
                    bw.Write(' ');
                }
                
                bw.Write(Aligned(dataLength));
                bw.Write(0x004E4942);
                for (int i = 0; i < encodedResult.data.Length; i++)
                {
                    bw.Write(encodedResult.data[i]);
                }
                
                bw.Write(textureData);
                
                for (int i = 0; i < Padding(dataLength); i++)
                {
                    bw.Write((byte) 0);
                }
            }

            encodedResult.data.Dispose();
            */
            return Task.FromResult(true);
            
        }

        /*
        public static async Task<Mesh> Copy(Mesh mesh)
        {
            var encodedResult = DracoEncoder.EncodeMesh(mesh)[0];
            
            //var draco = new DracoMeshLoader(false);
            var draco = new DracoMeshLoader();
            
            return await draco.ConvertDracoMeshToUnity(
                encodedResult.data,
                forceUnityLayout: true
                );
        }
        */

        private static int Padding(int len)
        {
            return (4 - (len % 4)) % 4;
        }
        
        private static int Aligned(int len)
        {
            return len + Padding(len);
        }
        
        private static int GLTFWrappingMode(TextureWrapMode textureWrapMode)
        {
            return textureWrapMode switch
            {
                TextureWrapMode.Repeat => 10497,
                TextureWrapMode.Clamp => 33071,
                TextureWrapMode.Mirror => 33648,
                TextureWrapMode.MirrorOnce => 33648
            };
        }
        
        private static Mesh FlipMesh(Mesh mesh)
        {
            var uvs = new List<Vector2>();
            mesh.GetUVs(0, uvs);

            for (int i = 0; i < uvs.Count; i++)
            {
                var uv = uvs[i];
                uvs[i] = new Vector2(uv.x, 1 - uv.y);
            }
            
            var indicies = new List<int>();
            mesh.GetIndices(indicies, 0);
            for (int i = 0; i < indicies.Count; i+=3)
            {
                (indicies[i + 1], indicies[i + 2]) = (indicies[i + 2], indicies[i + 1]);
            }
            
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                vertices[i] = new Vector3(-v.x, v.y, v.z);
            }
            
            var flippedMesh = new Mesh
            {
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices.ToArray(),
                triangles = indicies.ToArray(),
                uv = uvs.ToArray(),
            };

            return flippedMesh;
        }
    }
}