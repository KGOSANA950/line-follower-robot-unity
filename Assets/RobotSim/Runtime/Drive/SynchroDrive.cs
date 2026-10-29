using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Synchro drive: three wheels that are all driven by one motor and all steered together by one
    /// continuous steering motor (linked by belts in a real robot). The wheels can point in any direction,
    /// so the robot can drive any way it likes — but its body never rotates.
    /// Steering angle 0 = the body's forward, 90 = left, -90 = right, 180 = backwards.
    /// </summary>
    [AddComponentMenu("Robot Sim/Synchro Drive")]
    public class SynchroDrive : DriveSystem, ISteeredDrive
    {
        [SerializeField] Motor driveMotor;
        [SerializeField] SteeringServo steering;
        [SerializeField] SimWheel[] wheels;

        Motor[] motors;
        SteeringServo[] servos;

        public override string DriveType => "Synchro Drive";
        public override IReadOnlyList<Motor> DriveMotors => motors ??= new[] { driveMotor };
        public override IReadOnlyList<SteeringServo> DriveServos => servos ??= new[] { steering };

        public Motor DriveMotor => driveMotor;
        public SteeringServo SteeringServo => steering;

        /// <summary>Direction the wheels currently point, in degrees relative to the body (positive = left).</summary>
        public float SteerAngle => steering.Angle;

        public float MaxSteerAngle => 180f;

        /// <summary>True when the wheels have finished turning to the requested direction.</summary>
        public bool SteeringReady => steering.AtTarget;

        public float WheelRadius => wheels.Length > 0 ? wheels[0].Radius : 0f;

        public void SetPower(float power) => driveMotor.Power = power;

        /// <summary>Points all wheels in this direction (degrees relative to the body, positive = left).</summary>
        public void SetSteering(float angleDegrees) => steering.TargetAngle = angleDegrees;

        protected override void ApplySteering()
        {
            if (steering == null || wheels == null) return;
            foreach (var w in wheels)
                if (w != null) w.SteerAngle = steering.Angle;
        }

        internal void Configure(Motor motor, SteeringServo servo, SimWheel[] allWheels)
        {
            driveMotor = motor;
            steering = servo;
            wheels = allWheels;
            motors = null;
            servos = null;
        }
    }
}
