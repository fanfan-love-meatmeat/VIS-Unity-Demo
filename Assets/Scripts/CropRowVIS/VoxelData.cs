using System.Collections.Generic;
using UnityEngine;

public class VoxelData
{
    public List<Vector3> CropLocalCenters { get; private set; } = new List<Vector3>();
    public List<Vector3> TrackLocalCenters { get; private set; } = new List<Vector3>();
    public Bounds CropBounds { get; private set; }

    // Synthetic demo input only; retain the original CSV loaders below.
    // Binary layout: track count, track xyz floats, crop count, crop xyz floats.
    public void LoadGeneratedCenters(TextAsset data, float voxelSize)
    {
        if (data == null) throw new System.ArgumentNullException(nameof(data));
        TrackLocalCenters.Clear(); CropLocalCenters.Clear();
        using (var stream = new System.IO.MemoryStream(data.bytes))
        using (var reader = new System.IO.BinaryReader(stream))
        {
            ReadCenters(reader, TrackLocalCenters);
            ReadCenters(reader, CropLocalCenters);
            if (stream.Position != stream.Length) throw new System.IO.InvalidDataException("Unexpected trailing center data.");
        }
        CalculateCropBounds(voxelSize);
    }

    private static void ReadCenters(System.IO.BinaryReader reader, List<Vector3> centers)
    {
        int count = reader.ReadInt32();
        if (count < 0 || (long)count * 12 > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new System.IO.InvalidDataException("Invalid center count.");
        centers.Capacity = System.Math.Max(centers.Capacity, count);
        for (int i = 0; i < count; i++)
            centers.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
    }

    public void LoadCropVoxels(TextAsset csv, float voxelSize)
    {
        CropLocalCenters.Clear();

        if (csv == null)
        {
            Debug.LogWarning("[VoxelData] Crop voxel CSV is null.");
            return;
        }

        string[] lines = csv.text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            if (line.StartsWith("x") || line.StartsWith("y") || line.StartsWith("z")) continue;

            string[] tokens = line.Split(',');
            if (tokens.Length < 3) continue;

            if (float.TryParse(tokens[0], out float x) &&
                float.TryParse(tokens[1], out float y) &&
                float.TryParse(tokens[2], out float z))
            {
                CropLocalCenters.Add(new Vector3(x, y, z));
            }
        }

        CalculateCropBounds(voxelSize);
    }

    public void LoadTrackVoxels(TextAsset csv)
    {
        TrackLocalCenters.Clear();

        if (csv == null)
        {
            Debug.LogWarning("[VoxelData] Track voxel CSV is null.");
            return;
        }

        string[] lines = csv.text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            if (line.StartsWith("x") || line.StartsWith("y") || line.StartsWith("z")) continue;

            string[] tokens = line.Split(',');
            if (tokens.Length < 3) continue;

            if (float.TryParse(tokens[0], out float x) &&
                float.TryParse(tokens[1], out float y) &&
                float.TryParse(tokens[2], out float z))
            {
                TrackLocalCenters.Add(new Vector3(x, y, z));
            }
        }
    }

    private void CalculateCropBounds(float voxelSize)
    {
        if (CropLocalCenters.Count == 0)
        {
            CropBounds = new Bounds();
            return;
        }

        Vector3 min = CropLocalCenters[0];
        Vector3 max = CropLocalCenters[0];

        foreach (var center in CropLocalCenters)
        {
            min = Vector3.Min(min, center - Vector3.one * voxelSize * 0.5f);
            max = Vector3.Max(max, center + Vector3.one * voxelSize * 0.5f);
        }

        CropBounds = new Bounds((min + max) * 0.5f, max - min);
    }

    /// <summary>
    /// Calculate the bounding-box size for the crop-center density estimate in SpatialOptimizer.
    /// </summary>
    public Vector3 CalculateBounds(List<Vector3> positions)
    {
        if (positions == null || positions.Count == 0)
            return Vector3.one;

        Vector3 min = positions[0];
        Vector3 max = positions[0];

        foreach (var pos in positions)
        {
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        return max - min;
    }
}
