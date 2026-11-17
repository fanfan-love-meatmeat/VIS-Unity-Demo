using UnityEngine;
using System.Collections.Generic; // <-- Import Dictionary support.

[ExecuteAlways]
public class TrackVoxelizerController : MonoBehaviour
{
    [Header("Crop-row data")]
    public Transform cropRowTransform;
    public TextAsset cropVoxelCsv;
    public TextAsset generatedCenterData; // Synthetic demo input; no real CSV required.

    [Header("Track data")]
    public Transform leftTrackTransform;
    public Transform rightTrackTransform;
    public TextAsset trackVoxelCsv;

    [Header("Performance settings")]
    [Range(1, 60)]
    public int updateFrequency = 10;
    public float movementThreshold = 0.01f;

    [Header("Visualization settings")]
    public bool showTrackVoxels = true;
    public bool showCropRowBounds = true;
    public bool onlyWhenSelected = false;
    public bool showVoxelWireframes = false;
    public bool showLeftTrackWireframe = true;
    public bool showRightTrackWireframe = true;

    [Header("Colors and voxel edge length")]
    [InspectorName("Voxel edge length (l)")]
    public float voxelSize = 0.05f;

    [Header("Left-track voxel colors")]
    public Color leftTrackBaseColor = new Color(1f, 0f, 0f, 10f / 255f);
    [InspectorName("Left-track qualifying voxel color")]
    public Color leftTrackOverlapColor = new Color(1f, 0f, 0f, 1f);

    [Header("Right-track voxel colors")]
    public Color rightTrackBaseColor = new Color(0f, 0f, 1f, 10f / 255f);
    [InspectorName("Right-track qualifying voxel color")]
    public Color rightTrackOverlapColor = new Color(0f, 0f, 1f, 1f);

    [Header("Bounding-box and wireframe colors")]
    public Color cropRowBoundsColor = Color.green;
    public Color leftTrackWireColor = Color.red;
    public Color rightTrackWireColor = Color.blue;

    [Header("VIS statistics")]
    [SerializeField, InspectorName("Left qualifying track-voxel count")] private int leftOverlapCount = 0;
    [SerializeField, InspectorName("Right qualifying track-voxel count")] private int rightOverlapCount = 0;
    [SerializeField, InspectorName("Left-track VIS")] private float leftOverlapVolume = 0f;
    [SerializeField, InspectorName("Right-track VIS")] private float rightOverlapVolume = 0f;
    [SerializeField, InspectorName("Per-update VIS")] private float totalOverlapVolume = 0f;
    [SerializeField, InspectorName("Qualifying track voxels (%)")] private float overlapPercentage = 0f;
    [SerializeField, InspectorName("Left qualifying voxels (%)")] private float leftOverlapPercentage = 0f;
    [SerializeField, InspectorName("Right qualifying voxels (%)")] private float rightOverlapPercentage = 0f;

    private VoxelData voxelData;
    private SpatialOptimizer spatialOptimizer;
    private OverlapDetector overlapDetector;
    private StatisticsCalculator statisticsCalculator;
    private VoxelVisualizer visualizer;

    private Transform leftVoxelTransform, rightVoxelTransform;
    private float lastUpdateTime;
    private float updateInterval;
    private bool isInitialized = false;
    private bool hasSnapshot = false;                                  // [FIX] Edit-mode pose tracking: whether a first snapshot is available.
    private Vector3 lastLeftPos, lastRightPos, lastCropPos;            // [FIX] Previous track and crop positions.
    private Quaternion lastLeftRot, lastRightRot, lastCropRot;         // [FIX] Previous track and crop rotations.
    private bool IsReady => isInitialized
        && voxelData != null && spatialOptimizer != null
        && overlapDetector != null && statisticsCalculator != null;

    void OnEnable()
    {
        isInitialized = false;
        if (!TrackVoxelizerUtils.Validate(this))
        {
            enabled = false;
            return;
        }

        InitializeComponents();
        InitializeSystem();

        isInitialized = true;
        Debug.Log($"[TrackVoxelizer] Initialization complete.");
    }

    void OnDisable()
    {
        isInitialized = false;
    }

    void Update()
    {
        if (!IsReady) return;

        // Use separate update rules for Play and edit modes:
        //  - Play mode: retain the updateFrequency time limit used by the source detector.
        //  - Edit mode: Time.time does not advance normally, so detect changes in track/crop poses.
        //    Refresh voxel visualization when the vehicle moves in edit mode.
        if (Application.isPlaying)
        {
            updateInterval = 1f / updateFrequency;
            if (Time.time - lastUpdateTime < updateInterval) return;
        }
        else if (!HasSignificantMovement())
        {
            return;
        }

        UpdateVoxelTransforms();
        UpdateOverlapDetection();
        UpdateStatistics();
        lastUpdateTime = Time.time;
    }

    // [FIX] Refresh on pose changes in edit mode instead of using the time limit.
    private bool HasSignificantMovement()
    {
        bool moved = CaptureMove(ref lastLeftPos, ref lastLeftRot, leftTrackTransform);
        moved |= CaptureMove(ref lastRightPos, ref lastRightRot, rightTrackTransform);
        moved |= CaptureMove(ref lastCropPos, ref lastCropRot, cropRowTransform);
        hasSnapshot = true;
        return moved;
    }

    private bool CaptureMove(ref Vector3 lastPos, ref Quaternion lastRot, Transform t)
    {
        if (t == null) return false;
        if (!hasSnapshot)
        {
            lastPos = t.position;
            lastRot = t.rotation;
            return true;   // Treat the first snapshot as movement to trigger an initial refresh.
        }
        bool moved = Vector3.Distance(t.position, lastPos) > movementThreshold
                  || Mathf.Abs(Quaternion.Angle(t.rotation, lastRot)) > 1f;
        if (moved)
        {
            lastPos = t.position;
            lastRot = t.rotation;
        }
        return moved;
    }

