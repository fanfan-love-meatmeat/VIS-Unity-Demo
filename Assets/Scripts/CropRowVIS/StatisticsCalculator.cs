// StatisticsCalculator.cs - VIS statistics calculator
// Calculate and maintain center-proximity statistics.

using UnityEngine;

/// <summary>
/// Calculate per-update qualifying track-voxel counts and Voxel Intrusion Scores.
/// </summary>
public class StatisticsCalculator
{
    #region Statistics properties
    public int LeftOverlapCount { get; private set; } = 0;
    public int RightOverlapCount { get; private set; } = 0;

    public float LeftOverlapVolume { get; private set; } = 0f;
    public float RightOverlapVolume { get; private set; } = 0f;
    public float TotalOverlapVolume { get; private set; } = 0f;

    public float OverlapPercentage { get; private set; } = 0f;              // Percentage of qualifying track voxels across both tracks.
    public float LeftOverlapPercentage { get; private set; } = 0f;          // Percentage of qualifying voxels on the left track.
    public float RightOverlapPercentage { get; private set; } = 0f;         // Percentage of qualifying voxels on the right track.
    #endregion

    #region Statistics update
    /// <summary>
    /// Update center-proximity statistics.
    /// </summary>
    public void UpdateStatistics(OverlapDetector detector, float voxelSize)
    {
        int totalLeft = detector.LeftOverlapFlags?.Length ?? 0;
        int totalRight = detector.RightOverlapFlags?.Length ?? 0;

        // Qualifying track-voxel counts.
        LeftOverlapCount = CountOverlaps(detector.LeftOverlapFlags);
        RightOverlapCount = CountOverlaps(detector.RightOverlapFlags);

        // Per-update Voxel Intrusion Score: qualifying track-voxel count times voxel volume.
        float singleVoxelVolume = voxelSize * voxelSize * voxelSize;
        LeftOverlapVolume = LeftOverlapCount * singleVoxelVolume;
        RightOverlapVolume = RightOverlapCount * singleVoxelVolume;
        TotalOverlapVolume = LeftOverlapVolume + RightOverlapVolume;

        // Qualifying track-voxel percentage
        int totalVoxels = totalLeft + totalRight;
        OverlapPercentage = totalVoxels > 0 ? (float)(LeftOverlapCount + RightOverlapCount) / totalVoxels * 100f : 0f;

        LeftOverlapPercentage = totalLeft > 0 ? (float)LeftOverlapCount / totalLeft * 100f : 0f;
        RightOverlapPercentage = totalRight > 0 ? (float)RightOverlapCount / totalRight * 100f : 0f;
    }

    /// <summary>
    /// Count true values in a qualifying track-voxel flag array.
    /// </summary>
    private int CountOverlaps(bool[] overlapFlags)
    {
        if (overlapFlags == null) return 0;

        int count = 0;
        foreach (bool flag in overlapFlags)
            if (flag) count++;
        return count;
    }
    #endregion

    #region Public interface
    /// <summary>
    /// Return a formatted summary of center-proximity statistics.
    /// </summary>
    public string GetOverlapStatistics()
    {
        return $"Left track: {LeftOverlapCount} qualifying track voxels ({LeftOverlapVolume:F6} m³, {LeftOverlapPercentage:F2}%)\n" +
               $"Right track: {RightOverlapCount} qualifying track voxels ({RightOverlapVolume:F6} m³, {RightOverlapPercentage:F2}%)\n" +
               $"Total: {LeftOverlapCount + RightOverlapCount} qualifying track voxels ({TotalOverlapVolume:F6} m³)\n" +
               $"Qualifying track-voxel percentage: {OverlapPercentage:F2}%";
    }
    #endregion
}
