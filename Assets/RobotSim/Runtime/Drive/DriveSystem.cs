using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Base class for a drive configuration (differential, Ackermann, tracked, ...).
    /// A drive system groups the robot's motors, servos and wheels and offers convenient commands for them.
    /// Drive systems run after the program each physics step (to apply servo angles to the wheels) and
    /// before the wheels apply their forces.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public abstract class DriveSystem : MonoBehaviour
    {
        /// <summary>Human-readable drive type, e.g. "Differential Drive".</summary>
        public abstract string DriveType { get; }

        /// <summary>Every motor this drive controls.</summary>
        public abstract IReadOnlyList<Motor> DriveMotors { get; }

        /// <summary>Every steering servo this drive controls (none for differential/tracked drives).</summary>
        public virtual IReadOnlyList<SteeringServo> DriveServos => Array.Empty<SteeringServo>();

        /// <summary>Sets every motor's power to zero (which brakes). Servos keep their angle.</summary>
        public virtual void Stop()
        {
            foreach (var m in DriveMotors)
                if (m != null) m.Power = 0f;
        }

        /// <summary>Copy servo angles to the wheels. Called every physics step.</summary>
        protected virtual void ApplySteering() { }

        void FixedUpdate() => ApplySteering();
    }

    /// <summary>
    /// A drive with one drive motor and a steering servo: single-wheel (tricycle), Ackermann (car) and synchro drives.
    /// </summary>
    public interface ISteeredDrive
    {
        /// <summary>Drive motor power, -1 (full reverse) to 1 (full forward).</summary>
        void SetPower(float power);

        /// <summary>Steering angle in degrees. Positive = left (anticlockwise seen from above).</summary>
        void SetSteering(float angleDegrees);

        /// <summary>The steering's actual current angle in degrees.</summary>
        float SteerAngle { get; }

        /// <summary>Largest steering angle in either direction.</summary>
        float MaxSteerAngle { get; }

        Motor DriveMotor { get; }
        SteeringServo SteeringServo { get; }
    }
}
