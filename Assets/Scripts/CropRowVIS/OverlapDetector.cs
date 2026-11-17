// OverlapDetector.cs - Center-proximity detector (legacy class name).
// Evaluate track-to-crop center proximity and track unique qualifying crop indices.

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Evaluate the center-proximity criterion between track and crop centers.
/// </summary>
public class OverlapDetector
{
    #region Properties
    public bool[] LeftOverlapFlags { get; private set; }
    public bool[] RightOverlapFlags { get; private set; }

    // --- Episode-level aggregation ---
    // Use a HashSet to deduplicate qualifying crop-center indices.
    // Store every crop-center index that qualifies at any update in this episode.
    public HashSet<int> DamagedCropVoxelIndices { get; private set; } = new HashSet<int>();
    // --- End of source update. ---

    #endregion

    #region Initialization
    /// <summary>
    /// Initialize the per-update qualifying track-voxel flag arrays.
    /// </summary>
    public void Initialize(int trackVoxelCount)
    {
        LeftOverlapFlags = new bool[trackVoxelCount];
        RightOverlapFlags = new bool[trackVoxelCount];
    }

    // --- Episode reset ---
    /// <summary>
    /// Clear the episode-level set of unique qualifying crop-center indices at episode start.
    /// </summary>
    public void ResetDamagedVoxelTracking()
    {
        DamagedCropVoxelIndices.Clear();
    }
    // --- End of source update. ---
    #endregion

    #region Center-proximity evaluation
    /// <summary>
    /// Evaluate center proximity for both tracks.
    /// </summary>
    public void ComputeOverlaps(VoxelData voxelData, SpatialOptimizer optimizer,
        Transform leftTrackTransform, Transform rightTrackTransform,
        Transform cropRowTransform, float voxelSize)
    {
        if (cropRowTransform == null) return;

        // Evaluate the left-track center-proximity flags.
        if (leftTrackTransform != null)
        {
            ComputeOverlaps(voxelData, optimizer, leftTrackTransform, cropRowTransform,
                LeftOverlapFlags, voxelSize);
        }

        // Evaluate the right-track center-proximity flags.
        if (rightTrackTransform != null)
        {
            ComputeOverlaps(voxelData, optimizer, rightTrackTransform, cropRowTransform,
                RightOverlapFlags, voxelSize);
        }
    }

    /// <summary>
    /// Evaluate center proximity for the selected track.
    /// </summary>
    private void ComputeOverlaps(VoxelData voxelData, SpatialOptimizer optimizer,
        Transform trackTransform, Transform cropRowTransform,
        bool[] overlapFlags, float voxelSize)
    {
        if (trackTransform == null || cropRowTransform == null) return;

        float voxelSizeSqr = voxelSize * voxelSize;

        // Visit every track voxel center.
        for (int i = 0; i < voxelData.TrackLocalCenters.Count; i++)
        {
            // Transform the track voxel center to world coordinates.
            Vector3 trackWorldPos = trackTransform.TransformPoint(voxelData.TrackLocalCenters[i]);

            // Transform the track center into the crop-row coordinate frame.
            Vector3 trackInCropSpace = cropRowTransform.InverseTransformPoint(trackWorldPos);

            // Get the hash-cell index for this position.
            Vector3Int centerKey = optimizer.WorldToHash(trackInCropSpace);

            // --- Candidate evaluation and episode-level deduplication. ---

            // bool overlap = false; // Previous implementation.
            bool overlapFoundForThisTrackVoxel = false; // Use a local flag for this track center.

            // Search the 3x3x3 neighborhood (27 hash cells).
            for (int dx = -1; dx <= 1; dx++) // Previous loop also stopped on the first match.
            {
                for (int dy = -1; dy <= 1; dy++) // Previous loop also stopped on the first match.
                {
                    for (int dz = -1; dz <= 1; dz++) // Previous loop also stopped on the first match.
                    {
                        var key = new Vector3Int(centerKey.x + dx, centerKey.y + dy, centerKey.z + dz);

                        if (optimizer.CropHashIndices.TryGetValue(key, out var indices))
                        {
                            foreach (int cropIndex in indices)
                            {
                                Vector3 cropWorldPos = cropRowTransform.TransformPoint(
                                    voxelData.CropLocalCenters[cropIndex]);

                                if (Vector3.SqrMagnitude(trackWorldPos - cropWorldPos) <= voxelSizeSqr)
                                {
                                    overlapFoundForThisTrackVoxel = true; // Mark this track center as qualifying for the current update.
                                    DamagedCropVoxelIndices.Add(cropIndex); // Add the qualifying crop-center index to the episode set.

                                    // Do not stop after the first qualifying crop center.
                                    // Continue evaluating candidates to collect all qualifying crop-center indices.
                                    // break; // Previous implementation.
                                }
                            }
                        }
                    }
                }
            }
            overlapFlags[i] = overlapFoundForThisTrackVoxel; // Store this track center's per-update qualifying flag.
            // --- End of source update. ---
        }
    }
    #endregion
}
