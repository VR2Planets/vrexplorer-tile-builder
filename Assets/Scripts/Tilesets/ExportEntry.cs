using UnityEngine;

/// <summary>
/// Regroup all the information required to export a tile: name, metadata, 3D model reference.
/// </summary>
public struct ExportEntry
{
    public readonly string Name;
    public readonly TileMetadata TileMetadata;
    public readonly GameObject Model;

    public ExportEntry(string name, TileMetadata tileMetadata, GameObject model)
    {
        Name = name;
        TileMetadata = tileMetadata;
        Model = model;
    }
}