using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// 2D scanning lidar (like an RPLIDAR A1). Measures the distance in a full circle around the robot.
    /// <see cref="Ranges"/>[i] is the distance at angle <c>i * AngleIncrement</c> degrees, where 0° is straight ahead
    /// and angles increase anticlockwise seen from above (90° = left, 180° = behind, 270° = right).
    /// </summary>
    [AddComponentMenu("Robot Sim/Sensors/Lidar")]
    public class Lidar : Sensor
    {
        [Range(8, 1080)]
        [SerializeField] int raysPerScan = 360;
        [SerializeField] float minRange = 0.05f;
        [SerializeField] float maxRange = 6f;
        [SerializeField] Color pointColour = new Color(1f, 0.15f, 0.1f);

        float[] ranges;
        Mesh pointMesh;
        Vector3[] pointVertices;
        Color[] pointColours;
        MeshRenderer pointRenderer;

        /// <summary>Distance in metres for each ray. Rays that hit nothing read <see cref="MaxRange"/>.</summary>
        public float[] Ranges => ranges ??= CreateRanges();

        public int RaysPerScan => raysPerScan;

        /// <summary>Degrees between neighbouring rays.</summary>
        public float AngleIncrement => 360f / raysPerScan;

        public float MinRange => minRange;
        public float MaxRange => maxRange;

        /// <summary>Fires after every full scan. Copy the array if you keep it.</summary>
        public event Action<float[]> OnScan;

        public override string ReadingText
        {
            get
            {
                var r = Ranges;
                int n = r.Length;
                return $"front {r[0]:F2}  left {r[n / 4]:F2}  back {r[n / 2]:F2}  right {r[3 * n / 4]:F2}";
            }
        }

        /// <summary>The angle in degrees of ray <paramref name="index"/>.</summary>
        public float AngleOf(int index) => index * AngleIncrement;

        /// <summary>Distance at the ray closest to <paramref name="angleDegrees"/> (0 = ahead, 90 = left).</summary>
        public float GetRange(float angleDegrees)
        {
            float a = Mathf.Repeat(angleDegrees, 360f);
            int i = Mathf.RoundToInt(a / AngleIncrement) % raysPerScan;
            return Ranges[i];
        }

        float[] CreateRanges()
        {
            var r = new float[raysPerScan];
            for (int i = 0; i < r.Length; i++) r[i] = maxRange;
            return r;
        }

        protected override void Sample()
        {
            var r = Ranges;
            if (r.Length != raysPerScan) r = ranges = CreateRanges();
            Vector3 origin = SensorPosition;
            Quaternion rot = SensorRotation;
            Vector3 up = rot * Vector3.up;
            Vector3 forward = rot * Vector3.forward;
            float increment = AngleIncrement;

            for (int i = 0; i < raysPerScan; i++)
            {
                // Negative rotation about up = anticlockwise seen from above in Unity's left-handed frame.
                Vector3 dir = Quaternion.AngleAxis(-i * increment, up) * forward;
                float d = maxRange;
                if (SimPhysics.Raycast(Body, origin, dir, maxRange, out RaycastHit hit) && hit.distance >= minRange)
                    d = Mathf.Clamp(hit.distance + Noise(), minRange, maxRange);
                r[i] = d;
            }

            UpdatePointCloud();
            Raise(OnScan, r);
        }

        void UpdatePointCloud()
        {
            bool visible = DebugVisible;
            if (!visible)
            {
                if (pointRenderer != null) pointRenderer.enabled = false;
                return;
            }
            EnsurePointMesh();
            pointRenderer.enabled = true;
            const float half = 0.006f;
            float increment = AngleIncrement;
            for (int i = 0; i < raysPerScan; i++)
            {
                int v = i * 4;
                if (ranges[i] >= maxRange)
                {
                    pointVertices[v] = pointVertices[v + 1] = pointVertices[v + 2] = pointVertices[v + 3] = Vector3.zero;
                    continue;
                }
                Vector3 p = Quaternion.AngleAxis(-i * increment, Vector3.up) * Vector3.forward * ranges[i];
                pointVertices[v] = p + new Vector3(-half, 0f, -half);
                pointVertices[v + 1] = p + new Vector3(-half, 0f, half);
                pointVertices[v + 2] = p + new Vector3(half, 0f, half);
                pointVertices[v + 3] = p + new Vector3(half, 0f, -half);
            }
            pointMesh.vertices = pointVertices;
        }

        void EnsurePointMesh()
        {
            if (pointMesh != null && pointVertices.Length == raysPerScan * 4)
                return;
            var go = new GameObject("LidarPoints") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            pointRenderer = go.AddComponent<MeshRenderer>();
            pointRenderer.sharedMaterial = DebugLines.Material;
            pointRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pointRenderer.receiveShadows = false;
            pointMesh = new Mesh { name = "LidarPoints" };
            pointMesh.MarkDynamic();
            pointVertices = new Vector3[raysPerScan * 4];
            pointColours = new Color[raysPerScan * 4];
            var indices = new int[raysPerScan * 6];
            for (int i = 0; i < raysPerScan; i++)
            {
                int v = i * 4, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
                pointColours[v] = pointColours[v + 1] = pointColours[v + 2] = pointColours[v + 3] = pointColour;
            }
            pointMesh.vertices = pointVertices;
            pointMesh.colors = pointColours;
            pointMesh.triangles = indices;
            pointMesh.bounds = new Bounds(Vector3.zero, Vector3.one * maxRange * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = pointMesh;
        }

        protected override void ClearEvents() => OnScan = null;
    }
}
