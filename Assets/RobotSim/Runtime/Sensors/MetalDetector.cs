using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Inductive metal detector. Senses <see cref="MetalObject"/>s within <see cref="Range"/> — even
    /// hidden ones under the floor — and reports a signal strength that grows as the coil gets closer.
    /// </summary>
    [AddComponentMenu("Robot Sim/Sensors/Metal Detector")]
    public class MetalDetector : Sensor
    {
        [Tooltip("Detection radius around the coil (m).")]
        [SerializeField] float range = 0.12f;
        [Tooltip("Strength above which Detected becomes true.")]
        [Range(0f, 1f)]
        [SerializeField] float threshold = 0.15f;

        static readonly Collider[] overlapBuffer = new Collider[32];

        /// <summary>Signal strength from 0 (nothing) to 1 (right on top of metal).</summary>
        public float Strength { get; private set; }

        /// <summary>True when <see cref="Strength"/> is above <see cref="Threshold"/>.</summary>
        public bool Detected { get; private set; }

        public float Range => range;

        public float Threshold
        {
            get => threshold;
            set => threshold = Mathf.Clamp01(value);
        }

        /// <summary>Fires after every sample with the signal strength.</summary>
        public event Action<float> OnReading;
        public event Action OnMetalDetected;
        public event Action OnMetalLost;

        public override string ReadingText => $"{Strength:F2} {(Detected ? "(METAL!)" : "")}";

        protected override void Sample()
        {
            Vector3 p = SensorPosition;
            int count = Physics.OverlapSphereNonAlloc(p, range, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
            float signal = 0f;
            for (int i = 0; i < count; i++)
            {
                var metal = overlapBuffer[i].GetComponentInParent<MetalObject>();
                if (metal == null) continue;
                float d = Vector3.Distance(p, overlapBuffer[i].ClosestPoint(p));
                float falloff = 1f - Mathf.Clamp01(d / range);
                signal += metal.Strength * falloff * falloff;
            }
            Strength = Mathf.Clamp01(signal + Noise());
            bool was = Detected;
            Detected = Strength > threshold;
            Raise(OnReading, Strength);
            if (Detected && !was) Raise(OnMetalDetected);
            if (!Detected && was) Raise(OnMetalLost);
        }

        protected override void DrawDebug()
        {
            Color c = Color.Lerp(new Color(0.8f, 0.6f, 0.2f, 0.3f), new Color(1f, 0.9f, 0f, 1f), Strength);
            const int segments = 16;
            float r = 0.028f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Lines.Line(new Vector3(Mathf.Cos(a0) * r, 0.004f, Mathf.Sin(a0) * r),
                           new Vector3(Mathf.Cos(a1) * r, 0.004f, Mathf.Sin(a1) * r), c, Detected ? 0.006f : 0.003f);
            }
        }

        protected override void ClearEvents()
        {
            OnReading = null;
            OnMetalDetected = null;
            OnMetalLost = null;
        }

        internal override void ResetSensor()
        {
            base.ResetSensor();
            Detected = false;
            Strength = 0f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
