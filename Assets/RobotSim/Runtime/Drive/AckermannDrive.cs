using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Ackermann steering (like a car): the rear wheels are driven by one motor through a differential, and
    /// the front wheels steer. The inner front wheel turns more sharply than the outer one so that both
    /// roll around the same centre point on the rear axle line. You command one steering angle (that of an
    /// imaginary wheel in the middle of the front axle) and the linkage works out each wheel's angle.
    /// </summary>
    [AddComponentMenu("Robot Sim/Ackermann Drive")]
    public class AckermannDrive : DriveSystem, ISteeredDrive
    {
        [SerializeField] Motor driveMotor;
        [SerializeField] SteeringServo steering;
        [SerializeField] SimWheel frontLeft;
        [SerializeField] SimWheel frontRight;
        [SerializeField] SimWheel rearLeft;
        [SerializeField] SimWheel rearRight;

        Motor[] motors;
        SteeringServo[] servos;

        public override string DriveType => "Ackermann Steering";
        public override IReadOnlyList<Motor> DriveMotors => motors ??= new[] { driveMotor };
        public override IReadOnlyList<SteeringServo> DriveServos => servos ??= new[] { steering };

        public Motor DriveMotor => driveMotor;
        public SteeringServo SteeringServo => steering;
        public float SteerAngle => steering.Angle;
        public float MaxSteerAngle => steering.MaxAngle;

        /// <summary>Distance between front and rear axles (m).</summary>
        public float WheelBase => Mathf.Abs(frontLeft.transform.localPosition.z - rearLeft.transform.localPosition.z);

        /// <summary>Distance between the left and right wheels (m).</summary>
        public float TrackWidth => Mathf.Abs(frontLeft.transform.localPosition.x - frontRight.transform.localPosition.x);

        public float WheelRadius => rearLeft.Radius;

        /// <summary>Current turning radius of the middle of the rear axle (m). Infinity when going straight.</summary>
        public float TurningRadius
        {
            get
            {
                float t = Mathf.Tan(SteerAngle * Mathf.Deg2Rad);
                return Mathf.Abs(t) < 1e-4f ? float.PositiveInfinity : WheelBase / Mathf.Abs(t);
            }
        }

        /// <summary>Actual angle of the left front wheel (degrees).</summary>
        public float LeftWheelAngle => frontLeft.SteerAngle;

        /// <summary>Actual angle of the right front wheel (degrees).</summary>
        public float RightWheelAngle => frontRight.SteerAngle;

        public void SetPower(float power) => driveMotor.Power = power;
        public void SetSteering(float angleDegrees) => steering.TargetAngle = angleDegrees;

        protected override void ApplySteering()
        {
            if (steering == null || frontLeft == null || frontRight == null) return;
            ComputeWheelAngles(steering.Angle, WheelBase, TrackWidth, out float left, out float right);
            frontLeft.SteerAngle = left;
            frontRight.SteerAngle = right;
        }

        /// <summary>
        /// Ackermann geometry. <paramref name="angle"/> is the virtual centre-wheel angle (positive = left).
        /// The turn centre lies on the rear axle line at signed lateral distance R = L / tan(angle) (positive = left).
        /// </summary>
        public static void ComputeWheelAngles(float angle, float wheelBase, float trackWidth, out float left, out float right)
        {
            float t = Mathf.Tan(angle * Mathf.Deg2Rad);
            if (Mathf.Abs(t) < 1e-5f)
            {
                left = right = 0f;
                return;
            }
            float r = wheelBase / t;                  // signed: + means centre is to the left
            float half = trackWidth * 0.5f;
            left = Mathf.Atan(wheelBase / (r - half)) * Mathf.Rad2Deg;   // left wheel is at lateral +half
            right = Mathf.Atan(wheelBase / (r + half)) * Mathf.Rad2Deg;  // right wheel is at lateral -half
        }

        internal void Configure(Motor motor, SteeringServo servo, SimWheel fl, SimWheel fr, SimWheel rl, SimWheel rr)
        {
            driveMotor = motor;
            steering = servo;
            frontLeft = fl;
            frontRight = fr;
            rearLeft = rl;
            rearRight = rr;
            motors = null;
            servos = null;
        }
    }
}
