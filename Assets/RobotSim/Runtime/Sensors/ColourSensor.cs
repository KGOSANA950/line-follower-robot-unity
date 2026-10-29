using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>Simple colour names a <see cref="ColourSensor"/> can recognise.</summary>
    public enum NamedColour
    {
        None,
        Black,
        White,
        Grey,
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple
    }

    /// <summary>
    /// Downward-facing colour sensor (like a TCS34725). Reports the RGB colour of the floor below it
    /// and the nearest <see cref="NamedColour"/>.
    /// </summary>
    [AddComponentMenu("Robot Sim/Sensors/Colour Sensor")]
    public class ColourSensor : Sensor
    {
        [SerializeField] float maxDistance = 0.05f;

        /// <summary>Measured RGB colour (each channel 0..1). Black when nothing is below the sensor.</summary>
        public Color Colour { get; private set; }

        /// <summary>The closest named colour.</summary>
        public NamedColour Named { get; private set; }

        public float Red => Colour.r;
        public float Green => Colour.g;
        public float Blue => Colour.b;

        /// <summary>Fires after every sample with the measured colour.</summary>
        public event Action<Color> OnReading;

        /// <summary>Fires when the named colour changes (e.g. from White to Green).</summary>
        public event Action<NamedColour> OnColourChanged;

        public override string ReadingText => $"{Named} (R{Colour.r:F2} G{Colour.g:F2} B{Colour.b:F2})";

        protected override void Sample()
        {
            Color c = Color.black;
            bool hasSurface = false;
            if (SimPhysics.Raycast(Body, SensorPosition, SensorRotation * Vector3.down, maxDistance, out RaycastHit hit))
            {
                SimPhysics.ReadSurface(hit.collider, out c, out _);
                hasSurface = true;
            }
            c = new Color(Mathf.Clamp01(c.r + Noise()), Mathf.Clamp01(c.g + Noise()), Mathf.Clamp01(c.b + Noise()), 1f);
            Colour = c;
            NamedColour previous = Named;
            Named = hasSurface ? Classify(c) : NamedColour.None;
            Raise(OnReading, c);
            if (Named != previous) Raise(OnColourChanged, Named);
        }

        protected override void DrawDebug()
        {
            Color shown = Colour;
            shown.a = 1f;
            Lines.Line(Vector3.zero, Vector3.down * 0.012f, shown, 0.008f);
        }

        protected override void ClearEvents()
        {
            OnReading = null;
            OnColourChanged = null;
        }

        internal override void ResetSensor()
        {
            base.ResetSensor();
            Named = NamedColour.None;
        }

        /// <summary>Maps an RGB colour to the nearest simple colour name.</summary>
        public static NamedColour Classify(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.2f) return NamedColour.Black;
            if (s < 0.25f) return v > 0.7f ? NamedColour.White : NamedColour.Grey;
            float hue = h * 360f;
            if (hue < 15f || hue >= 330f) return NamedColour.Red;
            if (hue < 45f) return NamedColour.Orange;
            if (hue < 70f) return NamedColour.Yellow;
            if (hue < 165f) return NamedColour.Green;
            if (hue < 260f) return NamedColour.Blue;
            return NamedColour.Purple;
        }
    }
}
