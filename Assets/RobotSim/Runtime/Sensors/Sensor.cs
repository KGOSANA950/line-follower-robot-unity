using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Base class for all simulated sensors. A sensor samples the world <see cref="UpdateRateHz"/> times per
    /// simulated second. After each sample its value properties are updated and its events fire.
    /// The sensor's GameObject name is the name used in code: <c>GetSensor&lt;UltrasonicSensor&gt;("Front")</c>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public abstract class Sensor : MonoBehaviour
    {
        [Tooltip("Samples per simulated second.")]
        [Range(1f, 100f)]
        [SerializeField] float updateRateHz = 20f;

        [Tooltip("Standard deviation of Gaussian noise added to readings (0 = perfect sensor).")]
        [Min(0f)]
        [SerializeField] float noiseStdDev;

        [Tooltip("Draw the sensor's rays/readings in the Game view.")]
        [SerializeField] bool showDebug = true;

        /// <summary>Global switch for all sensor debug drawing (toggled with the G key).</summary>
        public static bool GlobalDebug = true;

        RigidMount mount;
        bool mounted;
        Robot robot;
        float accumulator = float.MaxValue;
        DebugLines lines;

        /// <summary>The sensor's name (its GameObject name).</summary>
        public string Name => gameObject.name;

        public float UpdateRateHz
        {
            get => updateRateHz;
            set => updateRateHz = Mathf.Clamp(value, 1f, 100f);
        }

        public float NoiseStdDev
        {
            get => noiseStdDev;
            set => noiseStdDev = Mathf.Max(0f, value);
        }

        public bool ShowDebug
        {
            get => showDebug;
            set => showDebug = value;
        }

        /// <summary>Simulation time of the most recent sample.</summary>
        public float LastSampleTime { get; private set; }

        /// <summary>Number of samples taken since the last reset.</summary>
        public int SampleCount { get; private set; }

        /// <summary>The current reading as text, for the HUD.</summary>
        public abstract string ReadingText { get; }

        protected Robot Robot => robot != null ? robot : (robot = GetComponentInParent<Robot>());
        protected Rigidbody Body { get { EnsureMounted(); return mount.Body; } }

        /// <summary>Current physics position of the sensor.</summary>
        protected Vector3 SensorPosition { get { EnsureMounted(); return mount.Position; } }

        /// <summary>Current physics rotation of the sensor.</summary>
        protected Quaternion SensorRotation { get { EnsureMounted(); return mount.Rotation; } }

        protected bool DebugVisible => showDebug && GlobalDebug;

        protected DebugLines Lines => lines != null ? lines : (lines = new DebugLines(transform));

        void EnsureMounted()
        {
            if (mounted && mount.IsBound) return;
            mounted = true;
            mount.Bind(transform);
        }

        protected virtual void Awake() => EnsureMounted();

        void FixedUpdate()
        {
            float period = 1f / updateRateHz;
            accumulator += Time.fixedDeltaTime;
            if (accumulator + 1e-5f < period)
                return;
            accumulator = accumulator > period * 2f ? 0f : accumulator - period;
            SampleNow();
        }

        /// <summary>Takes a reading immediately (normally called automatically at UpdateRateHz).</summary>
        public void SampleNow()
        {
            LastSampleTime = Time.fixedTime;
            SampleCount++;
            Sample();
            if (lines != null || DebugVisible)
            {
                Lines.Begin();
                if (DebugVisible) DrawDebug();
                Lines.End();
            }
        }

        /// <summary>Read the world and update readings; raise events.</summary>
        protected abstract void Sample();

        /// <summary>Draw debug lines using <see cref="Lines"/> (positions in the sensor's local space).</summary>
        protected virtual void DrawDebug() { }

        /// <summary>Remove all event subscribers (called on reset so a restarted program doesn't subscribe twice).</summary>
        protected abstract void ClearEvents();

        internal virtual void ResetSensor()
        {
            ClearEvents();
            accumulator = float.MaxValue;
            SampleCount = 0;
        }

        protected float Noise() => noiseStdDev > 0f ? SimPhysics.Gaussian() * noiseStdDev : 0f;

        /// <summary>Converts a world-space point to the sensor's local space using the physics pose.</summary>
        protected Vector3 ToLocal(Vector3 worldPoint) =>
            Quaternion.Inverse(SensorRotation) * (worldPoint - SensorPosition);

        protected void Raise<T>(Action<T> handler, T value)
        {
            if (handler == null || IsFaulted()) return;
            try { handler(value); }
            catch (Exception e) { Report(e); }
        }

        protected void Raise(Action handler)
        {
            if (handler == null || IsFaulted()) return;
            try { handler(); }
            catch (Exception e) { Report(e); }
        }

        bool IsFaulted() => Robot != null && Robot.ProgramFaulted;

        void Report(Exception e)
        {
            if (Robot != null) Robot.ReportProgramError(e, $"a \"{Name}\" sensor event handler");
            else Debug.LogException(e, this);
        }
    }
}
