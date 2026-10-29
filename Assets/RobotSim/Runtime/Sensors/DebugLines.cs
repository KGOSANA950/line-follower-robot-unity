using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Draws sensor rays in the Game view using pooled LineRenderers parented to the sensor
    /// (positions are in the sensor's local space, so the lines move smoothly with the robot).
    /// </summary>
    public class DebugLines
    {
        static Material sharedMaterial;

        readonly Transform parent;
        readonly List<LineRenderer> pool = new List<LineRenderer>();
        Transform container;
        int used;

        public DebugLines(Transform parent) => this.parent = parent;

        internal static Material Material
        {
            get
            {
                if (sharedMaterial == null)
                {
                    var shader = Shader.Find("Sprites/Default");
                    sharedMaterial = new Material(shader) { name = "RobotSim Debug Lines" };
                }
                return sharedMaterial;
            }
        }

        public void Begin() => used = 0;

        public void Line(Vector3 localFrom, Vector3 localTo, Color colour, float width = 0.003f)
        {
            if (container == null)
            {
                var go = new GameObject("DebugLines") { hideFlags = HideFlags.DontSave };
                container = go.transform;
                container.SetParent(parent, false);
            }
            LineRenderer lr;
            if (used < pool.Count)
            {
                lr = pool[used];
            }
            else
            {
                var go = new GameObject("Line") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(container, false);
                lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.sharedMaterial = Material;
                lr.positionCount = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 0;
                pool.Add(lr);
            }
            used++;
            lr.enabled = true;
            lr.startWidth = lr.endWidth = width;
            lr.startColor = lr.endColor = colour;
            lr.SetPosition(0, localFrom);
            lr.SetPosition(1, localTo);
        }

        public void End()
        {
            for (int i = used; i < pool.Count; i++)
                if (pool[i].enabled) pool[i].enabled = false;
        }
    }
}
