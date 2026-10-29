using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// Base class for all robot programs. Subclass it (or a drive-specific subclass such as
    /// <see cref="DifferentialDriveProgram"/>), add it to a robot, and override the callbacks:
    /// <list type="bullet">
    /// <item><see cref="OnStart"/>: once, when the program starts (find sensors, subscribe to events).</item>
    /// <item><see cref="OnTick"/>: repeatedly, <see cref="ControlRateHz"/> times per simulated second.</item>
    /// <item><see cref="OnCollision"/>: whenever the robot bumps into something.</item>
    /// <item><see cref="OnStop"/>: when the program is stopped or the robot is reset.</item>
    /// </list>
    /// Don't use Unity methods such as Update or FixedUpdate in programs — use the callbacks above.
    /// </summary>
    public abstract class RobotProgram : MonoBehaviour
    {
        [Tooltip("How many times per simulated second OnTick is called.")]
        [Range(1f, 100f)]
        [SerializeField] float controlRateHz = 50f;

        Robot robot;
        MotorSet motorSet;
        ServoSet servoSet;
        bool started;
        float tickAccumulator;
        float startTime;

        /// <summary>The robot this program controls.</summary>
        protected Robot Robot => robot != null ? robot : (robot = GetComponentInParent<Robot>());

        /// <summary>All motors by name, e.g. <c>Motors["Left"].Power = 0.5f;</c></summary>
        protected MotorSet Motors => motorSet ??= new MotorSet(Robot);

        /// <summary>All steering servos by name, e.g. <c>Servos["Steer"].TargetAngle = 20f;</c></summary>
        protected ServoSet Servos => servoSet ??= new ServoSet(Robot);

        /// <summary>How many times per second <see cref="OnTick"/> runs.</summary>
        protected float ControlRateHz
        {
            get => controlRateHz;
            set => controlRateHz = Mathf.Clamp(value, 1f, 100f);
        }

        /// <summary>Simulated seconds since the program started.</summary>
        protected float SimTime => started ? Time.fixedTime - startTime : 0f;

        /// <summary>True while the program is running.</summary>
        public bool IsRunning => started;

        // ---- Callbacks for students to override ----

        protected virtual void OnStart() { }
        protected virtual void OnTick(float dt) { }
        protected virtual void OnCollision(Collision collision) { }
        protected virtual void OnStop() { }

        // ---- Helpers ----

        /// <summary>Finds a sensor by its name (the name of its GameObject).</summary>
        protected T GetSensor<T>(string sensorName) where T : Sensor => Robot.GetSensor<T>(sensorName);

        /// <summary>Finds the first sensor of a type.</summary>
        protected T GetSensor<T>() where T : Sensor => Robot.GetSensor<T>();

        /// <summary>Finds every sensor of a type.</summary>
        protected List<T> GetSensors<T>() where T : Sensor => Robot.GetSensors<T>();

        /// <summary>Sets every motor's power to 0.</summary>
        protected void Stop() => Robot.StopAllMotors();

        /// <summary>Writes a message to the HUD console.</summary>
        protected void Log(object message) => RobotConsole.Log(message?.ToString() ?? "null");

        protected void LogWarning(object message) => RobotConsole.Log(message?.ToString() ?? "null", LogType.Warning);

        /// <summary>Adds a point to a live graph in the HUD.</summary>
        protected void Plot(string seriesName, float value) => RobotTelemetry.Plot(seriesName, value);

        // ---- Engine side (called by Robot) ----

        internal void Step(Robot owner)
        {
            robot = owner;
            if (!started)
            {
                started = true;
                startTime = Time.fixedTime;
                tickAccumulator = 0f;
                Run(OnStart, nameof(OnStart));
                if (owner.ProgramFaulted) return;
                tickAccumulator = 1f / controlRateHz; // tick straight away
            }
            else
            {
                tickAccumulator += Time.fixedDeltaTime;
            }

            float period = 1f / controlRateHz;
            if (tickAccumulator + 1e-5f >= period)
            {
                tickAccumulator -= period;
                if (tickAccumulator > period) tickAccumulator = 0f;
                try { OnTick(period); }
                catch (Exception e) { owner.ReportProgramError(e, nameof(OnTick)); }
            }
        }

        internal void HandleCollision(Collision collision)
        {
            if (!started) return;
            try { OnCollision(collision); }
            catch (Exception e) { Robot.ReportProgramError(e, nameof(OnCollision)); }
        }

        internal void Restart()
        {
            if (started)
                Run(OnStop, nameof(OnStop));
            started = false;
        }

        void Run(Action callback, string where)
        {
            try { callback(); }
            catch (Exception e) { Robot.ReportProgramError(e, where); }
        }

        void OnDisable()
        {
            if (!started) return;
            started = false;
            try { OnStop(); }
            catch (Exception e) { Debug.LogException(e, this); }
            if (Robot != null) Robot.StopAllMotors();
        }
    }

    /// <summary>
    /// A program for a particular kind of drive. <see cref="Drive"/> gives typed access to the drive's
    /// commands; the program reports a clear error if it is put on a robot with a different drive.
    /// </summary>
    public abstract class DriveProgram<TDrive> : RobotProgram where TDrive : class
    {
        TDrive drive;

        /// <summary>The robot's drive system.</summary>
        protected TDrive Drive
        {
            get
            {
                if (drive != null) return drive;
                drive = Robot.Drive as TDrive;
                if (drive == null)
                    throw new RobotException($"{GetType().Name} needs a robot with {typeof(TDrive).Name}, but this robot has {(Robot.Drive != null ? Robot.Drive.DriveType : "no drive system")}.");
                return drive;
            }
        }
    }

    /// <summary>
    /// For robots with a <see cref="DifferentialDrive"/> (and tracked robots, which are driven the same way):
    /// <c>Drive.SetPower(left, right)</c>.
    /// </summary>
    public abstract class DifferentialDriveProgram : DriveProgram<DifferentialDrive> { }

    /// <summary>For tracked (tank) robots: <c>Drive.SetPower(left, right)</c>.</summary>
    public abstract class TrackedDriveProgram : DriveProgram<TrackedDrive> { }

    /// <summary>For single-drive-wheel (tricycle) robots: <c>Drive.SetPower(p)</c> and <c>Drive.SetSteering(angle)</c>.</summary>
    public abstract class SingleWheelDriveProgram : DriveProgram<SingleWheelDrive> { }

    /// <summary>For Ackermann (car-like) robots: <c>Drive.SetPower(p)</c> and <c>Drive.SetSteering(angle)</c>.</summary>
    public abstract class AckermannDriveProgram : DriveProgram<AckermannDrive> { }

    /// <summary>For synchro-drive robots: <c>Drive.SetPower(p)</c> and <c>Drive.SetSteering(direction)</c>.</summary>
    public abstract class SynchroDriveProgram : DriveProgram<SynchroDrive> { }

    /// <summary>
    /// For any robot with one drive motor and a steering servo (single wheel, Ackermann or synchro):
    /// <c>Drive.SetPower(p)</c> and <c>Drive.SetSteering(angle)</c>.
    /// </summary>
    public abstract class SteeredDriveProgram : DriveProgram<ISteeredDrive> { }

    /// <summary>Lookup for a robot's motors by name.</summary>
    public class MotorSet : IEnumerable<Motor>
    {
        readonly Robot robot;

        internal MotorSet(Robot robot) => this.robot = robot;

        /// <summary>The motor with this name. Throws a helpful error if it doesn't exist.</summary>
        public Motor this[string motorName] => robot.GetMotor(motorName);

        public int Count => robot.Motors.Count;

        public IEnumerator<Motor> GetEnumerator() => robot.Motors.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

namespace RobotSim
{
    /// <summary>Lookup for a robot's steering servos by name.</summary>
    public class ServoSet : System.Collections.Generic.IEnumerable<SteeringServo>
    {
        readonly Robot robot;

        internal ServoSet(Robot robot) => this.robot = robot;

        /// <summary>The servo with this name. Throws a helpful error if it doesn't exist.</summary>
        public SteeringServo this[string servoName] => robot.GetServo(servoName);

        public int Count => robot.Servos.Count;

        public System.Collections.Generic.IEnumerator<SteeringServo> GetEnumerator() => robot.Servos.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
