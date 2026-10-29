using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Two independently driven wheels on a common axle, plus a caster for balance.
    /// Equal power drives straight; unequal power turns; opposite power spins on the spot.
    /// </summary>
    [AddComponentMenu("Robot Sim/Differential Drive")]
    public class DifferentialDrive : DriveSystem
    {
        [SerializeField] Motor leftMotor;
        [SerializeField] Motor rightMotor;
        [SerializeField] SimWheel leftWheel;
        [SerializeField] SimWheel rightWheel;

        Motor[] motors;

        public override string DriveType => "Differential Drive";

        public override IReadOnlyList<Motor> DriveMotors => motors ??= new[] { leftMotor, rightMotor };

        public Motor Left => leftMotor;
        public Motor Right => rightMotor;

        /// <summary>Wheel radius in metres.</summary>
        public float WheelRadius => leftWheel != null ? leftWheel.Radius : 0f;

        /// <summary>Distance between the centres of the two drive wheels in metres.</summary>
        public float TrackWidth => (leftWheel != null && rightWheel != null)
            ? Vector3.Distance(leftWheel.transform.position, rightWheel.transform.position)
            : 0f;

        /// <summary>Sets both motor powers at once (-1..1 each).</summary>
        public void SetPower(float left, float right)
        {
            leftMotor.Power = left;
            rightMotor.Power = right;
        }

        internal void Configure(Motor left, Motor right, SimWheel leftW, SimWheel rightW)
        {
            leftMotor = left;
            rightMotor = right;
            leftWheel = leftW;
            rightWheel = rightW;
            motors = null;
        }
    }
}
