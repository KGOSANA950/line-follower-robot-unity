using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Marks an object as metallic so a <see cref="MetalDetector"/> can sense it.
    /// The object needs a collider (a trigger collider is fine, and lets you bury it under the floor).
    /// </summary>
    [DisallowMultipleComponent]
    public class MetalObject : MonoBehaviour
    {
        [Tooltip("How strongly this object shows up on a metal detector (1 = a typical steel plate).")]
        [Range(0f, 2f)]
        [SerializeField] float strength = 1f;

        public float Strength
        {
            get => strength;
            set => strength = Mathf.Max(0f, value);
        }
    }
}
