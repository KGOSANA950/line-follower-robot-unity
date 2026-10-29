using System;
using UnityEngine;

namespace RobotSim
{
    /// <summary>The robot designs the simulator can build.</summary>
    public enum RobotType
    {
        Differential,
        SingleWheel,
        Synchro,
        Ackermann,
        Tracked
    }

    /// <summary>
    /// Builds robots and sensors from primitives. Used by the editor menu (Robot Sim > ...) and by tests.
    /// All sizes are in metres; each robot's origin is on the ground at its centre of rotation.
    /// </summary>
    public static class RobotFactory
    {
        public static readonly Color ChassisColour = new Color(0.95f, 0.5f, 0.1f);
        static readonly Color TrackedColour = new Color(0.42f, 0.52f, 0.25f);
        static readonly Color SingleWheelColour = new Color(0.1f, 0.6f, 0.65f);
        static readonly Color AckermannColour = new Color(0.85f, 0.15f, 0.15f);
        static readonly Color SynchroColour = new Color(0.55f, 0.3f, 0.8f);
        static readonly Color DeckColour = new Color(0.15f, 0.15f, 0.17f);
        static readonly Color TyreColour = new Color(0.1f, 0.1f, 0.1f);
        static readonly Color HubColour = new Color(0.75f, 0.75f, 0.78f);
        static readonly Color AccentColour = new Color(1f, 0.85f, 0.1f);
        static readonly Color BoardColour = new Color(0.1f, 0.3f, 0.75f);
        static readonly Color CopperColour = new Color(0.8f, 0.45f, 0.2f);
        static readonly Color GyroColour = new Color(0.1f, 0.45f, 0.2f);

        public const float WheelRadius = 0.035f;
        public const float WheelOffsetX = 0.085f;

        /// <summary>Where the standard sensor kit goes on a particular robot.</summary>
        struct SensorLayout
        {
            public Vector3 Front, Left, Right, LineArray, Colour, Metal, Lidar, Gyro;
            public Vector3? Back;
        }

        public static string DisplayName(RobotType type) => type switch
        {
            RobotType.Differential => "Differential Drive",
            RobotType.SingleWheel => "Single Wheel Drive",
            RobotType.Synchro => "Synchro Drive",
            RobotType.Ackermann => "Ackermann Steering",
            RobotType.Tracked => "Tracked",
            _ => type.ToString()
        };

        public static GameObject Create(RobotType type, Func<Color, Material> materials = null, bool addDefaultSensors = true) => type switch
        {
            RobotType.SingleWheel => CreateSingleWheelRobot(materials, addDefaultSensors),
            RobotType.Synchro => CreateSynchroRobot(materials, addDefaultSensors),
            RobotType.Ackermann => CreateAckermannRobot(materials, addDefaultSensors),
            RobotType.Tracked => CreateTrackedRobot(materials, addDefaultSensors),
            _ => CreateDifferentialRobot(materials, addDefaultSensors)
        };

        // =====================================================================
        // Differential drive
        // =====================================================================

