using System.IO;
using RobotSim.Examples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSim.EditorTools
{
    /// <summary>The "Robot Sim" menu: build the arena scene, create robots, add sensors.</summary>
    public static class RobotSimMenu
    {
        const string Root = "Assets/RobotSim";
        const string MaterialFolder = Root + "/Materials";
        const string PrefabFolder = Root + "/Prefabs";
        const string ScenePath = "Assets/Scenes/MazeArena.unity";

        // ---- Materials saved as assets so scenes and prefabs keep them ----

        public static Material MaterialAsset(Color colour)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/M_{ColorUtility.ToHtmlStringRGB(colour)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = SimMaterials.Create(colour);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---- Menu items ----

        [MenuItem("Robot Sim/Build Maze Arena Scene", priority = 0)]
        static void BuildSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildMazeArenaScene();
        }

        /// <summary>Creates (or rebuilds) Assets/Scenes/MazeArena.unity with the maze, robot, camera and HUD.</summary>
        public static void BuildMazeArenaScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.01f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 4.5f, -3.5f), Quaternion.Euler(52f, 0f, 0f));
            cam.gameObject.AddComponent<SimCameraRig>();

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                light.shadows = LightShadows.Soft;
                light.intensity = 1.6f;
            }

            var mazeGo = new GameObject("Maze");
            var maze = mazeGo.AddComponent<MazeGenerator>();
            maze.Generate(MaterialAsset);

            var prefabs = BuildAllRobotPrefabs(); // always start from freshly built robots
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[(int)RobotType.Differential]);
            robot.transform.SetPositionAndRotation(maze.StartPosition, maze.StartRotation);

            var sim = new GameObject("Simulation");
            var manager = sim.AddComponent<SimManager>();
            manager.Configure(maze);
            manager.ConfigureRobotPrefabs(prefabs);
            sim.AddComponent<TelemetryHUD>();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            Selection.activeGameObject = robot;
            Debug.Log($"Robot Sim: built {ScenePath}. Press Play, drive with WASD, and press 1-5 to try each drive type.");
        }

        // ---- Robots ----

        static string PrefabPath(RobotType type) => $"{PrefabFolder}/{type}Robot.prefab";

        [MenuItem("Robot Sim/Create Robot/Differential Drive", priority = 20)]
        static void CreateDifferential() => CreateRobotMenu(RobotType.Differential);

        [MenuItem("Robot Sim/Create Robot/Single Wheel Drive", priority = 21)]
        static void CreateSingleWheel() => CreateRobotMenu(RobotType.SingleWheel);

        [MenuItem("Robot Sim/Create Robot/Synchro Drive", priority = 22)]
        static void CreateSynchro() => CreateRobotMenu(RobotType.Synchro);

        [MenuItem("Robot Sim/Create Robot/Ackermann Steering", priority = 23)]
        static void CreateAckermann() => CreateRobotMenu(RobotType.Ackermann);

        [MenuItem("Robot Sim/Create Robot/Tracked", priority = 24)]
        static void CreateTracked() => CreateRobotMenu(RobotType.Tracked);

        static void CreateRobotMenu(RobotType type)
        {
            var robot = CreateRobotInstance(type);
            var maze = Object.FindAnyObjectByType<MazeGenerator>();
            if (maze != null) robot.transform.SetPositionAndRotation(maze.StartPosition, maze.StartRotation);
            Undo.RegisterCreatedObjectUndo(robot, "Create Robot");
            Selection.activeGameObject = robot;
            if (Object.FindObjectsByType<Robot>(FindObjectsInactive.Exclude).Length > 1)
                Debug.LogWarning("Robot Sim: there is more than one robot in the scene. Delete the old one unless you want several.");
        }

        [MenuItem("Robot Sim/Rebuild Robot Prefabs", priority = 30)]
        static void RebuildPrefabsMenu()
        {
            var prefabs = BuildAllRobotPrefabs();
            var manager = Object.FindAnyObjectByType<SimManager>();
            if (manager != null)
            {
                Undo.RecordObject(manager, "Assign Robot Prefabs");
                manager.ConfigureRobotPrefabs(prefabs);
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
            Debug.Log($"Robot Sim: rebuilt {prefabs.Length} robot prefabs in {PrefabFolder}");
        }

        /// <summary>Instantiates a robot prefab (building it first if needed).</summary>
        public static GameObject CreateRobotInstance(RobotType type = RobotType.Differential)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(type));
            if (prefab == null) prefab = BuildRobotPrefab(type);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        public static GameObject[] BuildAllRobotPrefabs()
        {
            var types = (RobotType[])System.Enum.GetValues(typeof(RobotType));
            var prefabs = new GameObject[types.Length];
            foreach (var t in types) prefabs[(int)t] = BuildRobotPrefab(t);
            return prefabs;
        }

        static GameObject BuildRobotPrefab(RobotType type)
        {
            var robot = RobotFactory.Create(type, MaterialAsset);
            // Keyboard teleop for driving by hand. Untick it when you add your own program.
            robot.AddComponent<KeyboardTeleop>();

            EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(robot, PrefabPath(type));
            Object.DestroyImmediate(robot);
            return prefab;
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == path) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---- Add Sensor ----

        static Transform SelectedRobot()
        {
            var go = Selection.activeGameObject;
            var robot = go != null ? go.GetComponentInParent<Robot>() : null;
            return robot != null ? robot.transform : null;
        }

        static bool HasRobotSelected() => SelectedRobot() != null;

        static void AddSensor(System.Func<Transform, Sensor> create)
        {
            var robot = SelectedRobot();
            var sensor = create(robot);
            Undo.RegisterCreatedObjectUndo(sensor.gameObject, "Add Sensor");
            Selection.activeGameObject = sensor.gameObject;
            Debug.Log($"Added {sensor.GetType().Name} \"{sensor.name}\". Rename the GameObject to change the name used in code, and move it to reposition the sensor.");
        }

        static string UniqueName(Transform robot, string baseName)
        {
            string name = baseName;
            for (int i = 2; robot.Find(name) != null; i++) name = baseName + i;
            return name;
        }

        [MenuItem("Robot Sim/Add Sensor/Ultrasonic", priority = 40)]
        static void AddUltrasonic() => AddSensor(r => RobotFactory.AddUltrasonic(r, UniqueName(r, "Ultrasonic"), new Vector3(0f, 0.05f, 0.08f), 0f, MaterialAsset));

        [MenuItem("Robot Sim/Add Sensor/Line Sensor", priority = 41)]
        static void AddLine() => AddSensor(r => RobotFactory.AddLineSensor(r, UniqueName(r, "Line"), new Vector3(0f, 0.015f, 0.075f), MaterialAsset));

        [MenuItem("Robot Sim/Add Sensor/Line Sensor Array", priority = 42)]
        static void AddLineArray() => AddSensor(r => RobotFactory.AddLineSensorArray(r, UniqueName(r, "LineArray"), new Vector3(0f, 0.015f, 0.075f), MaterialAsset));

        [MenuItem("Robot Sim/Add Sensor/Colour Sensor", priority = 43)]
        static void AddColour() => AddSensor(r => RobotFactory.AddColourSensor(r, UniqueName(r, "Colour"), new Vector3(0f, 0.015f, 0.035f), MaterialAsset));

        [MenuItem("Robot Sim/Add Sensor/Metal Detector", priority = 44)]
        static void AddMetal() => AddSensor(r => RobotFactory.AddMetalDetector(r, UniqueName(r, "Metal"), new Vector3(0f, 0.012f, 0.11f), MaterialAsset));

        [MenuItem("Robot Sim/Add Sensor/Lidar", priority = 45)]
        static void AddLidar() => AddSensor(r => RobotFactory.AddLidar(r, UniqueName(r, "Lidar"), new Vector3(0f, 0.095f, -0.02f), MaterialAsset));

        [MenuItem("Robot Sim/Add Sensor/Gyro", priority = 46)]
        static void AddGyro() => AddSensor(r => RobotFactory.AddGyro(r, UniqueName(r, "Gyro"), new Vector3(0f, 0.08f, -0.06f), MaterialAsset));

        [MenuItem("Robot Sim/Add Sensor/Ultrasonic", true)]
        [MenuItem("Robot Sim/Add Sensor/Line Sensor", true)]
        [MenuItem("Robot Sim/Add Sensor/Line Sensor Array", true)]
        [MenuItem("Robot Sim/Add Sensor/Colour Sensor", true)]
        [MenuItem("Robot Sim/Add Sensor/Metal Detector", true)]
        [MenuItem("Robot Sim/Add Sensor/Lidar", true)]
        [MenuItem("Robot Sim/Add Sensor/Gyro", true)]
        static bool ValidateAddSensor() => HasRobotSelected();

        [MenuItem("Robot Sim/Open Student Guide", priority = 100)]
        static void OpenGuide()
        {
            var guide = AssetDatabase.LoadAssetAtPath<Object>(Root + "/Docs/StudentGuide.md");
            if (guide != null) AssetDatabase.OpenAsset(guide);
        }
    }

    [CustomEditor(typeof(MazeGenerator))]
    public class MazeGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var maze = (MazeGenerator)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate")) Regenerate(maze);
                if (GUILayout.Button("Random Seed + Generate"))
                {
                    Undo.RecordObject(maze, "Random Maze Seed");
                    maze.Seed = Random.Range(0, 1000000);
                    Regenerate(maze);
                }
            }
            EditorGUILayout.HelpBox("The robot starts on the blue pad (south-west corner) facing north. The green pad is the goal.", MessageType.Info);
        }

        static void Regenerate(MazeGenerator maze)
        {
            if (Application.isPlaying)
                maze.Generate();
            else
            {
                maze.Generate(RobotSimMenu.MaterialAsset);
                EditorSceneManager.MarkSceneDirty(maze.gameObject.scene);
            }
        }
    }
}
