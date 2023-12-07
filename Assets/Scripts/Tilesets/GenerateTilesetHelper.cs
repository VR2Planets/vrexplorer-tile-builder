using System;
using System.IO;
using System.Threading.Tasks;
using MeshCutting;
using Model;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEngine;

public static class GenerateTilesetHelper
{
    /// <summary>
    /// This generates the tileset that defines the 3DTiles more details at https://github.com/CesiumGS/3d-tiles.
    /// </summary>
    public static async Task GenerateAndExportTileset(string exportPath, Tile tile, Bounds bounds)
    {
        // Calculate the geometricError for this dataset. This is the error interduced by not rendering this dataset.
        // It is the same as the size of the mesh
        var boundsMax = new double3(bounds.max.x, bounds.max.y, bounds.max.z);
        var boundsMin = new double3(bounds.min.x, bounds.min.y, bounds.min.z);
        var modelSize = boundsMax - boundsMin;
        var maxWidth = Math.Max(Math.Max(modelSize.x, modelSize.y), modelSize.z);
        var calculatedMax = boundsMin + new double3(maxWidth, maxWidth, maxWidth);
        var totalBounds = new TileBoundingBox
        {
            min = boundsMin,
            max = calculatedMax
        };
        var size = (calculatedMax - boundsMin);
        var dataSetGeometricError = math.length(size);

        var tileset = new Tileset()
        {
            asset = new Asset()
            {
                version = "1.0"
            },
            geometricError = dataSetGeometricError,
            root = tile,
        };

        var serializer = new JsonSerializerSettings();
        serializer.Converters.Add(new BoxConverter());
        var json = JsonConvert.SerializeObject(tileset, serializer);
        var modelTileset = Path.Combine(exportPath, @"tileset.json");

        await File.WriteAllTextAsync(modelTileset, json);
    }

    // 3DTiles uses a Z up right handed coordinate system unity uses y up left handed coordinate system so we need to
    // convert between the two systems
    public static double4x4 UnityMatrixTo3DTiles(Matrix4x4 source)
    {
        double4x4 yupLeftHandedToZupRightHanded = new double4x4(
            1, 0, 0, 0,
            0, 0, 1, 0,
            0, -1, 0, 0,
            0, 0, 0, 1
        );
        double4x4 unityMat = math.mul(yupLeftHandedToZupRightHanded, ((float4x4) source));
        return unityMat;
    }
}