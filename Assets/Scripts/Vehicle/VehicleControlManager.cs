using UnityEngine;

// Sample manual input and update the PD-type command mapping at 10 Hz.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(TrackedVehicleController))]
[RequireComponent(typeof(PIDController))]
[RequireComponent(typeof(KeyboardControlledVehicle))]
public class VehicleControlManager : MonoBehaviour
{
    public const float ControlPeriod = 0.1f;
    private KeyboardControlledVehicle manualController;
    private PIDController pidController;
    private TrackedVehicleController vehicleController;
    private float controlElapsed;
    void Awake()
    {
        manualController = GetComponent<KeyboardControlledVehicle>();
        pidController = GetComponent<PIDController>();
        vehicleController = GetComponent<TrackedVehicleController>();
    }
    public void ResetControlCycle()
    {
        controlElapsed = 0f;
    }
    void FixedUpdate()
    {
        if (Time.timeScale == 0 || !vehicleController.enabled) return;
        controlElapsed += Time.fixedDeltaTime;
        if (controlElapsed + 0.000001f < ControlPeriod) return;
        // Keep residual time without accumulating small negative float-rounding errors.
        controlElapsed = Mathf.Max(0f, controlElapsed - ControlPeriod);
        float targetSteering = manualController.GetSteeringValue();
        pidController.SetTargetOffset(targetSteering);
        pidController.StepPID(ControlPeriod);
        vehicleController.SetExecutionCommand(pidController.currentOffset);
    }
}
