using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Export;
using JetBrains.Annotations;
using MeshExporter;
using Newtonsoft.Json;
using UnityEngine;

public static class TilesExporter
{
    public static async Task ExportChunks(Dictionary<string, ExportEntry> chunksToExport, string exportFolderPath, Action<string> logMessage,
        [CanBeNull] Action<float, string> onProgress)
    {
        var allSucceeded = true;
        var i = 0;
        foreach (var c in chunksToExport.Values)
        {
            var p = (float) i++ / chunksToExport.Count;
            var success = await ExportChunkUnit(
                c.Model,
                Path.Combine(exportFolderPath, $"{c.Name}.glb"),
                Path.Combine(exportFolderPath, $"{c.Name}.json"),
                c.TileMetadata
            );
            onProgress?.Invoke(p, $"Exporting chunk ({i}/{chunksToExport.Count})");
            if (!success)
            {
                logMessage($"Failed to export chunk {c.Name}");
                allSucceeded = false;
            }
        }
        if (allSucceeded)
        {
            logMessage($"Successfully exported all tiles chunks as glb in {exportFolderPath}");
        }
    }

    /// <summary>
    /// Save the model to a glb, and the tile data in a json.
    /// </summary>
    /// <param name="chunk">The gameobject to export</param>
    /// <param name="modelPath">The path to the glb model</param>
    /// <param name="modelJson">Path to a json file containing model params</param>
    /// <param name="tileMetadata"></param>
    public static async Task<bool> ExportChunkUnit(GameObject chunk, string modelPath, string modelJson, TileMetadata tileMetadata)
    {
        var dir = Path.GetDirectoryName(modelPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        // Save a json file with extra model properties useful when generating the 3DTiles tileset.json
        // The tileset generation is not generated at the same time as the model exported so that we can regenerate 
        // the tileset when there are bugs without having to recalculate all the models.
        var json = JsonConvert.SerializeObject(tileMetadata);
        await File.WriteAllTextAsync(modelJson, json);
        
        var meshFilter = chunk.GetComponent<MeshFilter>();
        var meshRenderer = chunk.GetComponent<MeshRenderer>();

        if (meshFilter != null && meshRenderer != null)
        {
            var mesh = meshFilter.sharedMesh;
            var material = meshRenderer.material;

            if (material != null)
            {
                var texture = meshRenderer.material.mainTexture as Texture2D;

                if (texture != null)
                {
                    var success = await SimpleMeshExporter.Export(
                        chunk.name,
                        mesh,
                        texture,
                        modelPath
                    );
                    
                    if (success)
                    {
                        return true;
                    }
                }
            }
        }
        
        return await ExportChunkUnitComplex(chunk, modelPath);
    }
    
    /// <summary>
    /// Save the model to a glb, and the tile data in a json.
    /// </summary>
    /// <param name="chunk">The gameobject to export</param>
    /// <param name="modelPath">The path to the glb model</param>
    /// <param name="modelJson">Path to a json file containing model params</param>
    /// <param name="tileMetadata"></param>
    public static async Task<bool> ExportChunkUnitComplex(GameObject chunk, string modelPath)
    {
        // Export the model to a GLB
        var exportSettings = new ExportSettings
        {
            Format = GltfFormat.Binary,
            ImageDestination = ImageDestination.MainBuffer,
            ComponentMask = ComponentType.Mesh,
            FileConflictResolution = FileConflictResolution.Abort,
        };

        var gameObjectExportSettings = new GameObjectExportSettings
        {
            DisabledComponents = false,
            OnlyActiveInHierarchy = true,
        };

        var export = new GameObjectExport(
            exportSettings,
            gameObjectExportSettings
        );

        var mr = chunk.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.enabled = true;
        }
        
        // Hide the children
        for (int i = 0; i < chunk.transform.childCount; i++)
        {
            var child = chunk.transform.GetChild(i);
            child.gameObject.SetActive(false);
        }
        
        export.AddScene(new[]
        {
            chunk
        });
        
        bool success = false;
        try
        {
            success = await export.SaveToFileAndDispose(modelPath);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }

        if (mr != null)
        {
            mr.enabled = false;
        }
        
        // Show the children
        for (int i = 0; i < chunk.transform.childCount; i++)
        {
            var child = chunk.transform.GetChild(i);
            child.gameObject.SetActive(true);
        }
        
        return success;        
    }
    public static string GetExportName(string name, int lodLevel) => $"lod{lodLevel}\\{name}";
}