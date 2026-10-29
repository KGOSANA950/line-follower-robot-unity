using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSim
{
    /// <summary>
    /// Camera with three modes, cycled with C: Follow (chase the robot), Top Down (whole maze) and
    /// Orbit (right-drag to rotate around the robot, scroll to zoom).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Robot Sim/Sim Camera Rig")]
    public class SimCameraRig : MonoBehaviour
    {
        public enum Mode
        {
            Follow,
            TopDown,
            Orbit
        }

        [SerializeField] Mode mode = Mode.Follow;
        [Tooltip("What to follow. Defaults to the first robot in the scene.")]
        [SerializeField] Transform target;
        [SerializeField] float followDistance = 0.55f;
        [SerializeField] float followHeight = 0.45f;
        [SerializeField] float smoothing = 6f;

        float orbitYaw = 30f;
        float orbitPitch = 35f;
        float orbitDistance = 0.8f;
        Camera cam;

        public Mode CurrentMode => mode;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.01f;
        }

        void LateUpdate()
        {
            if (target == null && Robot.All.Count > 0)
                target = Robot.All[0].transform;

            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame)
                mode = (Mode)(((int)mode + 1) % 3);

            float dt = Time.unscaledDeltaTime;
            float blend = 1f - Mathf.Exp(-smoothing * dt);

            switch (mode)
            {
                case Mode.Follow:
                    if (target == null) break;
                    Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
                    Vector3 desired = target.position - flatForward * followDistance + Vector3.up * followHeight;
                    transform.position = Vector3.Lerp(transform.position, desired, blend);
                    Quaternion look = Quaternion.LookRotation(target.position + flatForward * 0.2f - transform.position);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, blend);
                    break;

                case Mode.TopDown:
                    GetArena(out Vector3 centre, out float extent);
                    float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                    float fitHeight = extent * 0.5f / Mathf.Tan(halfFov);
                    float fitWidth = extent * 0.5f / (Mathf.Tan(halfFov) * Mathf.Max(0.1f, cam.aspect));
                    float height = Mathf.Max(fitHeight, fitWidth) * 1.1f;
                    transform.position = Vector3.Lerp(transform.position, centre + Vector3.up * height, blend);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(90f, 0f, 0f), blend);
                    break;

                case Mode.Orbit:
                    var mouse = Mouse.current;
                    if (mouse != null)
                    {
                        if (mouse.rightButton.isPressed)
                        {
                            Vector2 delta = mouse.delta.ReadValue();
                            orbitYaw += delta.x * 0.25f;
                            orbitPitch = Mathf.Clamp(orbitPitch - delta.y * 0.25f, 5f, 89f);
                        }
                        float scroll = mouse.scroll.ReadValue().y;
                        if (Mathf.Abs(scroll) > 0.01f)
                            orbitDistance = Mathf.Clamp(orbitDistance * (scroll > 0 ? 0.9f : 1.1f), 0.2f, 8f);
                    }
                    Vector3 pivot = target != null ? target.position : Vector3.zero;
                    Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                    transform.position = Vector3.Lerp(transform.position, pivot - rot * Vector3.forward * orbitDistance, blend);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, blend);
                    break;
            }
        }

        void GetArena(out Vector3 centre, out float extent)
        {
            var maze = SimManager.Instance != null ? SimManager.Instance.Maze : null;
            if (maze != null)
            {
                centre = maze.transform.position;
                extent = Mathf.Max(maze.Size.x, maze.Size.y) + 0.4f;
            }
            else
            {
                centre = target != null ? target.position : Vector3.zero;
                extent = 3f;
            }
        }
    }
}
