using UnityEngine;

namespace RobotSim
{
    /// <summary>Physics queries shared by wheels and sensors.</summary>
    internal static class SimPhysics
    {
        static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        /// <summary>
        /// Raycast that ignores triggers and every collider belonging to <paramref name="self"/>,
        /// so a robot never detects its own body.
        /// </summary>
        public static bool Raycast(Rigidbody self, Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hitBuffer, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hitBuffer[i];
                if (self != null && h.collider.attachedRigidbody == self)
                    continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    hit = h;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Reads the colour and reflectivity of whatever a ray hit.</summary>
        public static void ReadSurface(Collider collider, out Color colour, out float reflectivity)
        {
            var surface = collider.GetComponentInParent<SurfaceProperties>();
            if (surface != null)
            {
                colour = surface.Colour;
                reflectivity = surface.Reflectivity;
            }
            else
            {
                colour = SurfaceProperties.DefaultColour;
                reflectivity = SurfaceProperties.DefaultReflectivity;
            }
        }

        /// <summary>Standard normal random number (mean 0, standard deviation 1).</summary>
        public static float Gaussian()
        {
            float u1 = 1f - Random.value;
            float u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }

    /// <summary>
    /// Caches a transform's pose relative to its rigidbody so FixedUpdate code can compute the
    /// true physics pose (Transform values are interpolated and lag behind the physics state).
    /// </summary>
    internal struct RigidMount
    {
        Rigidbody body;
        Transform transform;
        Vector3 localPosition;
        Quaternion localRotation;

        public Rigidbody Body => body;

        /// <summary>False after a script hot-reload during Play mode (non-serialized state is lost).</summary>
        public bool IsBound => transform != null;

        public void Bind(Transform t)
        {
            transform = t;
            body = t.GetComponentInParent<Rigidbody>();
            if (body != null)
            {
                Transform root = body.transform;
                localPosition = root.InverseTransformPoint(t.position);
                localRotation = Quaternion.Inverse(root.rotation) * t.rotation;
            }
        }

        public Vector3 Position => body != null ? body.position + body.rotation * localPosition : transform.position;
        public Quaternion Rotation => body != null ? body.rotation * localRotation : transform.rotation;
    }
}
