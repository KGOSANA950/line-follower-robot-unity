using UnityEngine;

namespace RobotSim
{
    /// <summary>
    /// A raycast wheel with a spring suspension, tyre grip and an optional motor.
    /// Every drive type is built from these: driven wheels, passive (unpowered but gripping) wheels and casters.
    /// Place the GameObject at the wheel hub; the wheel always points along the robot's forward axis
    /// (plus <see cref="SteerAngle"/>).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("Robot Sim/Sim Wheel")]
    public class SimWheel : MonoBehaviour
    {
        public enum WheelKind
        {
            /// <summary>Powered by a motor, grips sideways.</summary>
            Driven,
            /// <summary>Unpowered, but grips sideways (e.g. rear wheels of a tricycle).</summary>
            Passive,
            /// <summary>Free-swivelling ball/castor wheel: no sideways grip, only rolling resistance.</summary>
            Caster
        }

        [SerializeField] WheelKind kind = WheelKind.Driven;
        [SerializeField] Motor motor;
        [SerializeField] float radius = 0.035f;

        [Header("Suspension")]
        [SerializeField] float suspensionTravel = 0.01f;
        [SerializeField] float springStiffness = 1500f;
        [Range(0.1f, 2f)]
        [SerializeField] float dampingRatio = 0.5f;

        [Header("Tyre")]
        [Tooltip("Tyre-ground friction coefficient.")]
        [SerializeField] float friction = 0.9f;
        [Tooltip("Rolling resistance as a fraction of the load on the wheel.")]
        [SerializeField] float rollingResistance = 0.02f;
        [Tooltip("Fraction of sideways slip cancelled each physics step (0..1).")]
        [Range(0.05f, 1f)]
        [SerializeField] float lateralGrip = 0.5f;
        [Tooltip("Sideways friction as a fraction of forward friction. Below 1 lets the wheel skid sideways " +
                 "more easily (used for tank tracks, which must slide to turn).")]
        [Range(0.05f, 1f)]
        [SerializeField] float lateralFrictionScale = 1f;

        [Header("Visual")]
        [Tooltip("Child transform that is moved/spun to show the wheel. Should have no colliders.")]
        [SerializeField] Transform visual;

        RigidMount mount;
        Rigidbody body;
        float damper;
        float massShare;
        float springLength;
        Quaternion visualBaseRotation = Quaternion.identity;
        Vector3 visualBasePosition;

        public WheelKind Kind => kind;
        public Motor Motor => motor;
        public float Radius => radius;

        /// <summary>
        /// Steering angle in degrees relative to the robot's forward axis. Positive = pointing left
        /// (anticlockwise seen from above), like every other angle in the simulator.
        /// </summary>
        public float SteerAngle { get; set; }

        /// <summary>Wheel spin in rad/s (positive = rolling forward).</summary>
        public float AngularVelocity { get; private set; }

        /// <summary>Total wheel rotation in radians.</summary>
        public float Angle { get; private set; }

        public bool IsGrounded { get; private set; }

        /// <summary>Load on the wheel in newtons.</summary>
        public float NormalForce { get; private set; }

        /// <summary>Motor torque applied this step (N·m).</summary>
        public float AppliedTorque { get; private set; }

        internal void Configure(WheelKind wheelKind, Motor wheelMotor, float wheelRadius, Transform wheelVisual)
        {
            kind = wheelKind;
            motor = wheelMotor;
            radius = wheelRadius;
            visual = wheelVisual;
        }

        internal void ConfigureTyre(float tyreFriction, float lateralScale, float stiffness)
        {
            friction = tyreFriction;
            lateralFrictionScale = Mathf.Clamp(lateralScale, 0.05f, 1f);
            springStiffness = stiffness;
        }

        void Awake()
        {
            mount.Bind(transform);
            body = mount.Body;
            if (motor != null && kind != WheelKind.Caster)
                motor.Register(this);
            if (visual != null)
            {
                visualBaseRotation = visual.localRotation;
                visualBasePosition = visual.localPosition;
            }
            springLength = suspensionTravel;
        }

        void Start()
        {
            if (body == null)
            {
                Debug.LogWarning($"SimWheel '{name}' has no Rigidbody in its parents and will do nothing.", this);
                return;
            }
            int wheelCount = Mathf.Max(1, body.GetComponentsInChildren<SimWheel>().Length);
            massShare = body.mass / wheelCount;
            damper = 2f * dampingRatio * Mathf.Sqrt(springStiffness * massShare);
        }

