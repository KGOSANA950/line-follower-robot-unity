using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Builds a maze arena: floor, walls, a blue start pad, a green goal pad, an optional black line along
    /// the solution (for line following) and hidden metal plates (for the metal detector).
    /// Change the settings and press "Generate" in the Inspector.
    /// </summary>
    [AddComponentMenu("Robot Sim/Maze Generator")]
    public class MazeGenerator : MonoBehaviour
    {
        public static readonly Color FloorColour = new Color(0.9f, 0.9f, 0.88f);
        public static readonly Color WallColour = new Color(0.35f, 0.45f, 0.6f);
        public static readonly Color LineColour = new Color(0.05f, 0.05f, 0.05f);
        public static readonly Color StartColour = new Color(0.15f, 0.35f, 0.95f);
        public static readonly Color GoalColour = new Color(0.1f, 0.75f, 0.2f);
        public static readonly Color MetalColour = new Color(0.75f, 0.75f, 0.8f);

        [Header("Layout")]
        [Range(2, 20)] [SerializeField] int width = 8;
        [Range(2, 20)] [SerializeField] int height = 8;
        [Tooltip("Size of each square cell (m).")]
        [SerializeField] float cellSize = 0.5f;
        [SerializeField] float wallHeight = 0.15f;
        [SerializeField] float wallThickness = 0.02f;
        [SerializeField] int seed = 12345;

        [Header("Features")]
        [Tooltip("Draw a black line along the route from start to goal.")]
        [SerializeField] bool drawSolutionLine = true;
        [SerializeField] float lineWidth = 0.02f;
        [Range(0, 20)] [SerializeField] int metalPlateCount = 4;
        [Tooltip("Show the metal plates (otherwise they are hidden under the floor).")]
        [SerializeField] bool revealMetal;

        const string ContainerName = "Generated";

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public int Seed { get => seed; set => seed = value; }
        MazeLayout layout;

        /// <summary>The wall layout for the current settings.</summary>
        public MazeLayout Layout
        {
            get
            {
                if (layout == null || layout.Width != width || layout.Height != height)
                    layout = MazeLayout.Generate(width, height, seed);
                return layout;
            }
            private set => layout = value;
        }

        public Vector2Int StartCell => Vector2Int.zero;
        public Vector2Int GoalCell => new Vector2Int(width - 1, height - 1);

        /// <summary>World-space size of the maze footprint.</summary>
        public Vector2 Size => new Vector2(width * cellSize, height * cellSize);

        public Vector3 StartPosition => CellCentre(StartCell);
        public Quaternion StartRotation => transform.rotation;
        public Vector3 GoalPosition => CellCentre(GoalCell);

        public Vector3 CellCentre(Vector2Int c) => transform.TransformPoint(LocalCellCentre(c));

        Vector3 LocalCellCentre(Vector2Int c) =>
            new Vector3((c.x + 0.5f) * cellSize - width * cellSize * 0.5f, 0f, (c.y + 0.5f) * cellSize - height * cellSize * 0.5f);

        /// <summary>Rebuilds the maze. <paramref name="materials"/> maps a colour to a material (null = runtime materials).</summary>
        public void Generate(Func<Color, Material> materials = null)
        {
            materials ??= SimMaterials.Get;
            Layout = MazeLayout.Generate(width, height, seed);

            Transform old = transform.Find(ContainerName);
            if (old != null) DestroyImmediate(old.gameObject);
            var container = new GameObject(ContainerName).transform;
            container.SetParent(transform, false);

            float w = width * cellSize, h = height * cellSize;
            const float margin = 0.3f;

            // Floor (top surface at y = 0).
            Box("Floor", container, new Vector3(0f, -0.01f, 0f), new Vector3(w + margin * 2f, 0.02f, h + margin * 2f),
                FloorColour, materials, surface: true);

            BuildWalls(container, materials);

            var path = Layout.FindPath(StartCell, GoalCell);
            if (drawSolutionLine && path != null)
                BuildLine(container, path, materials);

            // The start pad sits under the line (so a line follower can see the line from the start);
            // the goal pad sits on top of it (so the colour sensor sees green at the end).
            Box("StartPad", container, LocalCellCentre(StartCell) + Vector3.up * 0.0005f, new Vector3(cellSize * 0.6f, 0.001f, cellSize * 0.6f),
                StartColour, materials, surface: true, castShadows: false);
            Box("GoalPad", container, LocalCellCentre(GoalCell) + Vector3.up * 0.0035f, new Vector3(cellSize * 0.6f, 0.003f, cellSize * 0.6f),
                GoalColour, materials, surface: true, castShadows: false);

            BuildMetal(container, materials);
        }

        void BuildWalls(Transform container, Func<Color, Material> materials)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(container, false);
            float x0 = -width * cellSize * 0.5f, z0 = -height * cellSize * 0.5f;
            float y = wallHeight * 0.5f;

            // Horizontal wall lines (running east-west) at z = j * cellSize. Merge neighbouring segments.
            for (int j = 0; j <= height; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= width; i++)
                {
                    bool wall = i < width && (j == 0 || j == height || Layout.HasWall(new Vector2Int(i, j - 1), MazeDirection.North));
                    if (wall && runStart < 0) runStart = i;
                    if (!wall && runStart >= 0)
                    {
                        float len = (i - runStart) * cellSize + wallThickness;
                        float cx = x0 + (runStart + i) * 0.5f * cellSize;
                        Box("Wall", walls, new Vector3(cx, y, z0 + j * cellSize), new Vector3(len, wallHeight, wallThickness), WallColour, materials, surface: true);
                        runStart = -1;
                    }
                }
            }

            // Vertical wall lines (running north-south) at x = i * cellSize.
            for (int i = 0; i <= width; i++)
            {
                int runStart = -1;
                for (int j = 0; j <= height; j++)
                {
                    bool wall = j < height && (i == 0 || i == width || Layout.HasWall(new Vector2Int(i - 1, j), MazeDirection.East));
                    if (wall && runStart < 0) runStart = j;
                    if (!wall && runStart >= 0)
                    {
                        float len = (j - runStart) * cellSize + wallThickness;
                        float cz = z0 + (runStart + j) * 0.5f * cellSize;
                        Box("Wall", walls, new Vector3(x0 + i * cellSize, y, cz), new Vector3(wallThickness, wallHeight, len), WallColour, materials, surface: true);
                        runStart = -1;
                    }
                }
            }
        }

        void BuildLine(Transform container, List<Vector2Int> path, Func<Color, Material> materials)
        {
            var line = new GameObject("Line").transform;
            line.SetParent(container, false);
            const float thickness = 0.002f;
            for (int k = 0; k < path.Count - 1; k++)
            {
                Vector3 a = LocalCellCentre(path[k]), b = LocalCellCentre(path[k + 1]);
                Vector3 mid = (a + b) * 0.5f + Vector3.up * (0.001f + thickness * 0.5f);
                bool alongX = Mathf.Abs(a.x - b.x) > 0.001f;
                Vector3 size = alongX
                    ? new Vector3(cellSize + lineWidth, thickness, lineWidth)
                    : new Vector3(lineWidth, thickness, cellSize + lineWidth);
                Box("LineSegment", line, mid, size, LineColour, materials, surface: true, castShadows: false);
            }
        }

        void BuildMetal(Transform container, Func<Color, Material> materials)
        {
            if (metalPlateCount <= 0) return;
            var metal = new GameObject("Metal").transform;
            metal.SetParent(container, false);
            var rng = new System.Random(seed * 31 + 7);
            var used = new HashSet<Vector2Int> { StartCell, GoalCell };
            int target = Mathf.Min(metalPlateCount, width * height - 2);
            int guard = 0;
            while (used.Count - 2 < target && guard++ < 1000)
            {
                var cell = new Vector2Int(rng.Next(width), rng.Next(height));
                if (!used.Add(cell)) continue;
                // Offset within the cell so plates aren't always dead centre.
                Vector3 offset = new Vector3((float)(rng.NextDouble() - 0.5) * cellSize * 0.4f, 0f, (float)(rng.NextDouble() - 0.5) * cellSize * 0.4f);
                Vector3 pos = LocalCellCentre(cell) + offset + (revealMetal ? Vector3.up * 0.0034f : Vector3.down * 0.004f);
                var plate = Box("MetalPlate", metal, pos, new Vector3(0.08f, revealMetal ? 0.0004f : 0.004f, 0.08f),
                    MetalColour, materials, surface: false, castShadows: false);
                plate.GetComponent<Collider>().isTrigger = true;
                plate.AddComponent<MetalObject>();
                plate.GetComponent<Renderer>().enabled = revealMetal;
            }
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Color colour,
            Func<Color, Material> materials, bool surface, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = materials(colour);
            if (!castShadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (surface) go.AddComponent<SurfaceProperties>().Colour = colour;
            return go;
        }
    }
}
