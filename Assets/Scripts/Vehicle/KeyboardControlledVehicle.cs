using UnityEngine;

// Require a TrackedVehicleController on the vehicle.
[RequireComponent(typeof(TrackedVehicleController))]
public class KeyboardControlledVehicle : MonoBehaviour
{
    [Header("Keyboard control settings")]
    [Tooltip("Steering-command magnitude while a key is held. Positive commands turn left; negative commands turn right.")]
    public float keyboardCommandValue = 0.25f;

    [Header("Debug settings")]
    public bool enableDebugLog = false;

    // Store the steering command for the current keyboard input.
    private float currentSteering = 0f;

    void Update()
    {
        // Cache keyboard input; VehicleControlManager samples it at each control tick.
        if (!Application.isPlaying) return;

        // Read horizontal keyboard input (-1 for A/Left, 1 for D/Right).
        float input = Input.GetAxisRaw("Horizontal");

        // Paper yaw is positive left, opposite to Unity's Horizontal-axis input.
        if (input < 0)
            currentSteering = keyboardCommandValue;
        else if (input > 0)
            currentSteering = -keyboardCommandValue;
        else
            currentSteering = 0f;

        if (enableDebugLog)
            Debug.Log($"[Keyboard] Input={input}, Steering={currentSteering:F2}");
    }

    /// <summary>
    /// Expose the current command to VehicleControlManager.
    /// </summary>
    /// <returns>The normalized steering command in [-1, 1] for the current keyboard input.</returns>
    public float GetSteeringValue()
    {
        return currentSteering;
    }
}
