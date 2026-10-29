using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// A row of line sensors across the front of the robot (like a QTR-5RC). Gives every sensor's
    /// reflectance plus a <see cref="Position"/> estimate of where the line is: -1 = far left, 0 = centre, +1 = far right.
    /// </summary>
    [AddComponentMenu("Robot Sim/Sensors/Line Sensor Array")]
    public class LineSensorArray : Sensor
    {
        [Range(2, 16)]
        [SerializeField] int sensorCount = 5;
        [Tooltip("Distance between neighbouring sensors (m).")]
        [SerializeField] float spacing = 0.015f;
        [SerializeField] float maxDistance = 0.05f;
        [Range(0f, 1f)]
        [SerializeField] float threshold = 0.3f;
        [SerializeField] float spotRadius = 0.004f;

        float[] values;
        bool[] onLine;
        float position;
        bool lineDetected;

        /// <summary>Reflectance of each sensor, left to right (0 = black, 1 = white).</summary>
        public float[] Values => values ??= new float[sensorCount];

        /// <summary>Number of sensors in the array.</summary>
        public int Count => sensorCount;

        /// <summary>Distance between neighbouring sensors in metres.</summary>
        public float Spacing => spacing;

        /// <summary>True for each sensor that currently sees the line.</summary>
        public bool IsOnLine(int index) => onLine != null && onLine[index];

        /// <summary>
        /// Where the line is under the array, from -1 (under the leftmost sensor) to +1 (rightmost).
        /// When the line is lost this keeps its last known value.
        /// </summary>
        public float Position => position;

        /// <summary>True when at least one sensor sees the line.</summary>
        public bool LineDetected => lineDetected;

        public float Threshold
        {
            get => threshold;
            set => threshold = Mathf.Clamp01(value);
        }

        /// <summary>Fires after every sample with all sensor values (left to right). Copy the array if you keep it.</summary>
        public event Action<float[]> OnReading;
        public event Action OnLineFound;
        public event Action OnLineLost;

        public override string ReadingText
        {
            get
            {
                var v = Values;
                var parts = new string[v.Length];
                for (int i = 0; i < v.Length; i++) parts[i] = v[i].ToString("F1");
                return $"[{string.Join(" ", parts)}] pos {(lineDetected ? position.ToString("+0.00;-0.00") : "lost")}";
            }
        }

        /// <summary>Local position of sensor <paramref name="i"/> relative to the array centre.</summary>
        Vector3 SensorOffset(int i) => Vector3.right * ((i - (sensorCount - 1) * 0.5f) * spacing);

        protected override void Sample()
        {
            var v = Values;
            if (onLine == null || onLine.Length != sensorCount) onLine = new bool[sensorCount];
            Vector3 origin = SensorPosition;
            Quaternion rot = SensorRotation;

            float weighted = 0f, total = 0f;
            for (int i = 0; i < sensorCount; i++)
            {
                float raw = LineSensor.ReadReflectance(Body, origin + rot * SensorOffset(i), rot, maxDistance, spotRadius);
                v[i] = Mathf.Clamp01(raw + Noise());
                onLine[i] = v[i] < threshold;
                // Darkness weight: 1 on black, falling to 0 at twice the threshold.
                float w = Mathf.Clamp01(1f - v[i] / (2f * threshold));
                float x = sensorCount == 1 ? 0f : (i / (float)(sensorCount - 1)) * 2f - 1f;
                weighted += w * x;
                total += w;
            }

            bool was = lineDetected;
            lineDetected = Array.IndexOf(onLine, true) >= 0;
            if (lineDetected && total > 0f)
                position = Mathf.Clamp(weighted / total, -1f, 1f);

            Raise(OnReading, v);
            if (lineDetected && !was) Raise(OnLineFound);
            if (!lineDetected && was) Raise(OnLineLost);
        }

        protected override void DrawDebug()
        {
            for (int i = 0; i < sensorCount; i++)
            {
                Color c = onLine[i] ? new Color(1f, 0.15f, 0.15f) : new Color(1f, 0.7f, 0.7f, 0.4f);
                Vector3 o = SensorOffset(i);
                Lines.Line(o, o + Vector3.down * 0.012f, c, 0.004f);
            }
        }

        protected override void ClearEvents()
        {
            OnReading = null;
            OnLineFound = null;
            OnLineLost = null;
        }

        internal override void ResetSensor()
        {
            base.ResetSensor();
            position = 0f;
            lineDetected = false;
        }
    }
}