    void OnDrawGizmos()
    {
        if (!IsReady || visualizer == null) return;

        visualizer.DrawGizmosWithOverlapColors(
            gameObject,
            voxelData,
            overlapDetector,
            cropRowTransform,
            leftTrackTransform,
            rightTrackTransform,
            leftVoxelTransform,
            rightVoxelTransform,
            voxelSize,
            onlyWhenSelected,
            showTrackVoxels,
            showVoxelWireframes,
            showLeftTrackWireframe,
            showRightTrackWireframe,
            showCropRowBounds,
            cropRowBoundsColor,
            leftTrackWireColor,
            rightTrackWireColor,
            leftTrackBaseColor,
            leftTrackOverlapColor,
            rightTrackBaseColor,
            rightTrackOverlapColor
        );
    }

    private void InitializeComponents()
    {
        voxelData = new VoxelData();
        spatialOptimizer = new SpatialOptimizer();
        overlapDetector = new OverlapDetector();
        statisticsCalculator = new StatisticsCalculator();
        visualizer = new VoxelVisualizer();
    }

    private void InitializeSystem()
    {
        if (generatedCenterData != null)
            voxelData.LoadGeneratedCenters(generatedCenterData, voxelSize);
        else
        {
            voxelData.LoadCropVoxels(cropVoxelCsv, voxelSize);
            voxelData.LoadTrackVoxels(trackVoxelCsv);
        }

        spatialOptimizer.Initialize(voxelData, voxelSize);
        overlapDetector.Initialize(voxelData.TrackLocalCenters.Count);

        UpdateVoxelTransforms();

        Debug.Log($"[TrackVoxelizer] Data loaded - crop voxel centers: {voxelData.CropLocalCenters.Count}, track voxel centers: {voxelData.TrackLocalCenters.Count}");
    }

    private void UpdateVoxelTransforms()
    {
        leftVoxelTransform = TrackVoxelizerUtils.UpdateOrCreateNormalizedTransform(transform, leftTrackTransform, leftVoxelTransform);
        rightVoxelTransform = TrackVoxelizerUtils.UpdateOrCreateNormalizedTransform(transform, rightTrackTransform, rightVoxelTransform);
    }

    private void UpdateOverlapDetection()
    {
        overlapDetector.ComputeOverlaps(voxelData, spatialOptimizer,
            leftVoxelTransform, rightVoxelTransform, cropRowTransform, voxelSize);
    }

    private void UpdateStatistics()
    {
        statisticsCalculator.UpdateStatistics(overlapDetector, voxelSize);
        leftOverlapCount = statisticsCalculator.LeftOverlapCount;
        rightOverlapCount = statisticsCalculator.RightOverlapCount;
        leftOverlapVolume = statisticsCalculator.LeftOverlapVolume;
        rightOverlapVolume = statisticsCalculator.RightOverlapVolume;
        totalOverlapVolume = statisticsCalculator.TotalOverlapVolume;
        overlapPercentage = statisticsCalculator.OverlapPercentage;
        leftOverlapPercentage = statisticsCalculator.LeftOverlapPercentage;
        rightOverlapPercentage = statisticsCalculator.RightOverlapPercentage;
    }

    public string GetOverlapStatistics()
    {
        return statisticsCalculator?.GetOverlapStatistics() ?? "Statistics unavailable.";
    }

    public void ForceUpdateOverlaps()
    {
        if (!IsReady) return;
        UpdateOverlapDetection();
        UpdateStatistics();
        lastUpdateTime = Time.time;
    }

    // Expose per-update track VIS values.
    public float LeftOverlapVolume => leftOverlapVolume;
    public float RightOverlapVolume => rightOverlapVolume;
    public float TotalOverlapVolume => totalOverlapVolume;

    // --- Expose episode-level unique qualifying crop-center statistics. ---

    /// <summary>
    /// Return the episode-level unique qualifying crop-voxel index count (N_VIS,E).
    /// </summary>
    public int TotalUniqueDamagedVoxelCount => overlapDetector?.DamagedCropVoxelIndices.Count ?? 0;

    /// <summary>
    /// Return the episode-level Voxel Intrusion Volume Proxy (V_VIS,E), not a measured physical volume.
    /// </summary>
    public float TotalUniqueDamagedVolume => TotalUniqueDamagedVoxelCount * (voxelSize * voxelSize * voxelSize);

    /// <summary>
    /// Clear the unique qualifying crop-center set at the start of each episode.
    /// </summary>
    public void ResetUniqueDamageTracking()
    {
        if (overlapDetector != null)
        {
            overlapDetector.ResetDamagedVoxelTracking();
        }
    }
    // --- End of source update. ---

    // --- Expose qualifying crop-center data for the source DataManager interface. ---

    /// <summary>
    /// Return a dictionary of all unique qualifying crop centers in this episode.
    /// Key: Global crop-center index (ID).
    /// Value: Center coordinates in the crop-row coordinate frame.
    /// </summary>
    public Dictionary<int, Vector3> GetDamagedVoxelData()
    {
        var data = new Dictionary<int, Vector3>();
        if (overlapDetector == null || voxelData == null)
        {
            return data;
        }

        // Visit the unique qualifying crop-center indices stored in the HashSet.
        foreach (int index in overlapDetector.DamagedCropVoxelIndices)
        {
            // Check that the index is within VoxelData bounds.
            if (index >= 0 && index < voxelData.CropLocalCenters.Count)
            {
                // Store the index and its local center coordinates in the dictionary.
                data[index] = voxelData.CropLocalCenters[index];
            }
        }
        return data;
    }
    // --- End of source update. ---
}
