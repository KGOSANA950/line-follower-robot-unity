using System.Linq;
using RobotSim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSim.Examples
{
    /// <summary>
    /// Drive any robot by hand with W/A/S/D (or the arrow keys); hold Shift for full power.
    /// <list type="bullet">
    /// <item>Differential / tracked: W/S drive, A/D turn (tank mixing).</item>
    /// <item>Single wheel / Ackermann: W/S drive, A/D steer.</item>
    /// <item>Synchro: the keys choose a direction (W = forward, A = left, W+D = diagonal ...) and the wheels turn to face it.</item>
    /// </list>
    /// Uses exactly the same API as any student program.
    /// </summary>
    public class KeyboardTeleop : RobotProgram
    {
        public float normalPower = 0.5f;
        public float turnAmount = 0.6f;

        protected override void OnStart()
        {
            string drive = Robot.Drive != null ? Robot.Drive.DriveType : "no drive";
            Log($"Keyboard teleop ({drive}): WASD / arrow keys to drive, Shift for full power.");

            var colour = GetSensors<ColourSensor>().FirstOrDefault();
            if (colour != null) colour.OnColourChanged += c => Log($"Floor colour: {c}");
            var metal = GetSensors<MetalDetector>().FirstOrDefault();
            if (metal != null) metal.OnMetalDetected += () => Log("Metal detected!");
        }

        protected override void OnTick(float dt)
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float forward = 0f, right = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) forward += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) forward -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) right += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) right -= 1f;
            float power = kb.shiftKey.isPressed ? 1f : normalPower;

            switch (Robot.Drive)
            {
                case DifferentialDrive tank: // also tracked robots
                {
                    float left = forward + right * turnAmount;
                    float rightPower = forward - right * turnAmount;
                    float biggest = Mathf.Max(1f, Mathf.Abs(left), Mathf.Abs(rightPower));
                    tank.SetPower(left / biggest * power, rightPower / biggest * power);
                    Plot("Speed L (rad/s)", tank.Left.WheelSpeed);
                    break;
                }

                case SynchroDrive synchro:
                {
                    if (forward == 0f && right == 0f)
                    {
                        synchro.SetPower(0f);
                        break;
                    }
                    // Direction of travel relative to the body: 0 = forward, +90 = left.
                    float direction = Mathf.Atan2(-right, forward) * Mathf.Rad2Deg;
                    synchro.SetSteering(direction);
                    // Only drive once the wheels are (nearly) pointing the right way.
                    float error = Mathf.Abs(Mathf.DeltaAngle(synchro.SteerAngle, direction));
                    synchro.SetPower(error < 20f ? power : 0f);
                    Plot("Wheel direction (deg)", synchro.SteerAngle);
                    break;
                }

                case ISteeredDrive car: // single wheel and Ackermann
                {
                    car.SetSteering(-right * car.MaxSteerAngle);   // A = steer left (positive angle)
                    car.SetPower(forward * power);
                    Plot("Steering (deg)", car.SteerAngle);
                    break;
                }
            }

            var front = GetSensors<UltrasonicSensor>().FirstOrDefault(s => s.Name == "Front");
            if (front != null) Plot("Front distance (m)", front.Distance);
        }
    }
}
