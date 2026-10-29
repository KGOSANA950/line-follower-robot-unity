using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// A position-controlled steering servo. Students set <see cref="TargetAngle"/> (degrees, positive = left)
    /// and the servo turns towards it at up to <see cref="MaxSpeed"/> degrees per second, like a real RC servo.
    /// A <see cref="Continuous"/> servo can rotate all the way round (used by synchro drive).
    /// </summary>
    [DefaultExecutionOrder(40)]
    [AddComponentMenu("Robot Sim/Steering Servo")]
    public class SteeringServo : MonoBehaviour
    {
        [Tooltip("Name used in code, e.g. Servos[\"Steer\"].")]
        [SerializeField] string servoName = "Steer";
        [SerializeField] float minAngle = -35f;
        [SerializeField] float maxAngle = 35f;
        [Tooltip("Can rotate a full 360° (angles wrap to -180..180).")]
        [SerializeField] bool continuous;
        [Tooltip("How fast the servo turns (degrees per second).")]
        [SerializeField] float maxSpeed = 240f;

        float target;
        float angle;

        public string Name => servoName;
        public float MinAngle => continuous ? -180f : minAngle;
        public float MaxAngle => continuous ? 180f : maxAngle;
        public bool Continuous => continuous;

        /// <summary>Degrees per second.</summary>
        public float MaxSpeed
        {
            get => maxSpeed;
            set => maxSpeed = Mathf.Max(1f, value);
        }

        /// <summary>Where the servo is trying to go, in degrees (positive = left). Clamped to its range.</summary>
        public float TargetAngle
        {
            get => target;
            set
            {
                if (Locked || float.IsNaN(value)) return;
                target = continuous ? Mathf.DeltaAngle(0f, value) : Mathf.Clamp(value, minAngle, maxAngle);
            }
        }

        /// <summary>The servo's actual current angle in degrees.</summary>
        public float Angle => angle;

        /// <summary>True when the servo has (almost) reached its target.</summary>
        public bool AtTarget => Mathf.Abs(Mathf.DeltaAngle(angle, target)) < 0.5f;

        internal bool Locked { get; set; }

        internal void Configure(string name, float min, float max, bool isContinuous, float speed)
        {
            servoName = name;
            minAngle = min;
            maxAngle = max;
            continuous = isContinuous;
            maxSpeed = speed;
        }

        internal void ResetState()
        {
            Locked = false;
            target = 0f;
            angle = 0f;
        }

        /// <summary>Freezes the servo where it is (after a program error).</summary>
        internal void Hold()
        {
            target = angle;
            Locked = true;
        }

        void FixedUpdate()
        {
            float step = maxSpeed * Time.fixedDeltaTime;
            angle = continuous
                ? Mathf.DeltaAngle(0f, Mathf.MoveTowardsAngle(angle, target, step))
                : Mathf.MoveTowards(angle, target, step);
        }
    }
}
