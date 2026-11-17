using UnityEngine;

/// <summary>
/// Ideal no-slip differential-drive kinematics from Section 4.4, Equations (9)-(11).
/// Positive paper yaw turns left; Unity's positive Y rotation turns right.
/// Track targets are held between control updates and integrated at the physics timestep.
/// </summary>
[RequireComponent(typeof(TrackedVehicleSetting))]
public class TrackedVehicleController : MonoBehaviour
{
    [Header("Differential-drive parameters")]
    [Min(0f), Tooltip("Forward speed v in m/s; the paper uses 0.5 m/s.")]
    public float baseSpeed = 0.5f;
    [Min(0.001f), Tooltip("Maximum absolute speed of one track in m/s; the paper uses 0.8 m/s. Track gauge s is read from Tracked Vehicle Setting.")]
    public float maxTrackSpeed = 0.8f;

    [Header("Held track targets and body motion")]
    [SerializeField, Tooltip("Left-track target speed v_L in m/s, after clipping.")]
    private float leftTrackTargetSpeed;
    [SerializeField, Tooltip("Right-track target speed v_R in m/s, after clipping.")]
    private float rightTrackTargetSpeed;
    [SerializeField, Tooltip("Body forward speed (v_L + v_R) / 2 in m/s.")]
    private float bodyForwardSpeed;
    [SerializeField, Tooltip("Body yaw rate (v_R - v_L) / s in rad/s; positive turns left.")]
    private float bodyYawRate;
    private TrackedVehicleSetting trackSettings;

    public float TrackGauge
    {
        get
        {
            if (trackSettings == null) trackSettings = GetComponent<TrackedVehicleSetting>();
            return trackSettings.trackSpacing;
        }
    }
    public float MaxYawRate => 2f * (maxTrackSpeed - baseSpeed) / TrackGauge;
    public float LeftTrackTargetSpeed => leftTrackTargetSpeed;
    public float RightTrackTargetSpeed => rightTrackTargetSpeed;
    public float BodyForwardSpeed => bodyForwardSpeed;
    public float BodyYawRate => bodyYawRate;

    void Awake()
    {
        ResetController();
    }

    /// <summary>
    /// Map the normalized execution command to yaw and left/right track speed targets.
    /// Positive commands turn left. VehicleControlManager calls this at 10 Hz.
    /// </summary>
    public void SetExecutionCommand(float command)
    {
        float gauge = TrackGauge;
        if (gauge <= 0f || baseSpeed < 0f || maxTrackSpeed < baseSpeed)
            throw new System.InvalidOperationException("Differential-drive parameters require s > 0 and 0 <= v <= v_max.");

        // Equations (9)-(10): omega_max = 2(v_max - v)/s; omega = omega_max * u_tilde.
        float targetYawRate = MaxYawRate * Mathf.Clamp(command, -1f, 1f);
        // Equation (11): v_L = v - s*omega/2; v_R = v + s*omega/2.
        leftTrackTargetSpeed = Mathf.Clamp(baseSpeed - gauge * targetYawRate * 0.5f, -maxTrackSpeed, maxTrackSpeed);
        rightTrackTargetSpeed = Mathf.Clamp(baseSpeed + gauge * targetYawRate * 0.5f, -maxTrackSpeed, maxTrackSpeed);

        // Derive body motion from the final, clipped track targets.
        bodyForwardSpeed = (leftTrackTargetSpeed + rightTrackTargetSpeed) * 0.5f;
        bodyYawRate = (rightTrackTargetSpeed - leftTrackTargetSpeed) / gauge;
    }

    /// <summary>
    /// Reset the steering state for episode initialization or reset.
    /// </summary>
    public void ResetController()
    {
        SetExecutionCommand(0f);
    }

    void FixedUpdate()
    {
        // Retain forward-then-yaw integration and hold targets between 10 Hz updates.
        Vector3 move = transform.forward * bodyForwardSpeed * Time.fixedDeltaTime;
        transform.position += move;

        // Convert positive-left paper yaw to Unity's positive-right Y rotation, in degrees.
        transform.Rotate(Vector3.up, -bodyYawRate * Mathf.Rad2Deg * Time.fixedDeltaTime);
    }

    void OnValidate()
    {
        maxTrackSpeed = Mathf.Max(0.001f, maxTrackSpeed);
        baseSpeed = Mathf.Clamp(baseSpeed, 0f, maxTrackSpeed);
    }
}
