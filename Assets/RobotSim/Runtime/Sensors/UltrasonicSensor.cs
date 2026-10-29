using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Simulated ultrasonic range finder (like an HC-SR04). Sends a fan of rays across its beam
    /// and reports the distance to the nearest object in metres.
    /// </summary>
    [AddComponentMenu("Robot Sim/Sensors/Ultrasonic Sensor")]
    public class UltrasonicSensor : Sensor
    {
        [SerializeField] float minRange = 0.02f;
        [SerializeField] float maxRange = 4f;
        [Tooltip("Half-width of the beam in degrees.")]
        [Range(0f, 45f)]
        [SerializeField] float beamHalfAngle = 15f;
        [Range(1, 15)]
        [SerializeField] int rayCount = 7;

        float distance;
        readonly float[] rayHits = new float[15];

        /// <summary>Distance to the nearest object in metres. Equals <see cref="MaxRange"/> when nothing is in range.</summary>
        public float Distance => distance;

        /// <summary>True when an object was detected within range.</summary>
        public bool InRange { get; private set; }

        public float MinRange => minRange;
        public float MaxRange => maxRange;

        /// <summary>Fires after every sample with the distance in metres.</summary>
        public event Action<float> OnReading;

        public override string ReadingText => InRange ? $"{distance:F3} m" : "--- (nothing in range)";

        protected override void Awake()
        {
            base.Awake();
            distance = maxRange;
        }

        protected override void Sample()
        {
            Vector3 origin = SensorPosition;
            Quaternion rot = SensorRotation;
            Vector3 up = rot * Vector3.up;
            Vector3 forward = rot * Vector3.forward;
            float nearest = float.MaxValue;
            int n = Mathf.Clamp(rayCount, 1, rayHits.Length);

            for (int i = 0; i < n; i++)
            {
                float angle = n == 1 ? 0f : Mathf.Lerp(-beamHalfAngle, beamHalfAngle, i / (float)(n - 1));
                Vector3 dir = Quaternion.AngleAxis(angle, up) * forward;
                if (SimPhysics.Raycast(Body, origin, dir, maxRange, out RaycastHit hit))
                {
                    rayHits[i] = hit.distance;
                    nearest = Mathf.Min(nearest, hit.distance);
                }
                else
                {
                    rayHits[i] = float.MaxValue;
                }
            }

            InRange = nearest <= maxRange;
            distance = InRange ? Mathf.Clamp(nearest + Noise(), minRange, maxRange) : maxRange;
            Raise(OnReading, distance);
        }

        protected override void DrawDebug()
        {
            int n = Mathf.Clamp(rayCount, 1, rayHits.Length);
            float shown = InRange ? distance : Mathf.Min(maxRange, 0.5f);
            Color colour = InRange ? new Color(0.2f, 1f, 0.3f, 0.8f) : new Color(0.6f, 0.6f, 0.6f, 0.4f);
            float a = n == 1 ? 0f : beamHalfAngle;
            Lines.Line(Vector3.zero, Quaternion.Euler(0f, -a, 0f) * Vector3.forward * shown, colour, 0.002f);
            Lines.Line(Vector3.zero, Quaternion.Euler(0f, a, 0f) * Vector3.forward * shown, colour, 0.002f);
            if (InRange)
            {
                Vector3 left = Quaternion.Euler(0f, -a, 0f) * Vector3.forward * shown;
                Vector3 right = Quaternion.Euler(0f, a, 0f) * Vector3.forward * shown;
                Lines.Line(left, right, colour, 0.004f);
            }
        }

        protected override void ClearEvents() => OnReading = null;
    }
}