        internal void ResetState()
        {
            AngularVelocity = 0f;
            Angle = 0f;
            SteerAngle = 0f;
            AppliedTorque = 0f;
        }

        void FixedUpdate()
        {
            if (!mount.IsBound)
            {
                // Recover after a script hot-reload in Play mode.
                mount.Bind(transform);
                body = mount.Body;
                if (body != null) Start();
            }
            if (body == null)
                return;

            float dt = Time.fixedDeltaTime;
            Quaternion rotation = mount.Rotation;
            Vector3 up = rotation * Vector3.up;
            Vector3 origin = mount.Position + up * suspensionTravel;
            float rayLength = suspensionTravel * 2f + radius;
            AppliedTorque = 0f;

            if (SimPhysics.Raycast(body, origin, -up, rayLength, out RaycastHit hit))
            {
                IsGrounded = true;
                springLength = hit.distance - radius;
                float compression = suspensionTravel - springLength;
                // Rate of compression from the body's actual velocity (no one-step lag).
                float compressionVelocity = -Vector3.Dot(body.GetPointVelocity(origin), up);
                NormalForce = Mathf.Max(0f, springStiffness * compression + damper * compressionVelocity);

                Vector3 n = hit.normal;
                Vector3 steerForward = Quaternion.AngleAxis(-SteerAngle, up) * (rotation * Vector3.forward);
                Vector3 forward = Vector3.ProjectOnPlane(steerForward, n).normalized;
                Vector3 right = Vector3.Cross(n, forward);
                Vector3 v = body.GetPointVelocity(hit.point);
                float vLong = Vector3.Dot(v, forward);
                float vLat = Vector3.Dot(v, right);

                // Rolling without slip: the wheel turns with the ground.
                AngularVelocity = vLong / radius;

                Vector3 force = n * NormalForce;
                if (kind == WheelKind.Caster)
                {
                    Vector3 planar = v - n * Vector3.Dot(v, n);
                    force -= planar / Mathf.Max(planar.magnitude, 0.01f) * rollingResistance * NormalForce;
                }
                else
                {
                    float drive = 0f;
                    if (kind == WheelKind.Driven && motor != null)
                    {
                        AppliedTorque = motor.TorqueForWheel(AngularVelocity);
                        drive = AppliedTorque / radius;
                    }
                    float roll = -Mathf.Clamp(vLong / 0.01f, -1f, 1f) * rollingResistance * NormalForce;
                    float lateral = -vLat * massShare / dt * lateralGrip;
                    float longitudinal = drive + roll;

                    // Friction ellipse: the tyre can only push so hard before it slips.
                    float maxLong = friction * NormalForce;
                    float maxLat = maxLong * lateralFrictionScale;
                    if (maxLong > 0f)
                    {
                        float a = longitudinal / maxLong, b = lateral / maxLat;
                        float usage = Mathf.Sqrt(a * a + b * b);
                        if (usage > 1f)
                        {
                            longitudinal /= usage;
                            lateral /= usage;
                        }
                    }
                    else
                    {
                        longitudinal = lateral = 0f;
                    }
                    force += forward * longitudinal + right * lateral;
                }
                body.AddForceAtPosition(force, hit.point);
            }
            else
            {
                IsGrounded = false;
                NormalForce = 0f;
                springLength = suspensionTravel * 2f;

                // Wheel in the air: spins up towards the motor's free speed, or slowly coasts down.
                float target = (kind == WheelKind.Driven && motor != null) ? motor.Power * motor.FreeSpeed : 0f;
                AngularVelocity = target + (AngularVelocity - target) * Mathf.Exp(-dt / 0.08f);
            }

            Angle += AngularVelocity * dt;
            if (motor != null && kind == WheelKind.Driven)
                motor.RecordTorque();
        }

        void LateUpdate()
        {
            if (visual == null)
                return;
            visual.localPosition = visualBasePosition + Vector3.up * (suspensionTravel - springLength);
            if (kind != WheelKind.Caster)
            {
                visual.localRotation = Quaternion.Euler(0f, -SteerAngle, 0f)
                                       * Quaternion.Euler(Angle * Mathf.Rad2Deg, 0f, 0f)
                                       * visualBaseRotation;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position + transform.up * suspensionTravel,
                            transform.position - transform.up * (suspensionTravel + radius));
        }
    }
}