        /// <summary>
        /// Two driven wheels and a rear ball caster, plus (optionally) the standard sensor kit:
        /// Front/Left/Right ultrasonics, LineArray, Colour, Metal, Lidar and Gyro.
        /// </summary>
        public static GameObject CreateDifferentialRobot(Func<Color, Material> materials = null, bool addDefaultSensors = true)
        {
            materials ??= SimMaterials.Get;
            var root = CreateRoot("DifferentialRobot", 1.5f, new Vector3(0f, 0.04f, -0.03f), new Vector3(0.19f, 0.08f, 0.22f));

            Part("Chassis", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.045f, -0.02f),
                new Vector3(0.14f, 0.05f, 0.18f), ChassisColour, materials, keepCollider: true);
            Part("Deck", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.072f, -0.02f),
                new Vector3(0.13f, 0.004f, 0.17f), DeckColour, materials);
            Part("FrontMarker", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.0745f, 0.05f),
                new Vector3(0.1f, 0.003f, 0.015f), AccentColour, materials);

            var left = AddMotor(root, "Left", 0.15f, 14f);
            var right = AddMotor(root, "Right", 0.15f, 14f);
            var leftWheel = CreateWheel("WheelLeft", root.transform, new Vector3(-WheelOffsetX, WheelRadius, 0f), SimWheel.WheelKind.Driven, left, WheelRadius, 0.02f, materials);
            var rightWheel = CreateWheel("WheelRight", root.transform, new Vector3(WheelOffsetX, WheelRadius, 0f), SimWheel.WheelKind.Driven, right, WheelRadius, 0.02f, materials);
            CreateCaster("Caster", root.transform, new Vector3(0f, 0.015f, -0.085f), 0.015f, materials);
            root.AddComponent<DifferentialDrive>().Configure(left, right, leftWheel, rightWheel);

            if (addDefaultSensors)
            {
                AddSensorKit(root.transform, new SensorLayout
                {
                    Front = new Vector3(0f, 0.05f, 0.08f),
                    Left = new Vector3(-0.078f, 0.05f, -0.065f),
                    Right = new Vector3(0.078f, 0.05f, -0.065f),
                    LineArray = new Vector3(0f, 0.015f, 0.075f),
                    Colour = new Vector3(0f, 0.015f, 0.035f),
                    Metal = new Vector3(0f, 0.012f, 0.11f),
                    Lidar = new Vector3(0f, 0.095f, -0.02f),
                    Gyro = new Vector3(0f, 0.0755f, -0.08f)
                }, materials);
            }
            return Finish(root);
        }

        // =====================================================================
        // Tracked (skid steer)
        // =====================================================================

        /// <summary>
        /// Two tank tracks, each a row of four high-grip road wheels driven by one motor. Turning requires
        /// the tracks to skid sideways, which the wheels allow through a reduced sideways friction.
        /// </summary>
        public static GameObject CreateTrackedRobot(Func<Color, Material> materials = null, bool addDefaultSensors = true)
        {
            materials ??= SimMaterials.Get;
            var root = CreateRoot("TrackedRobot", 1.8f, new Vector3(0f, 0.045f, 0f), new Vector3(0.21f, 0.09f, 0.22f));

            Part("Chassis", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.052f, 0f),
                new Vector3(0.13f, 0.05f, 0.2f), TrackedColour, materials, keepCollider: true);
            Part("Deck", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.079f, 0f),
                new Vector3(0.12f, 0.004f, 0.19f), DeckColour, materials);
            Part("FrontMarker", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.0815f, 0.075f),
                new Vector3(0.1f, 0.003f, 0.015f), AccentColour, materials);

            const float r = 0.022f, trackX = 0.09f;
            var left = AddMotor(root, "Left", 0.13f, 20f);
            var right = AddMotor(root, "Right", 0.13f, 20f);
            SimWheel leftRef = null, rightRef = null;
            float[] zs = { 0.08f, 0.027f, -0.027f, -0.08f };
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var motor = side == 0 ? left : right;
                string sideName = side == 0 ? "Left" : "Right";
                Part($"Track{sideName}", PrimitiveType.Cube, root.transform, new Vector3(sx * trackX, 0.025f, 0f),
                    new Vector3(0.028f, 0.05f, 0.215f), TyreColour, materials);
                Part($"TrackGuard{sideName}", PrimitiveType.Cube, root.transform, new Vector3(sx * trackX, 0.0515f, 0f),
                    new Vector3(0.032f, 0.003f, 0.2f), TrackedColour, materials);
                for (int i = 0; i < zs.Length; i++)
                {
                    bool end = i == 0 || i == zs.Length - 1;
                    var wheel = CreateWheel($"Road{sideName}{i}", root.transform, new Vector3(sx * trackX, r, zs[i]),
                        SimWheel.WheelKind.Driven, motor, r, 0.02f, materials,
                        visible: end, visualOffsetX: sx * 0.016f);
                    wheel.ConfigureTyre(1.0f, 0.45f, 800f);
                    if (i == 0)
                    {
                        if (side == 0) leftRef = wheel; else rightRef = wheel;
                    }
                }
            }
            root.AddComponent<TrackedDrive>().Configure(left, right, leftRef, rightRef);

            if (addDefaultSensors)
            {
                AddSensorKit(root.transform, new SensorLayout
                {
                    Front = new Vector3(0f, 0.055f, 0.108f),
                    Left = new Vector3(-0.108f, 0.068f, -0.06f),
                    Right = new Vector3(0.108f, 0.068f, -0.06f),
                    LineArray = new Vector3(0f, 0.015f, 0.12f),
                    Colour = new Vector3(0f, 0.015f, 0.04f),
                    Metal = new Vector3(0f, 0.012f, 0.155f),
                    Lidar = new Vector3(0f, 0.102f, -0.02f),
                    Gyro = new Vector3(0f, 0.0825f, -0.07f)
                }, materials);
            }
            return Finish(root);
        }

        // =====================================================================
        // Single drive wheel (tricycle)
        // =====================================================================

        /// <summary>
        /// A steered and driven front wheel with two unpowered rear wheels. The origin is the middle of the
        /// rear axle, which is the point the robot pivots around.
        /// </summary>
        public static GameObject CreateSingleWheelRobot(Func<Color, Material> materials = null, bool addDefaultSensors = true)
        {
            materials ??= SimMaterials.Get;
            const float wheelBase = 0.13f;
            var root = CreateRoot("SingleWheelRobot", 1.5f, new Vector3(0f, 0.04f, 0.04f), new Vector3(0.19f, 0.08f, 0.22f));

            Part("Chassis", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.05f, 0.0175f),
                new Vector3(0.14f, 0.05f, 0.155f), SingleWheelColour, materials, keepCollider: true);
            Part("Deck", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.077f, 0.0175f),
                new Vector3(0.13f, 0.004f, 0.145f), DeckColour, materials);
            Part("FrontMarker", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.0795f, 0.08f),
                new Vector3(0.1f, 0.003f, 0.015f), AccentColour, materials);
            // Steering head over the front wheel.
            Part("HeadPlate", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.08f, wheelBase + 0.02f),
                new Vector3(0.05f, 0.006f, 0.1f), DeckColour, materials);
            Part("LineBracket", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.05f, 0.185f),
                new Vector3(0.01f, 0.06f, 0.006f), DeckColour, materials);
            Part("SteeringColumn", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.075f, wheelBase),
                new Vector3(0.014f, 0.006f, 0.014f), HubColour, materials);

            var drive = AddMotor(root, "Drive", 0.18f, 14f);
            var steer = AddServo(root, "Steer", -85f, 85f, false, 240f);
            var front = CreateWheel("FrontWheel", root.transform, new Vector3(0f, WheelRadius, wheelBase), SimWheel.WheelKind.Driven, drive, WheelRadius, 0.022f, materials);
            var rl = CreateWheel("RearLeft", root.transform, new Vector3(-WheelOffsetX, WheelRadius, 0f), SimWheel.WheelKind.Passive, null, WheelRadius, 0.02f, materials);
            var rr = CreateWheel("RearRight", root.transform, new Vector3(WheelOffsetX, WheelRadius, 0f), SimWheel.WheelKind.Passive, null, WheelRadius, 0.02f, materials);
            root.AddComponent<SingleWheelDrive>().Configure(drive, steer, front, rl, rr);

            if (addDefaultSensors)
            {
                AddSensorKit(root.transform, new SensorLayout
                {
                    Front = new Vector3(0f, 0.095f, 0.165f),
                    Left = new Vector3(-0.078f, 0.05f, 0.06f),
                    Right = new Vector3(0.078f, 0.05f, 0.06f),
                    LineArray = new Vector3(0f, 0.015f, 0.185f),
                    Colour = new Vector3(0f, 0.015f, 0.06f),
                    Metal = new Vector3(0f, 0.012f, -0.03f),
                    Lidar = new Vector3(0f, 0.1f, 0f),
                    Gyro = new Vector3(0f, 0.0805f, -0.04f)
                }, materials);
            }
            return Finish(root);
        }

        // =====================================================================
        // Ackermann (car)
        // =====================================================================

        /// <summary>Car-like robot: rear wheels driven through a differential, front wheels steered by an Ackermann linkage.</summary>
        public static GameObject CreateAckermannRobot(Func<Color, Material> materials = null, bool addDefaultSensors = true)
        {
            materials ??= SimMaterials.Get;
            const float r = 0.03f, halfBase = 0.065f, halfTrack = 0.075f;
            var root = CreateRoot("AckermannRobot", 1.6f, new Vector3(0f, 0.045f, 0f), new Vector3(0.17f, 0.08f, 0.26f));

            Part("Chassis", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.055f, 0f),
                new Vector3(0.1f, 0.04f, 0.24f), AckermannColour, materials, keepCollider: true);
            Part("Deck", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.077f, -0.01f),
                new Vector3(0.09f, 0.004f, 0.2f), DeckColour, materials);
            Part("FrontBumper", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.045f, 0.122f),
                new Vector3(0.15f, 0.015f, 0.01f), DeckColour, materials);
            Part("RearBumper", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.045f, -0.122f),
                new Vector3(0.15f, 0.015f, 0.01f), DeckColour, materials);
            Part("FrontMarker", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.0795f, 0.07f),
                new Vector3(0.07f, 0.003f, 0.015f), AccentColour, materials);
            Part("FrontAxle", PrimitiveType.Cube, root.transform, new Vector3(0f, r, halfBase),
                new Vector3(halfTrack * 2f - 0.02f, 0.006f, 0.006f), HubColour, materials);
            Part("RearAxle", PrimitiveType.Cube, root.transform, new Vector3(0f, r, -halfBase),
                new Vector3(halfTrack * 2f - 0.02f, 0.006f, 0.006f), HubColour, materials);

            var drive = AddMotor(root, "Drive", 0.22f, 16f);
            var steer = AddServo(root, "Steer", -40f, 40f, false, 200f);
            var fl = CreateWheel("FrontLeft", root.transform, new Vector3(-halfTrack, r, halfBase), SimWheel.WheelKind.Passive, null, r, 0.018f, materials);
            var fr = CreateWheel("FrontRight", root.transform, new Vector3(halfTrack, r, halfBase), SimWheel.WheelKind.Passive, null, r, 0.018f, materials);
            var rl = CreateWheel("RearLeft", root.transform, new Vector3(-halfTrack, r, -halfBase), SimWheel.WheelKind.Driven, drive, r, 0.018f, materials);
            var rr = CreateWheel("RearRight", root.transform, new Vector3(halfTrack, r, -halfBase), SimWheel.WheelKind.Driven, drive, r, 0.018f, materials);
            root.AddComponent<AckermannDrive>().Configure(drive, steer, fl, fr, rl, rr);

            if (addDefaultSensors)
            {
                AddSensorKit(root.transform, new SensorLayout
                {
                    Front = new Vector3(0f, 0.06f, 0.13f),
                    Left = new Vector3(-0.058f, 0.06f, 0f),
                    Right = new Vector3(0.058f, 0.06f, 0f),
                    LineArray = new Vector3(0f, 0.015f, 0.14f),
                    Colour = new Vector3(0f, 0.015f, 0.03f),
                    Metal = new Vector3(0f, 0.015f, -0.035f),
                    Lidar = new Vector3(0f, 0.1f, -0.03f),
                    Gyro = new Vector3(0f, 0.0805f, -0.09f)
                }, materials);
            }
            return Finish(root);
        }

        // =====================================================================
        // Synchro drive
        // =====================================================================

        /// <summary>
        /// Round robot with three wheels that are all driven by one motor and all steered together by one
        /// continuous steering motor. The body never rotates; the wheels point where the robot should go.
        /// Has a fourth ultrasonic ("Back") since it can drive in any direction.
        /// </summary>
        public static GameObject CreateSynchroRobot(Func<Color, Material> materials = null, bool addDefaultSensors = true)
        {
            materials ??= SimMaterials.Get;
            const float r = 0.028f, wheelRing = 0.075f, bodyRadius = 0.11f;
            var root = CreateRoot("SynchroRobot", 1.6f, new Vector3(0f, 0.06f, 0f), Vector3.zero);
            root.GetComponent<Robot>().Configure(new Vector3(0f, 0.06f, 0f), new Vector3(0.0053f, 0.0097f, 0.0053f));

            Part("Body", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.085f, 0f),
                new Vector3(bodyRadius * 2f, 0.025f, bodyRadius * 2f), SynchroColour, materials, keepCollider: true);
            Part("Deck", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.111f, 0f),
                new Vector3(bodyRadius * 1.85f, 0.002f, bodyRadius * 1.85f), DeckColour, materials);
            Part("FrontMarker", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.1135f, 0.085f),
                new Vector3(0.06f, 0.003f, 0.015f), AccentColour, materials);

            var drive = AddMotor(root, "Drive", 0.24f, 16f);
            var steer = AddServo(root, "Steer", -180f, 180f, true, 180f);
            var wheels = new SimWheel[3];
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var hub = new Vector3(Mathf.Sin(a) * wheelRing, r, Mathf.Cos(a) * wheelRing);
                wheels[i] = CreateWheel($"Wheel{i}", root.transform, hub, SimWheel.WheelKind.Driven, drive, r, 0.018f, materials);
                // Fork: a short column from the body down to each wheel.
                Part($"Column{i}", PrimitiveType.Cylinder, root.transform, new Vector3(hub.x, 0.062f, hub.z),
                    new Vector3(0.012f, 0.006f, 0.012f), HubColour, materials);
            }
            root.AddComponent<SynchroDrive>().Configure(drive, steer, wheels);

            if (addDefaultSensors)
            {
                AddSensorKit(root.transform, new SensorLayout
                {
                    Front = new Vector3(0f, 0.085f, bodyRadius + 0.005f),
                    Back = new Vector3(0f, 0.085f, -bodyRadius - 0.005f),
                    Left = new Vector3(-bodyRadius - 0.005f, 0.085f, 0f),
                    Right = new Vector3(bodyRadius + 0.005f, 0.085f, 0f),
                    LineArray = new Vector3(0f, 0.015f, 0.125f),
                    Colour = new Vector3(0f, 0.015f, 0f),
                    Metal = new Vector3(0f, 0.015f, -0.05f),
                    Lidar = new Vector3(0f, 0.135f, 0f),
                    Gyro = new Vector3(0.05f, 0.1135f, -0.05f)
                }, materials);
                // Bracket holding the line array out in front of the body.
                Part("LineBracket", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.04f, 0.115f),
                    new Vector3(0.01f, 0.045f, 0.006f), DeckColour, materials);
            }
            return Finish(root);
        }

        // =====================================================================
        // Shared building blocks
        // =====================================================================

        static GameObject CreateRoot(string name, float mass, Vector3 centerOfMass, Vector3 size)
        {
            var root = new GameObject(name);
            root.SetActive(false); // components wake up only once the robot is fully assembled
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.angularDamping = 0.05f;
            body.linearDamping = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var robot = root.AddComponent<Robot>();
            if (size != Vector3.zero)
            {
                // Solid box approximation of the robot's rotational inertia.
                var inertia = new Vector3(
                    mass * (size.y * size.y + size.z * size.z) / 12f,
                    mass * (size.x * size.x + size.z * size.z) / 12f,
                    mass * (size.x * size.x + size.y * size.y) / 12f);
                robot.Configure(centerOfMass, inertia);
            }
            return root;
        }

        static GameObject Finish(GameObject root)
        {
            root.SetActive(true);
            return root;
        }

        static Motor AddMotor(GameObject root, string name, float stallTorque, float freeSpeed)
        {
            var m = root.AddComponent<Motor>();
            m.Configure(name, stallTorque, freeSpeed);
            return m;
        }

        static SteeringServo AddServo(GameObject root, string name, float min, float max, bool continuous, float speed)
        {
            var s = root.AddComponent<SteeringServo>();
            s.Configure(name, min, max, continuous, speed);
            return s;
        }

        static SimWheel CreateWheel(string name, Transform parent, Vector3 hub, SimWheel.WheelKind kind, Motor motor, float radius, float width,
            Func<Color, Material> materials, bool visible = true, float visualOffsetX = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = hub;
            Transform visual = null;
            if (visible)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                visual.localPosition = new Vector3(visualOffsetX, 0f, 0f);
                Part("Tyre", PrimitiveType.Cylinder, visual, Vector3.zero, new Vector3(radius * 2f, width * 0.5f, radius * 2f),
                    TyreColour, materials, rotation: Quaternion.Euler(0f, 0f, 90f));
                Part("Hub", PrimitiveType.Cylinder, visual, Vector3.zero, new Vector3(radius * 0.86f, width * 0.525f, radius * 0.86f),
                    HubColour, materials, rotation: Quaternion.Euler(0f, 0f, 90f));
                Part("Spoke", PrimitiveType.Cube, visual, Vector3.zero, new Vector3(width * 1.075f, radius * 1.66f, radius * 0.23f), HubColour, materials);
            }
            var wheel = go.AddComponent<SimWheel>();
            wheel.Configure(kind, motor, radius, visual);
            return wheel;
        }

        static SimWheel CreateCaster(string name, Transform parent, Vector3 hub, float radius, Func<Color, Material> materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = hub;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            Part("Ball", PrimitiveType.Sphere, visual, Vector3.zero, Vector3.one * radius * 2f, HubColour, materials);
            var wheel = go.AddComponent<SimWheel>();
            wheel.Configure(SimWheel.WheelKind.Caster, null, radius, visual);
            return wheel;
        }

        static void AddSensorKit(Transform robot, SensorLayout l, Func<Color, Material> materials)
        {
            AddUltrasonic(robot, "Front", l.Front, 0f, materials);
            if (l.Back.HasValue) AddUltrasonic(robot, "Back", l.Back.Value, 180f, materials);
            AddUltrasonic(robot, "Left", l.Left, -90f, materials);
            AddUltrasonic(robot, "Right", l.Right, 90f, materials);
            AddLineSensorArray(robot, "LineArray", l.LineArray, materials);
            AddColourSensor(robot, "Colour", l.Colour, materials);
            AddMetalDetector(robot, "Metal", l.Metal, materials);
            AddLidar(robot, "Lidar", l.Lidar, materials);
            AddGyro(robot, "Gyro", l.Gyro, materials);
        }

        // ---- Sensors ----

        public static UltrasonicSensor AddUltrasonic(Transform robot, string name, Vector3 localPos, float yawDegrees, Func<Color, Material> materials = null)
        {
            var go = SensorObject(robot, name, localPos, yawDegrees);
            materials ??= SimMaterials.Get;
            Part("Board", PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -0.009f), new Vector3(0.045f, 0.02f, 0.003f), BoardColour, materials);
            Part("TransducerL", PrimitiveType.Cylinder, go.transform, new Vector3(-0.012f, 0f, -0.004f), new Vector3(0.016f, 0.004f, 0.016f),
                HubColour, materials, rotation: Quaternion.Euler(90f, 0f, 0f));
            Part("TransducerR", PrimitiveType.Cylinder, go.transform, new Vector3(0.012f, 0f, -0.004f), new Vector3(0.016f, 0.004f, 0.016f),
                HubColour, materials, rotation: Quaternion.Euler(90f, 0f, 0f));
            var s = go.AddComponent<UltrasonicSensor>();
            s.UpdateRateHz = 20f;
            return s;
        }

        public static LineSensor AddLineSensor(Transform robot, string name, Vector3 localPos, Func<Color, Material> materials = null)
        {
            var go = SensorObject(robot, name, localPos, 0f);
            materials ??= SimMaterials.Get;
            Part("Board", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.003f, 0f), new Vector3(0.012f, 0.004f, 0.012f), DeckColour, materials);
            var s = go.AddComponent<LineSensor>();
            s.UpdateRateHz = 100f;
            return s;
        }

        public static LineSensorArray AddLineSensorArray(Transform robot, string name, Vector3 localPos, Func<Color, Material> materials = null)
        {
            var go = SensorObject(robot, name, localPos, 0f);
            materials ??= SimMaterials.Get;
            Part("Board", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.003f, 0f), new Vector3(0.075f, 0.004f, 0.012f), DeckColour, materials);
            var s = go.AddComponent<LineSensorArray>();
            s.UpdateRateHz = 100f;
            return s;
        }

        public static ColourSensor AddColourSensor(Transform robot, string name, Vector3 localPos, Func<Color, Material> materials = null)
        {
            var go = SensorObject(robot, name, localPos, 0f);
            materials ??= SimMaterials.Get;
            Part("Board", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.003f, 0f), new Vector3(0.018f, 0.004f, 0.018f), Color.white, materials);
            var s = go.AddComponent<ColourSensor>();
            s.UpdateRateHz = 50f;
            return s;
        }

        public static MetalDetector AddMetalDetector(Transform robot, string name, Vector3 localPos, Func<Color, Material> materials = null)
        {
            var go = SensorObject(robot, name, localPos, 0f);
            materials ??= SimMaterials.Get;
            Part("Coil", PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.05f, 0.002f, 0.05f), CopperColour, materials);
            Part("Arm", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.012f, -0.035f), new Vector3(0.008f, 0.004f, 0.05f), DeckColour, materials);
            var s = go.AddComponent<MetalDetector>();
            s.UpdateRateHz = 20f;
            return s;
        }

        public static Lidar AddLidar(Transform robot, string name, Vector3 localPos, Func<Color, Material> materials = null)
        {
            var go = SensorObject(robot, name, localPos, 0f);
            materials ??= SimMaterials.Get;
            Part("Base", PrimitiveType.Cylinder, go.transform, new Vector3(0f, -0.015f, 0f), new Vector3(0.05f, 0.006f, 0.05f), DeckColour, materials);
            Part("Head", PrimitiveType.Cylinder, go.transform, new Vector3(0f, -0.002f, 0f), new Vector3(0.04f, 0.008f, 0.04f), TyreColour, materials);
            var s = go.AddComponent<Lidar>();
            s.UpdateRateHz = 10f;
            return s;
        }

        public static GyroSensor AddGyro(Transform robot, string name, Vector3 localPos, Func<Color, Material> materials = null)
        {
            var go = SensorObject(robot, name, localPos, 0f);
            materials ??= SimMaterials.Get;
            Part("Board", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.001f, 0f), new Vector3(0.02f, 0.002f, 0.016f), GyroColour, materials);
            Part("Chip", PrimitiveType.Cube, go.transform, new Vector3(0f, 0.0025f, 0f), new Vector3(0.007f, 0.002f, 0.007f), TyreColour, materials);
            var s = go.AddComponent<GyroSensor>();
            s.UpdateRateHz = 50f;
            return s;
        }

        static GameObject SensorObject(Transform robot, string name, Vector3 localPos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(robot, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color colour,
            Func<Color, Material> materials, bool keepCollider = false, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = materials(colour);
            if (!keepCollider)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            else if (type == PrimitiveType.Cylinder)
            {
                // A flat cylinder's default capsule collider would be a sphere; use the real shape instead.
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().convex = true;
            }
            return go;
        }
    }
}
