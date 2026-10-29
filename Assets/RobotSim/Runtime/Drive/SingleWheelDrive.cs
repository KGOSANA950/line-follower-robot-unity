using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Single drive wheel (tricycle): one front wheel that is both steered and driven, and two unpowered rear
    /// wheels. The robot turns about a point on the rear axle line; at ±90° steering it pivots on the spot
    /// around the middle of the rear axle.
    /// </summary>
    [AddComponentMenu("Robot Sim/Single Wheel Drive")]
    public class SingleWheelDrive : DriveSystem, ISteeredDrive
    {
        [SerializeField] Motor driveMotor;
        [SerializeField] SteeringServo steering;
        [SerializeField] SimWheel frontWheel;
        [SerializeField] SimWheel rearLeft;
        [SerializeField] SimWheel rearRight;

        Motor[] motors;
        SteeringServo[] servos;

        public override string DriveType => "Single Wheel Drive";
        public override IReadOnlyList<Motor> DriveMotors => motors ??= new[] { driveMotor };
        public override IReadOnlyList<SteeringServo> DriveServos => servos ??= new[] { steering };

        public Motor DriveMotor => driveMotor;
        public SteeringServo SteeringServo => steering;
        public float SteerAngle => steering.Angle;
        public float MaxSteerAngle => steering.MaxAngle;

        /// <summary>Distance from the rear axle to the front wheel (m).</summary>
        public float WheelBase => Mathf.Abs(frontWheel.transform.localPosition.z - rearLeft.transform.localPosition.z);

        /// <summary>Distance between the two rear wheels (m).</summary>
        public float TrackWidth => Vector3.Distance(rearLeft.transform.position, rearRight.transform.position);

        public float WheelRadius => frontWheel.Radius;

        public void SetPower(float power) => driveMotor.Power = power;
        public void SetSteering(float angleDegrees) => steering.TargetAngle = angleDegrees;

        protected override void ApplySteering()
        {
            if (frontWheel != null && steering != null)
                frontWheel.SteerAngle = steering.Angle;
        }

        internal void Configure(Motor motor, SteeringServo servo, SimWheel front, SimWheel left, SimWheel right)
        {
            driveMotor = motor;
            steering = servo;
            frontWheel = front;
            rearLeft = left;
            rearRight = right;
            motors = null;
            servos = null;
        }
    }
}
