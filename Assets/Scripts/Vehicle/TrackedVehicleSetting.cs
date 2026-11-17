using UnityEngine;

// Configure the dimensions and positions of the tracked vehicle body and tracks.
// Retain dimension updates, initialization and parameter validation; motion control is separate.
public class TrackedVehicleSetting : MonoBehaviour
{
    [Header("Vehicle Parameters")]
    [Tooltip("Vehicle body length")]
    public float vehicleLength = 2.0f;    // Vehicle body length.
    [Tooltip("Vehicle body width")]
    public float vehicleWidth = 1.0f;     // Vehicle body width.
    [Tooltip("Vehicle body height")]
    public float vehicleHeight = 0.5f;    // Vehicle body height.

    [Header("Track Parameters")]
    [Tooltip("Track width")]
    public float trackWidth = 0.2f;       // Width of one track.
    [Tooltip("Track length")]
    public float trackLength = 2.0f;      // Length of one track.
    [Tooltip("Track height")]
    public float trackHeight = 0.3f;      // Height of one track.
    [Tooltip("Track ground clearance")]
    public float trackGroundClearance = 0.1f; // Minimum track clearance above the ground.
    [Tooltip("Track gauge")]
    [InspectorName("Track gauge (s)")]
    public float trackSpacing = 1.4f;     // Shared geometry/control track gauge s in meters.


    // Transform references for configuring vehicle dimensions.
    public Transform leftTrack;      // Left-track Transform.
    public Transform rightTrack;     // Right-track Transform.
    public Transform vehicleBody;    // Vehicle-body Transform.

    // Start is called once when the simulation begins.
    void Start()
    {
        // Find child objects by name if they were not assigned in the Inspector.
        if (vehicleBody == null)
            vehicleBody = transform.Find("VehicleBody");
        if (leftTrack == null)
            leftTrack = transform.Find("LeftTrack");
        if (rightTrack == null)
            rightTrack = transform.Find("RightTrack");

        // Apply the configured dimensions and positions to the body and tracks.
        UpdateDimensions();
    }

    // Input handling, speed calculation and motion have been removed from this component.
    // This component configures dimensions only; it contains no control logic.

    /// <summary>
    /// Update the body and track dimensions and positions from the configured parameters.
    /// </summary>
    public void UpdateDimensions()
    {
        // Update the body local scale and position when its Transform is assigned.
        if (vehicleBody != null)
        {
            // Set the body width, height and length.
            vehicleBody.localScale = new Vector3(vehicleWidth, vehicleHeight, vehicleLength);
            // Align the body underside with the tops of the tracks.
            // Body center height = track height + ground clearance + half the body height.
            vehicleBody.localPosition = new Vector3(
                0f,
                trackHeight + trackGroundClearance + vehicleHeight / 2f,
                0f
            );
        }

        // Update the left-track local scale and position when assigned.
        if (leftTrack != null)
        {
            // Set the left-track width, height and length.
            leftTrack.localScale = new Vector3(trackWidth, trackHeight, trackLength);
            // Set the left-track position:
            // X: -trackSpacing / 2, to the left of the vehicle center.
            // Y: half the track height plus the ground clearance.
            // Z: retain zero offset.
            leftTrack.localPosition = new Vector3(
                -trackSpacing / 2f,
                trackHeight / 2f + trackGroundClearance,
                0f
            );
        }

        // Update the right-track local scale and position when assigned.
        if (rightTrack != null)
        {
            // Set the right-track width, height and length.
            rightTrack.localScale = new Vector3(trackWidth, trackHeight, trackLength);
            // Set the right-track position:
            // X: trackSpacing / 2, to the right of the vehicle center.
            // Y: half the track height plus the ground clearance.
            // Z: retain zero offset.
            rightTrack.localPosition = new Vector3(
                trackSpacing / 2f,
                trackHeight / 2f + trackGroundClearance,
                0f
            );
        }
    }

    // OnValidate updates and validates parameters after Inspector edits.
    void OnValidate()
    {
        // Clamp body dimensions to their minimum allowed values.
        vehicleLength = Mathf.Max(0.1f, vehicleLength);
        vehicleWidth = Mathf.Max(0.1f, vehicleWidth);
        vehicleHeight = Mathf.Max(0.1f, vehicleHeight);

        // Clamp track dimensions to their minimum allowed values.
        trackWidth = Mathf.Max(0.05f, trackWidth);
        trackLength = Mathf.Max(0.1f, trackLength);
        trackHeight = Mathf.Max(0.05f, trackHeight);
        trackGroundClearance = Mathf.Max(0f, trackGroundClearance);
        // Ensure the track gauge is at least twice the width of one track.
        trackSpacing = Mathf.Max(trackWidth * 2f, trackSpacing);

        // Reapply dimensions and positions after parameter changes.
        UpdateDimensions();
    }
}
