using UnityEngine;

/// <summary>
/// Helpers for TrackVoxelizer Transform synchronization and parameter validation.
/// </summary>
public static class TrackVoxelizerUtils
{
    /// <summary>
    /// Validate the controller's required references.
    /// </summary>
    public static bool Validate(TrackVoxelizerController ctrl)
    {
        bool isValid = true;
        if (ctrl.cropRowTransform == null)
        {
            Debug.LogError("[TrackVoxelizer] Error: crop-row Transform is not assigned.");
            isValid = false;
        }
        if (ctrl.generatedCenterData == null && ctrl.cropVoxelCsv == null)
        {
            Debug.LogError("[TrackVoxelizer] Error: crop voxel CSV is not assigned.");
            isValid = false;
        }
        if (ctrl.generatedCenterData == null && ctrl.trackVoxelCsv == null)
        {
            Debug.LogError("[TrackVoxelizer] Error: track voxel CSV is not assigned.");
            isValid = false;
        }
        if (ctrl.leftTrackTransform == null && ctrl.rightTrackTransform == null)
        {
            Debug.LogError("[TrackVoxelizer] Error: at least one track Transform is required.");
            isValid = false;
        }
        return isValid;
    }

    /// <summary>
    /// Synchronize unit-scale Transforms for voxel visualization in world space.
    /// </summary>
    public static Transform UpdateOrCreateNormalizedTransform(Transform controllerRoot, Transform src, Transform cache)
    {
        if (src == null) return null;

        string normName = src.name + "_VoxelTransform";
        Transform norm = cache;
        if (norm == null)
        {
            norm = controllerRoot.Find(normName);
            if (norm == null)
            {
                GameObject normObj = new GameObject(normName);
                normObj.transform.SetParent(controllerRoot);
                norm = normObj.transform;
                normObj.hideFlags = HideFlags.HideInHierarchy;
            }
        }
        // Synchronize in world space without inheriting the parent scale.
        norm.position = src.position;
        norm.rotation = src.rotation;
        norm.localScale = Vector3.one;
        return norm;
    }
}
