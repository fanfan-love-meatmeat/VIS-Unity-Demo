// SpatialOptimizer.cs - Spatial hash index
// Build a spatial hash index to accelerate center-proximity checks.

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Accelerate candidate retrieval for the center-proximity criterion using spatial hashing.
/// </summary>
public class SpatialOptimizer
{
    #region Properties
    public Dictionary<Vector3Int, List<int>> CropHashIndices { get; private set; } = new();
    public float CellSize { get; private set; }
    #endregion

    #region Initialization
    /// <summary>
    /// Initialize the spatial hash index.
    /// </summary>
    public void Initialize(VoxelData voxelData, float voxelSize)
    {
        OptimizeCellSize(voxelData, voxelSize);
        BuildCropHashIndices(voxelData.CropLocalCenters);
    }

    /// <summary>
    /// Choose the spatial-hash cell edge length from the crop-center density.
    /// </summary>
    private void OptimizeCellSize(VoxelData voxelData, float voxelSize)
    {
        if (voxelData.CropLocalCenters.Count == 0) return;

        // Estimate the spatial distribution of crop voxel centers.
        var bounds = voxelData.CalculateBounds(voxelData.CropLocalCenters);
        float volume = bounds.x * bounds.y * bounds.z;
        float density = voxelData.CropLocalCenters.Count / volume;

        // Set the target number of crop centers per hash cell.
        float targetVoxelsPerCell = 25f;
        float idealCellVolume = targetVoxelsPerCell / density;
        CellSize = Mathf.Pow(idealCellVolume, 1f / 3f);

        // Constrain the hash-cell edge length.
        CellSize = Mathf.Clamp(CellSize, voxelSize * 2f, voxelSize * 10f);
    }

    /// <summary>
    /// Build the spatial hash index of crop voxel centers.
    /// </summary>
    private void BuildCropHashIndices(List<Vector3> cropLocalCenters)
    {
        CropHashIndices.Clear();

        // Assign each crop-center index to its hash cell.
        for (int i = 0; i < cropLocalCenters.Count; i++)
        {
            Vector3Int key = WorldToHash(cropLocalCenters[i]);

            // Create the cell list if it does not exist.
            if (!CropHashIndices.TryGetValue(key, out var list))
            {
                list = new List<int>();
                CropHashIndices[key] = list;
            }

            // Add the crop-center index to its cell list.
            list.Add(i);
        }
    }
    #endregion

    #region Helper methods
    /// <summary>
    /// Map a coordinate in the spatial hash frame to an integer cell index.
    /// </summary>
    public Vector3Int WorldToHash(Vector3 localPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(localPos.x / CellSize),
            Mathf.FloorToInt(localPos.y / CellSize),
            Mathf.FloorToInt(localPos.z / CellSize)
        );
    }
    #endregion
}
