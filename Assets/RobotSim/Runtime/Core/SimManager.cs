using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSim
{
    /// <summary>
    /// Runs the simulation: sets the physics rate and handles the global keys
    /// (R reset, Space pause, +/- time scale, G sensor rays, H HUD).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("Robot Sim/Sim Manager")]
    public class SimManager : MonoBehaviour
    {
        [Tooltip("Physics steps per simulated second.")]
        [SerializeField] float physicsRateHz = 100f;
        [SerializeField] MazeGenerator maze;
        [Tooltip("Generate a new random maze every time Play is pressed.")]
        [SerializeField] bool randomMazeOnPlay;

        [Header("Marking")]
        [Tooltip("Simulated seconds allowed to reach the goal pad. Shown in the HUD.")]
        [SerializeField] float runTimeLimit = 600f;

        [Header("Robot switching")]
        [Tooltip("Let the number keys 1-5 swap the robot for another drive type while playing.")]
        [SerializeField] bool allowRobotSwitching = true;
        [Tooltip("Robot prefabs in RobotType order: Differential, Single Wheel, Synchro, Ackermann, Tracked.")]
        [SerializeField] GameObject[] robotPrefabs = new GameObject[0];

        static readonly float[] TimeScales = { 0.25f, 0.5f, 1f, 2f, 4f };
        int timeScaleIndex = 2;

        public static SimManager Instance { get; private set; }
        public bool Paused { get; private set; }
        public float TimeScale => TimeScales[timeScaleIndex];
        public MazeGenerator Maze => maze;

        public void Configure(MazeGenerator mazeGenerator) => maze = mazeGenerator;

        public void ConfigureRobotPrefabs(GameObject[] prefabs) => robotPrefabs = prefabs;

        public IReadOnlyList<GameObject> RobotPrefabs => robotPrefabs;

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = 1f / physicsRateHz;
            ApplyTimeScale();
        }

        float runStart;

        /// <summary>Simulated seconds since Play was pressed or the simulation was reset.</summary>
        public float RunTime => Time.time - runStart;
        public float RunTimeLimit => runTimeLimit;

        /// <summary>Run time at which the robot first reached the goal pad, or -1 if it hasn't yet.</summary>
        public float GoalTime { get; private set; } = -1f;

        void ResetRun()
        {
            runStart = Time.time;
            GoalTime = -1f;
        }

        void FixedUpdate()
        {
            // Independent check of the goal (doesn't rely on the student's program).
            if (maze == null || GoalTime >= 0f || Robot.All.Count == 0) return;
            Vector3 d = maze.transform.InverseTransformPoint(Robot.All[0].Body.position) -
                        maze.transform.InverseTransformPoint(maze.GoalPosition);
            float half = maze.CellSize * 0.3f;
            if (Mathf.Abs(d.x) <= half && Mathf.Abs(d.z) <= half)
            {
                GoalTime = RunTime;
                RobotConsole.Log($"[SIM] Robot reached the goal pad at t = {GoalTime:F1} s");
            }
        }

        void Start()
        {
            ResetRun();
            if (maze != null && randomMazeOnPlay)
            {
                maze.Seed = Random.Range(0, 1000000);
                maze.Generate();
                foreach (var robot in Robot.All)
                {
                    robot.SetStartPose(maze.StartPosition, maze.StartRotation);
                    robot.ResetRobot();
                }
                RobotConsole.Log($"Generated maze with seed {maze.Seed}");
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) ResetSimulation();
            if (kb.spaceKey.wasPressedThisFrame) SetPaused(!Paused);
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) ChangeTimeScale(1);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) ChangeTimeScale(-1);
            if (kb.gKey.wasPressedThisFrame) Sensor.GlobalDebug = !Sensor.GlobalDebug;
            if (allowRobotSwitching)
            {
                if (kb.digit1Key.wasPressedThisFrame) SwitchRobot(RobotType.Differential);
                if (kb.digit2Key.wasPressedThisFrame) SwitchRobot(RobotType.SingleWheel);
                if (kb.digit3Key.wasPressedThisFrame) SwitchRobot(RobotType.Synchro);
                if (kb.digit4Key.wasPressedThisFrame) SwitchRobot(RobotType.Ackermann);
                if (kb.digit5Key.wasPressedThisFrame) SwitchRobot(RobotType.Tracked);
            }
        }

        /// <summary>Resets every robot to its start pose and restarts its program.</summary>
        public void ResetSimulation()
        {
            RobotConsole.Clear();
            RobotTelemetry.Clear();
            foreach (var robot in Robot.All)
                robot.ResetRobot();
            ResetRun();
            RobotConsole.Log("Simulation reset");
        }

        /// <summary>Replaces the current robot with a fresh one of another drive type, at the start position.</summary>
        public void SwitchRobot(RobotType type)
        {
            int index = (int)type;
            if (index >= robotPrefabs.Length || robotPrefabs[index] == null)
            {
                RobotConsole.Log($"No prefab set up for {RobotFactory.DisplayName(type)}.", LogType.Warning);
                return;
            }

            Pose start = new Pose(Vector3.zero, Quaternion.identity);
            if (maze != null) start = new Pose(maze.StartPosition, maze.StartRotation);
            else if (Robot.All.Count > 0) start = Robot.All[0].StartPose;

            for (int i = Robot.All.Count - 1; i >= 0; i--)
                Destroy(Robot.All[i].gameObject);

            Instantiate(robotPrefabs[index], start.position, start.rotation);
            RobotConsole.Clear();
            RobotTelemetry.Clear();
            ResetRun();
            RobotConsole.Log($"Switched to {RobotFactory.DisplayName(type)}");
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            ApplyTimeScale();
        }

        public void ChangeTimeScale(int direction)
        {
            timeScaleIndex = Mathf.Clamp(timeScaleIndex + direction, 0, TimeScales.Length - 1);
            ApplyTimeScale();
        }

        void ApplyTimeScale() => Time.timeScale = Paused ? 0f : TimeScale;
    }
}
