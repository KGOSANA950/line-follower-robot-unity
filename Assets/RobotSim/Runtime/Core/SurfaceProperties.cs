using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Describes how a surface appears to the robot's downward-facing sensors
    /// (line sensors read <see cref="Reflectivity"/>, colour sensors read <see cref="Colour"/>).
    /// Surfaces without this component read as mid-grey.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceProperties : MonoBehaviour
    {
        [SerializeField] Color colour = Color.white;

        [Tooltip("Derive reflectivity from the colour's brightness. Untick to set it manually.")]
        [SerializeField] bool autoReflectivity = true;

        [Range(0f, 1f)]
        [SerializeField] float reflectivity = 0.8f;

        public Color Colour
        {
            get => colour;
            set => colour = value;
        }

        /// <summary>0 = absorbs all light (black), 1 = reflects all light (white).</summary>
        public float Reflectivity => autoReflectivity ? colour.grayscale : reflectivity;

        public void SetReflectivity(float value)
        {
            autoReflectivity = false;
            reflectivity = Mathf.Clamp01(value);
        }

        /// <summary>The surface read when a ray hits something without SurfaceProperties.</summary>
        internal static readonly Color DefaultColour = new Color(0.5f, 0.5f, 0.5f);
        internal const float DefaultReflectivity = 0.5f;
    }
}
