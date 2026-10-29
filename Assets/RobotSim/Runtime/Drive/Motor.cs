using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// A geared DC motor. Students set <see cref="Power"/> (-1..1, like a PWM duty cycle) and the motor
    /// produces torque according to a simple DC motor model:
    /// <c>torque = stallTorque * (power - wheelSpeed / freeSpeed)</c>.
    /// So the motor is strongest from standstill, tops out at <c>power * freeSpeed</c>, and brakes when power is 0.
    /// </summary>
    [AddComponentMenu("Robot Sim/Motor")]
    public class Motor : MonoBehaviour
    {
        [Tooltip("Name used in code, e.g. Motors[\"Left\"].")]
        [SerializeField] string motorName = "Left";

        [Tooltip("Torque at the wheel (N·m) at full power from standstill (shared between the wheels it drives).")]
        [SerializeField] float stallTorque = 0.15f;

        [Tooltip("Wheel speed (rad/s) reached at full power with no load.")]
        [SerializeField] float freeSpeed = 14f;

        [Tooltip("Encoder resolution: ticks per wheel revolution.")]
        [SerializeField] int ticksPerRevolution = 360;

        readonly List<SimWheel> wheels = new List<SimWheel>();
        float power;
        float angleOffset;

        public string Name => motorName;
        public float StallTorque => stallTorque;
        public float FreeSpeed => freeSpeed;
        public int TicksPerRevolution => ticksPerRevolution;

        /// <summary>Motor power from -1 (full reverse) to 1 (full forward). 0 brakes.</summary>
        public float Power
        {
            get => power;
            set
            {
                if (Locked)
                    return;
                power = float.IsNaN(value) ? 0f : Mathf.Clamp(value, -1f, 1f);
            }
        }

        /// <summary>Wheel angular velocity in rad/s (positive = rolling forward).</summary>
        public float WheelSpeed
        {
            get
            {
                if (wheels.Count == 0) return 0f;
                float sum = 0f;
                foreach (var w in wheels) sum += w.AngularVelocity;
                return sum / wheels.Count;
            }
        }

        /// <summary>Wheel speed in revolutions per minute.</summary>
        public float Rpm => WheelSpeed * 60f / (2f * Mathf.PI);

        /// <summary>Total wheel rotation in radians since the last encoder reset.</summary>
        public float Angle => RawAngle - angleOffset;

        /// <summary>Encoder count since the last reset (<see cref="TicksPerRevolution"/> per wheel revolution).</summary>
        public long EncoderTicks => (long)System.Math.Floor(Angle / (2f * Mathf.PI) * ticksPerRevolution);

        /// <summary>Distance the wheel rim has rolled in metres since the last encoder reset.</summary>
        public float Distance
        {
            get
            {
                if (wheels.Count == 0) return 0f;
                return Angle * wheels[0].Radius;
            }
        }

        /// <summary>Torque the motor is currently producing in N·m (total across its wheels).</summary>
        public float Torque { get; private set; }

        public IReadOnlyList<SimWheel> Wheels => wheels;

        public void ResetEncoder() => angleOffset = RawAngle;

        float RawAngle
        {
            get
            {
                if (wheels.Count == 0) return 0f;
                float sum = 0f;
                foreach (var w in wheels) sum += w.Angle;
                return sum / wheels.Count;
            }
        }

        /// <summary>When locked (after a program error) the motor ignores power commands.</summary>
        internal bool Locked { get; set; }

        internal void Configure(string name, float stall, float free)
        {
            motorName = name;
            stallTorque = stall;
            freeSpeed = free;
        }

        internal void Register(SimWheel wheel)
        {
            if (!wheels.Contains(wheel))
                wheels.Add(wheel);
        }

        internal void ResetState()
        {
            Locked = false;
            power = 0f;
            angleOffset = 0f;
            Torque = 0f;
        }

        /// <summary>Torque for one wheel turning at <paramref name="omega"/> rad/s.</summary>
        internal float TorqueForWheel(float omega)
        {
            int n = Mathf.Max(1, wheels.Count);
            float t = TorqueAt(power, omega, stallTorque, freeSpeed) / n;
            return t;
        }

        internal void RecordTorque()
        {
            float total = 0f;
            foreach (var w in wheels) total += w.AppliedTorque;
            Torque = total;
        }

        /// <summary>The DC motor torque curve.</summary>
        public static float TorqueAt(float power, float omega, float stallTorque, float freeSpeed)
        {
            return stallTorque * (Mathf.Clamp(power, -1f, 1f) - omega / freeSpeed);
        }
    }
}
