// PIDController.cs

using UnityEngine;

public class PIDController : MonoBehaviour
{
    [Header("PID parameters")]
    public float Kp = 0.8f;
    public float Ki = 0f;
    public float Kd = 0.05f;

    [Header("Output limits")]
    public float outputMin = -1f; // Keep the output within the normalized input range [-1, 1].
    public float outputMax = 1f; // Upper bound of the normalized output range [-1, 1].

    [Header("Debug output")]
    public float currentOffset = 0f;  // Final output signal calculated by the PID controller.

    // Keep targetOffset private and expose a read-only property.
    private float targetOffset = 0f;
    public float TargetOffset { get { return targetOffset; } } // Read-only access to the target offset.

    private float integral;
    private float lastError;

    public void SetTargetOffset(float target)
    {
        // Public entry point for setting the PID target.
        targetOffset = target;
    }

    public void ResetPID()
    {
        integral = 0f;
        lastError = 0f;
        currentOffset = 0f;
        targetOffset = 0f;
    }

    public void StepPID(float deltaTime)
    {
        if (deltaTime <= 0f) throw new System.ArgumentOutOfRangeException(nameof(deltaTime));
        // Calculate the PID output from the private targetOffset.
        float error = targetOffset;
        integral += error * deltaTime;
        float derivative = (error - lastError) / deltaTime;

        float output = Kp * error + Ki * integral + Kd * derivative;
        output = Mathf.Clamp(output, outputMin, outputMax);
        currentOffset = output;

        lastError = error;
    }

    // VehicleControlManager owns the 0.1 s update cycle; no independent FixedUpdate.
}
