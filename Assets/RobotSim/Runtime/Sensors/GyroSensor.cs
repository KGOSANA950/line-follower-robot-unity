using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Yaw gyroscope (like the Z axis of an MPU-6050), integrated into a heading.
    /// Angles are in degrees and positive anticlockwise seen from above (turning left), measured from
    /// the robot's heading when the program started (or since <see cref="ResetHeading"/>).
    /// </summary>
    [AddComponentMenu("Robot Sim/Sensors/Gyro Sensor")]
    public class GyroSensor : Sensor
    {
        [Tooltip("Simulated gyro drift in degrees per second (0 = perfect).")]
        [SerializeField] float driftDegreesPerSecond;

        float totalRotation;
        float lastYaw;
        float lastTime;
        bool hasLast;

        /// <summary>Heading in degrees, -180..180 (positive = turned left since the start).</summary>
        public float Heading => Mathf.DeltaAngle(0f, totalRotation);

        /// <summary>Total rotation in degrees without wrapping (e.g. two full left turns = 720).</summary>
        public float TotalRotation => totalRotation;

        /// <summary>Turning rate in degrees per second (positive = turning left).</summary>
        public float YawRate { get; private set; }

        public float DriftDegreesPerSecond
        {
            get => driftDegreesPerSecond;
            set => driftDegreesPerSecond = value;
        }

        /// <summary>Fires after every sample with the heading in degrees.</summary>
        public event Action<float> OnReading;

        public override string ReadingText => $"{Heading:+0.0;-0.0;0.0}°  ({YawRate:+0;-0;0} °/s)";

        /// <summary>Makes the current direction heading 0.</summary>
        public void ResetHeading() => totalRotation = 0f;

        protected override void Sample()
        {
            Quaternion rot = SensorRotation;
            Vector3 f = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up);
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;   // Unity yaw: clockwise positive
            float now = Time.fixedTime;
            if (hasLast)
            {
                float dt = Mathf.Max(1e-5f, now - lastTime);
                float delta = -Mathf.DeltaAngle(lastYaw, yaw);     // anticlockwise positive
                delta += (driftDegreesPerSecond + Noise()) * dt;
                totalRotation += delta;
                YawRate = delta / dt;
            }
            lastYaw = yaw;
            lastTime = now;
            hasLast = true;
            Raise(OnReading, Heading);
        }

        protected override void ClearEvents() => OnReading = null;

        internal override void ResetSensor()
        {
            base.ResetSensor();
            totalRotation = 0f;
            YawRate = 0f;
            hasLast = false;
        }
    }
}
