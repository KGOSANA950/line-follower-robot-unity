using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSim
{
    /// <summary>
    /// On-screen telemetry: program status, motor power/speed/encoders, live sensor readings,
    /// a lidar mini-map, graphs from Plot() and the Log() console. Toggle with H.
    /// </summary>
    [AddComponentMenu("Robot Sim/Telemetry HUD")]
    public class TelemetryHUD : MonoBehaviour
    {
        [SerializeField] bool visible = true;
        [SerializeField] int consoleLines = 9;

        const float RefHeight = 1080f;
        const float PanelWidth = 430f;
        const float Line = 22f;

        Texture2D white;
        GUIStyle label, small, header, consoleStyle;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) visible = !visible;
        }

        void EnsureStyles()
        {
            if (white != null) return;
            white = Texture2D.whiteTexture;
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, clipping = TextClipping.Clip };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 13 };
            header = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold };
            consoleStyle = new GUIStyle(small) { wordWrap = false };
        }

        void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();
            float scale = Mathf.Max(0.5f, Screen.height / RefHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenW = Screen.width / scale, screenH = Screen.height / scale;

            Robot robot = Robot.All.Count > 0 ? Robot.All[0] : null;
            DrawStatusPanel(robot);
            DrawPlots(screenW);
            if (robot != null) DrawLidarMap(robot, screenW, screenH);
            DrawConsole(screenH);
            DrawHelp(screenW, screenH);
        }

        // ---- Panels ----

        void DrawStatusPanel(Robot robot)
        {
            float x = 10f, y = 10f;
            int sensorCount = robot != null ? robot.Sensors.Count : 0;
            int motorCount = robot != null ? robot.Motors.Count : 0;
            int servoCount = robot != null ? robot.Servos.Count : 0;
            float h = 110f + Line + motorCount * Line * 2f + servoCount * Line + sensorCount * Line + 40f;
            Panel(new Rect(x, y, PanelWidth, h));
            x += 10f; y += 6f;

            GUI.Label(new Rect(x, y, PanelWidth, Line + 4), "Robot Simulator", header);
            y += Line + 4;

            if (robot == null)
            {
                GUI.Label(new Rect(x, y, PanelWidth, Line), "No robot in scene.", label);
                return;
            }

            var program = robot.ActiveProgram;
            string programName = program != null ? program.GetType().Name : "<i>none</i>";
            string status = robot.ProgramFaulted ? "<color=#ff5555>STOPPED (error)</color>"
                : program == null ? "<color=#aaaaaa>no program</color>"
                : "<color=#66ff66>running</color>";
            GUI.Label(new Rect(x, y, PanelWidth - 20, Line), $"Program: <b>{programName}</b>  {status}", label);
            y += Line;

            var sim = SimManager.Instance;
            string time = sim != null && sim.Paused ? "<color=#ffcc33>PAUSED</color>" : $"x{(sim != null ? sim.TimeScale : 1f):0.##}";
            string drive = robot.Drive != null ? robot.Drive.DriveType : "no drive";
            GUI.Label(new Rect(x, y, PanelWidth - 20, Line), $"speed {time}   {drive}", small);
            y += Line;

            if (sim != null)
            {
                // Marking line: run time vs limit, bumps, goal (detected by the simulator, not the program).
                bool over = sim.RunTime > sim.RunTimeLimit && sim.GoalTime < 0f;
                string runText = $"<color={(over ? "#ff6666" : "#ffffff")}>Run {sim.RunTime:F1} / {sim.RunTimeLimit:F0} s</color>";
                string bumps = $"<color={(robot.BumpCount == 0 ? "#66ff66" : "#ffcc33")}>Bumps {robot.BumpCount}</color>";
                string goal = sim.GoalTime >= 0f ? $"<color=#66ff66>GOAL at {sim.GoalTime:F1} s</color>" : "<color=#aaaaaa>goal not reached</color>";
                GUI.Label(new Rect(x, y, PanelWidth - 20, Line), $"{runText}   {bumps}   {goal}", small);
                y += Line;
            }

            if (robot.ProgramFaulted)
            {
                GUI.Label(new Rect(x, y, PanelWidth - 20, Line * 2), $"<color=#ff7777>{robot.FaultMessage}</color>", small);
            }
            y += Line + 6;

            GUI.Label(new Rect(x, y, PanelWidth, Line), servoCount > 0 ? "<b>Motors & servos</b>" : "<b>Motors</b>", label);
            y += Line;
            foreach (var m in robot.Motors)
            {
                GUI.Label(new Rect(x, y, 80, Line), m.Name, label);
                DrawPowerBar(new Rect(x + 80, y + 4, 150, Line - 8), m.Power);
                GUI.Label(new Rect(x + 240, y, 170, Line), $"{m.Power:+0.00;-0.00; 0.00}", label);
                y += Line;
                GUI.Label(new Rect(x + 80, y, PanelWidth - 100, Line),
                    $"{m.WheelSpeed:F1} rad/s   {m.EncoderTicks} ticks   {m.Distance:F2} m", small);
                y += Line;
            }

            foreach (var s in robot.Servos)
            {
                GUI.Label(new Rect(x, y, 80, Line), s.Name, label);
                float range = Mathf.Max(Mathf.Abs(s.MinAngle), Mathf.Abs(s.MaxAngle));
                // Bar runs right-to-left for positive (left) angles, like looking down on the robot.
                DrawPowerBar(new Rect(x + 80, y + 4, 150, Line - 8), -s.Angle / range);
                GUI.Label(new Rect(x + 240, y, 180, Line), $"{s.Angle:+0;-0;0}°  <color=#aaaaaa>→ {s.TargetAngle:+0;-0;0}°</color>", label);
                y += Line;
            }

            y += 6;
            GUI.Label(new Rect(x, y, PanelWidth, Line), "<b>Sensors</b>", label);
            y += Line;
            foreach (var s in robot.Sensors)
            {
                GUI.Label(new Rect(x, y, 95, Line), s.Name, small);
                GUI.Label(new Rect(x + 95, y, PanelWidth - 115, Line), s.ReadingText, small);
                y += Line;
            }
        }

        void DrawPowerBar(Rect r, float power)
        {
            DrawRect(r, new Color(1f, 1f, 1f, 0.15f));
            float half = r.width * 0.5f;
            float w = Mathf.Abs(power) * half;
            Color c = power >= 0 ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.45f, 0.3f);
            if (power >= 0) DrawRect(new Rect(r.x + half, r.y, w, r.height), c);
            else DrawRect(new Rect(r.x + half - w, r.y, w, r.height), c);
            DrawRect(new Rect(r.x + half - 0.5f, r.y - 2, 1f, r.height + 4), Color.white);
        }

        void DrawPlots(float screenW)
        {
            var series = RobotTelemetry.All;
            if (series.Count == 0) return;
            const float w = 340f, h = 80f;
            float x = screenW - w - 10f, y = 10f;
            foreach (var s in series)
            {
                Panel(new Rect(x, y, w, h + 24f));
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < s.Count; i++) { min = Mathf.Min(min, s[i]); max = Mathf.Max(max, s[i]); }
                if (max - min < 1e-4f) { max += 0.5f; min -= 0.5f; }
                GUI.Label(new Rect(x + 8, y + 2, w - 16, Line), $"<b>{s.Name}</b>  {s.Latest:0.###}   <color=#aaaaaa>[{min:0.##} … {max:0.##}]</color>", small);
                var plot = new Rect(x + 8, y + 22, w - 16, h - 4);
                DrawRect(plot, new Color(0f, 0f, 0f, 0.3f));
                float step = plot.width / (RobotTelemetry.Capacity - 1);
                float prevY = 0f;
                for (int i = 0; i < s.Count; i++)
                {
                    float py = plot.yMax - (s[i] - min) / (max - min) * plot.height;
                    float px = plot.x + (RobotTelemetry.Capacity - s.Count + i) * step;
                    if (i > 0)
                    {
                        float top = Mathf.Min(py, prevY), bottom = Mathf.Max(py, prevY);
                        DrawRect(new Rect(px - 1f, top - 1f, 2f, bottom - top + 2f), new Color(0.4f, 0.8f, 1f));
                    }
                    prevY = py;
                }
                y += h + 30f;
            }
        }

        void DrawLidarMap(Robot robot, float screenW, float screenH)
        {
            Lidar lidar = null;
            foreach (var s in robot.Sensors)
                if (s is Lidar l) { lidar = l; break; }
            if (lidar == null) return;

            const float size = 230f;
            var r = new Rect(screenW - size - 10f, screenH - size - 40f, size, size);
            Panel(r);
            GUI.Label(new Rect(r.x + 8, r.y + 2, size, Line), "<b>Lidar</b>  (2 m radius)", small);
            Vector2 c = r.center + new Vector2(0f, 8f);
            float metresToPx = (size * 0.5f - 14f) / 2f;
            var ranges = lidar.Ranges;
            float inc = lidar.AngleIncrement * Mathf.Deg2Rad;
            for (int i = 0; i < ranges.Length; i++)
            {
                float d = ranges[i];
                if (d >= lidar.MaxRange || d > 2f) continue;
                float a = i * inc;
                // 0 rad = up on the map (robot forward), positive = anticlockwise (left).
                float px = c.x - Mathf.Sin(a) * d * metresToPx;
                float py = c.y - Mathf.Cos(a) * d * metresToPx;
                DrawRect(new Rect(px - 1.5f, py - 1.5f, 3f, 3f), new Color(1f, 0.3f, 0.2f));
            }
            DrawRect(new Rect(c.x - 4f, c.y - 4f, 8f, 8f), RobotFactory.ChassisColour);
            DrawRect(new Rect(c.x - 1f, c.y - 12f, 2f, 8f), Color.white);
        }

        void DrawConsole(float screenH)
        {
            var entries = RobotConsole.Entries;
            float h = consoleLines * 18f + 30f;
            var r = new Rect(10f, screenH - h - 40f, 640f, h);
            Panel(r);
            GUI.Label(new Rect(r.x + 8, r.y + 2, 300, Line), "<b>Console</b>", small);
            int start = Mathf.Max(0, entries.Count - consoleLines);
            float y = r.y + 24f;
            for (int i = start; i < entries.Count; i++)
            {
                var e = entries[i];
                string colour = e.Type == LogType.Error || e.Type == LogType.Exception ? "#ff6666"
                    : e.Type == LogType.Warning ? "#ffcc55" : "#e8e8e8";
                string first = e.Message;
                int nl = first.IndexOf('\n');
                if (nl >= 0) first = first.Substring(0, nl);
                GUI.Label(new Rect(r.x + 8, y, r.width - 16, 18f), $"<color=#888888>{e.Time,6:F1}</color>  <color={colour}>{Escape(first)}</color>", consoleStyle);
                y += 18f;
            }
        }

        void DrawHelp(float screenW, float screenH)
        {
            var r = new Rect(0f, screenH - 30f, screenW, 30f);
            DrawRect(r, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(12f, r.y + 5f, screenW, Line),
                "<b>R</b> reset   <b>Space</b> pause   <b>+/-</b> sim speed   <b>C</b> camera   <b>G</b> sensor rays   <b>H</b> hide HUD   <b>1-5</b> robot type   (KeyboardTeleop: <b>WASD</b>/arrows, <b>Shift</b> = full power)",
                small);
        }

        // ---- Helpers ----

        void Panel(Rect r) => DrawRect(r, new Color(0.05f, 0.07f, 0.1f, 0.72f));

        void DrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        static string Escape(string s) => s.Replace("<", "‹").Replace(">", "›");
    }
}
