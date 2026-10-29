using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Downward-facing infrared reflectance sensor (like a TCRT5000). Reports how much light the floor
    /// reflects: about 0.05 on a black line and about 0.9 on a white floor.
    /// </summary>
    [AddComponentMenu("Robot Sim/Sensors/Line Sensor")]
    public class LineSensor : Sensor
    {
        [Tooltip("How far below the sensor the floor can be and still be read (m).")]
        [SerializeField] float maxDistance = 0.05f;
        [Tooltip("Readings below this count as 'on the line'.")]
        [Range(0f, 1f)]
        [SerializeField] float threshold = 0.3f;
        [Tooltip("Radius of the spot of floor the sensor sees (m).")]
        [SerializeField] float spotRadius = 0.004f;

        /// <summary>Reflected light from 0 (black / nothing below) to 1 (bright white).</summary>
        public float Reflectance { get; private set; }

        /// <summary>True when <see cref="Reflectance"/> is below <see cref="Threshold"/>.</summary>
        public bool OnLine { get; private set; }

        public float Threshold
        {
            get => threshold;
            set => threshold = Mathf.Clamp01(value);
        }

        /// <summary>Fires after every sample with the reflectance.</summary>
        public event Action<float> OnReading;
        /// <summary>Fires when the sensor moves onto a line.</summary>
        public event Action OnLineEnter;
        /// <summary>Fires when the sensor moves off a line.</summary>
        public event Action OnLineLost;

        public override string ReadingText => $"{Reflectance:F2} {(OnLine ? "(LINE)" : "")}";

        protected override void Sample()
        {
            float raw = ReadReflectance(Body, SensorPosition, SensorRotation, maxDistance, spotRadius);
            Reflectance = Mathf.Clamp01(raw + Noise());
            bool was = OnLine;
            OnLine = Reflectance < threshold;
            Raise(OnReading, Reflectance);
            if (OnLine && !was) Raise(OnLineEnter);
            if (!OnLine && was) Raise(OnLineLost);
        }

        protected override void DrawDebug()
        {
            Color c = OnLine ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.6f, 0.6f, 0.5f);
            Lines.Line(Vector3.zero, Vector3.down * maxDistance * 0.5f, c, 0.003f);
        }

        protected override void ClearEvents()
        {
            OnReading = null;
            OnLineEnter = null;
            OnLineLost = null;
        }

        /// <summary>Average reflectance across a small spot directly below a point.</summary>
        internal static float ReadReflectance(Rigidbody body, Vector3 position, Quaternion rotation, float maxDistance, float spotRadius)
        {
            Vector3 down = rotation * Vector3.down;
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            const int samples = 5;
            float total = 0f;
            for (int i = 0; i < samples; i++)
            {
                Vector3 offset = i switch
                {
                    1 => right * spotRadius,
                    2 => -right * spotRadius,
                    3 => forward * spotRadius,
                    4 => -forward * spotRadius,
                    _ => Vector3.zero
                };
                if (SimPhysics.Raycast(body, position + offset, down, maxDistance, out RaycastHit hit))
                {
                    SimPhysics.ReadSurface(hit.collider, out _, out float r);
                    total += r;
                }
            }
            return total / samples;
        }
    }
}
