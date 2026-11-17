using UnityEngine;

/// <summary>
/// Provides a camera that follows a target with a fixed world-space orientation.
/// Only position follows the target; rotation remains fixed in world space.
/// The initial position offset can be calculated automatically at startup.
/// </summary>
public class DroneFollowCamera : MonoBehaviour
{
    [Header("Follow target")]
    [Tooltip("Assign the target Transform to follow, such as VehicleBody.")]
    public Transform target;

    // --- Optional initialization ---
    [Header("Position offset settings")]
    [Tooltip("Calculate the offset from the initial camera and target positions at startup; otherwise use the configured Position Offset.")]
    public bool autoCalculateOffsetOnStart = true;
    // --- End of source update. ---

    [Tooltip("Fixed camera offset relative to the target. Automatic calculation replaces this value at startup.")]
    public Vector3 positionOffset = new Vector3(0, 10, -8);

    // Start is called before the first frame update
    void Start()
    {
        // Check that a target has been assigned.
        if (target == null)
        {
            Debug.LogWarning("DroneFollowCamera: No follow target is assigned. The component has been disabled.", this);
            this.enabled = false;
            return;
        }

        // --- Calculate the offset when automatic initialization is enabled. ---
        if (autoCalculateOffsetOnStart)
        {
            // Calculate and retain the initial position offset.
            // Offset = current camera position - target position.
            positionOffset = transform.position - target.position;
            Debug.Log($"[DroneFollowCamera] Calculated initial position offset: {positionOffset}");
        }
    }

    // Update after the other Update calls to reduce camera-follow jitter.
    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // Update position only.
        // Camera position = current target position + the calculated or configured offset.
        transform.position = target.position + positionOffset;
    }
}