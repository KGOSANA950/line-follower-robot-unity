using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Creates simple coloured materials at runtime. The editor tools pass their own provider instead,
    /// which saves the materials as assets so scenes and prefabs keep them.
    /// </summary>
    public static class SimMaterials
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

        public static Material Get(Color colour)
        {
            if (cache.TryGetValue(colour, out var m) && m != null)
                return m;
            m = Create(colour);
            m.hideFlags = HideFlags.DontSave;
            cache[colour] = m;
            return m;
        }

        /// <summary>A new URP Lit material of the given colour (falls back to Standard outside URP).</summary>
        public static Material Create(Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = "RobotSim " + ColorUtility.ToHtmlStringRGB(colour) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);
            return m;
        }
    }
}
