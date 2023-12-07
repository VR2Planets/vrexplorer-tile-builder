using System;
using System.Collections.Generic;
using MeshCutting;
using Model;
using UnityEngine;

public class Cutter : MonoBehaviour
{
    /*
    public async void Start()
    {
        Tile tile = null;

        Renderer meshRenderer;
        Matrix4x4? localToWorldMatrix;

        // Create a parent for LOD 0
        var parentGameObject = new GameObject("LOD0");

        var chunksToExport = new Dictionary<string, ExportEntry>();
        tile = await MeshCuttingHelper.ExportLODAndSplitRecursive(parentGameObject,
            localToWorldMatrix.Value,
            MinMesh.FromMesh(mesh),
            meshRenderer.materials, MeshCuttingHelper.GetSplitPoint,
            TargetTriangles,
            MaxLOD,
            Options,
            TargetTextureSize,
            Application.dataPath,
            chunksToExport,
            null,
            "model",
            0,
            _timeMeasure
        );
        
        try
        {
            await GenerateTilesetHelper.GenerateAndExportTileset(outputPath, tile, mesh.bounds);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }        
    }
    */
}
