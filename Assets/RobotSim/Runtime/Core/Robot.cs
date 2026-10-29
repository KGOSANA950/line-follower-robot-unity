using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// The root of a simulated robot. Finds the robot's motors, wheels, sensors, drive system and program,
    /// and runs the program's control loop each physics step (after sensors have sampled, before wheels apply forces).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Robot Sim/Robot")]
    public class Robot : MonoBehaviour
    {
        [SerializeField] Vector3 centerOfMass = new Vector3(0f, 0.04f, -0.03f);
        [Tooltip("Rotational inertia (kg·m²) about the robot's x (pitch), y (yaw) and z (roll) axes. " +
                 "Set explicitly because the collider alone ignores motors, battery and wheels.")]
        [SerializeField] Vector3 inertia = new Vector3(0.009f, 0.0123f, 0.0058f);

        static readonly List<Robot> all = new List<Robot>();
        /// <summary>Every active robot in the scene.</summary>
        public static IReadOnlyList<Robot> All => all;

        Rigidbody body;
        DriveSystem drive;
        readonly List<Motor> motors = new List<Motor>();
        readonly List<SteeringServo> servos = new List<SteeringServo>();
        readonly List<SimWheel> wheels = new List<SimWheel>();
        readonly List<Sensor> sensors = new List<Sensor>();
        readonly List<RobotProgram> programScratch = new List<RobotProgram>();
        Pose startPose;
        bool discovered;

        public Rigidbody Body => body;
        public DriveSystem Drive { get { Discover(); return drive; } }
        public IReadOnlyList<Motor> Motors { get { Discover(); return motors; } }
        public IReadOnlyList<SteeringServo> Servos { get { Discover(); return servos; } }
        public IReadOnlyList<SimWheel> Wheels { get { Discover(); return wheels; } }
        public IReadOnlyList<Sensor> Sensors { get { Discover(); return sensors; } }

        /// <summary>The program currently controlling this robot (the first enabled RobotProgram).</summary>
        public RobotProgram ActiveProgram { get; private set; }

        /// <summary>True after student code threw an exception; motors are stopped until reset.</summary>
        public bool ProgramFaulted { get; private set; }

        /// <summary>Number of times the robot has bumped into something since the last reset (shown in the HUD).</summary>
        public int BumpCount { get; private set; }
        public string FaultMessage { get; private set; }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.centerOfMass = centerOfMass;
            body.inertiaTensor = inertia;
            body.inertiaTensorRotation = Quaternion.identity;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            startPose = new Pose(transform.position, transform.rotation);

            // Low-friction body so the robot slides along walls instead of sticking to them.
            bodyMaterial ??= new PhysicsMaterial("RobotBody")
            {
                dynamicFriction = 0.15f,
                staticFriction = 0.15f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f
            };
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.sharedMaterial == null) c.sharedMaterial = bodyMaterial;
                c.contactOffset = 0.002f; // small robot: keep speculative contacts tight
            }

            Discover();
        }

        static PhysicsMaterial bodyMaterial;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        internal void Configure(Vector3 com, Vector3 inertiaTensor)
        {
            centerOfMass = com;
            inertia = inertiaTensor;
        }

        /// <summary>Re-scans the robot for motors, wheels and sensors (call after adding parts at runtime).</summary>
        public void Rediscover()
        {
            discovered = false;
            Discover();
        }

        void Discover()
        {
            if (discovered)
                return;
            discovered = true;
            GetComponentsInChildren(motors);
            GetComponentsInChildren(servos);
            GetComponentsInChildren(wheels);
            GetComponentsInChildren(sensors);
            drive = GetComponentInChildren<DriveSystem>();

            foreach (var group in sensors.GroupBy(s => s.Name).Where(g => g.Count() > 1))
                Debug.LogWarning($"Robot '{name}' has {group.Count()} sensors named '{group.Key}'. Give each sensor a unique name.", this);
            foreach (var group in motors.GroupBy(m => m.Name).Where(g => g.Count() > 1))
                Debug.LogWarning($"Robot '{name}' has {group.Count()} motors named '{group.Key}'.", this);
        }

        void FindProgram()
        {
            GetComponentsInChildren(programScratch);
            programScratch.RemoveAll(p => !p.isActiveAndEnabled);
            if (programScratch.Count > 1)
                Debug.LogWarning($"Robot '{name}' has {programScratch.Count} enabled programs; running '{programScratch[0].GetType().Name}'. Disable the others.", this);
            ActiveProgram = programScratch.Count > 0 ? programScratch[0] : null;
        }

        void FixedUpdate()
        {
            if (ActiveProgram == null || !ActiveProgram.isActiveAndEnabled)
                FindProgram();
            if (ActiveProgram != null && !ProgramFaulted)
                ActiveProgram.Step(this);
        }

        // Physics reports "contacts" that are merely close (within the contact offset), and contacts with
        // the ground. A bump is reported to the program only when something actually touches the robot's side.
        readonly HashSet<Collider> touching = new HashSet<Collider>();
        static readonly List<ContactPoint> contactScratch = new List<ContactPoint>();

        void OnCollisionEnter(Collision collision) => EvaluateContact(collision);
        void OnCollisionStay(Collision collision) => EvaluateContact(collision);
        void OnCollisionExit(Collision collision) => touching.Remove(collision.collider);

        void EvaluateContact(Collision collision)
        {
            if (!IsRealBump(collision))
            {
                touching.Remove(collision.collider);
                return;
            }
            if (!touching.Add(collision.collider))
                return;
            BumpCount++;
            if (ActiveProgram != null && !ProgramFaulted)
                ActiveProgram.HandleCollision(collision);
        }

        static bool IsRealBump(Collision collision)
        {
            collision.GetContacts(contactScratch);
            foreach (var c in contactScratch)
                if (c.separation < 0.001f && Mathf.Abs(c.normal.y) < 0.7f)
                    return true;
            return false;
        }

        // ---- Lookups (with friendly errors for students) ----

        public Motor GetMotor(string motorName)
        {
            Discover();
            foreach (var m in motors)
                if (m.Name == motorName) return m;
            string available = motors.Count == 0 ? "none" : string.Join(", ", motors.Select(m => $"\"{m.Name}\""));
            throw new RobotException($"No motor named \"{motorName}\". Available motors: {available}.");
        }

        public SteeringServo GetServo(string servoName)
        {
            Discover();
            foreach (var s in servos)
                if (s.Name == servoName) return s;
            string available = servos.Count == 0 ? "none (this robot has no steering)" : string.Join(", ", servos.Select(s => $"\"{s.Name}\""));
            throw new RobotException($"No servo named \"{servoName}\". Available servos: {available}.");
        }

        public T GetSensor<T>(string sensorName) where T : Sensor
        {
            Discover();
            foreach (var s in sensors)
            {
                if (s.Name != sensorName) continue;
                if (s is T typed) return typed;
                throw new RobotException($"Sensor \"{sensorName}\" is a {s.GetType().Name}, not a {typeof(T).Name}.");
            }
            throw new RobotException($"No sensor named \"{sensorName}\". Available sensors: {DescribeSensors()}.");
        }

        public T GetSensor<T>() where T : Sensor
        {
            Discover();
            foreach (var s in sensors)
                if (s is T typed) return typed;
            throw new RobotException($"This robot has no {typeof(T).Name}. Available sensors: {DescribeSensors()}.");
        }

        public List<T> GetSensors<T>() where T : Sensor
        {
            Discover();
            return sensors.OfType<T>().ToList();
        }

        string DescribeSensors() =>
            sensors.Count == 0 ? "none" : string.Join(", ", sensors.Select(s => $"\"{s.Name}\" ({s.GetType().Name})"));

        // ---- Control ----

        public void StopAllMotors()
        {
            Discover();
            foreach (var m in motors)
                m.Power = 0f;
        }

        /// <summary>Where <see cref="ResetRobot"/> puts the robot.</summary>
        public Pose StartPose => startPose;

        public void SetStartPose(Vector3 position, Quaternion rotation) => startPose = new Pose(position, rotation);

        /// <summary>Puts the robot back at its start pose and restarts its program.</summary>
        public void ResetRobot()
        {
            Discover();
            if (body != null)
            {
                body.position = startPose.position;
                body.rotation = startPose.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(startPose.position, startPose.rotation);
            foreach (var m in motors) m.ResetState();
            foreach (var s in servos) s.ResetState();
            foreach (var w in wheels) w.ResetState();
            foreach (var s in sensors) s.ResetSensor();
            ProgramFaulted = false;
            FaultMessage = null;
            touching.Clear();
            BumpCount = 0;
            if (ActiveProgram != null)
                ActiveProgram.Restart();
            Physics.SyncTransforms();
        }

        /// <summary>Called when student code throws: stops and locks the motors and reports the error.</summary>
        internal void ReportProgramError(Exception e, string where)
        {
            if (ProgramFaulted)
                return;
            StopAllMotors();
            foreach (var m in motors) m.Locked = true;
            foreach (var s in servos) s.Hold();
            ProgramFaulted = true;
            FaultMessage = $"{e.GetType().Name} in {where}: {e.Message}";
            RobotConsole.Log($"Program error — {FaultMessage}\nThe robot has been stopped. Fix the code and press R to restart.\n{FirstUserFrame(e)}", LogType.Error);
        }

        static string FirstUserFrame(Exception e)
        {
            if (string.IsNullOrEmpty(e.StackTrace)) return string.Empty;
            foreach (var line in e.StackTrace.Split('\n'))
            {
                if (!line.Contains("RobotSim.Robot") && !line.Contains("RobotSim.RobotProgram") && !line.Contains("RobotSim.Sensor."))
                    return line.Trim();
            }
            return string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();
    }
}
