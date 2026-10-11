using UnityEngine;

namespace CodeEscape.Player
{
    /// <summary>
    /// Smoothly follows a target and keeps the view inside the level bounds. Works with an orthographic
    /// camera or the 2.5D perspective camera (bounds apply to what is seen on the gameplay plane).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 offset = new Vector2(0f, 1f);
        [SerializeField, Min(0f)] float smoothTime = 0.15f;

        [Header("Level Bounds (world space)")]
        [SerializeField] bool clampToBounds = true;
        [SerializeField] Vector2 boundsMin = new Vector2(-16f, -5.5f);
        [SerializeField] Vector2 boundsMax = new Vector2(16f, 6f);

        Camera cam;
        Vector3 velocity;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void OnEnable()
        {
            // Re-enabled after a cutscene camera move: continue smoothly from where the camera is.
            velocity = Vector3.zero;
        }

        void Start()
        {
            // Start already framed on the target instead of sliding in from the scene origin.
            if (target != null)
                transform.position = GetDesiredPosition(CameraView.GetSize(cam));
        }

        void LateUpdate()
        {
            if (target == null)
                return;
            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(CameraView.GetSize(cam)), ref velocity, smoothTime);
        }

        /// <summary>Where the camera wants to be for the given view size (inside the level bounds).</summary>
        public Vector3 GetDesiredPosition(float viewSize)
        {
            Vector3 position = target != null
                ? new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z)
                : transform.position;
            return ClampToBounds(position, viewSize);
        }

        /// <summary>Keeps a camera position inside the level bounds for the given view size (see CameraView).</summary>
        public Vector3 ClampToBounds(Vector3 position, float viewSize)
        {
            if (!clampToBounds || cam == null)
                return position;

            float halfHeight = viewSize;
            float halfWidth = halfHeight * cam.aspect;
            position.x = ClampAxis(position.x, boundsMin.x + halfWidth, boundsMax.x - halfWidth);
            position.y = ClampAxis(position.y, boundsMin.y + halfHeight, boundsMax.y - halfHeight);
            return position;
        }

        static float ClampAxis(float value, float min, float max)
        {
            // If the level is smaller than the view on this axis, keep it centred.
            return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
        }

        void OnDrawGizmosSelected()
        {
            if (!clampToBounds)
                return;
            Gizmos.color = Color.cyan;
            Vector2 center = (boundsMin + boundsMax) * 0.5f;
            Vector2 size = boundsMax - boundsMin;
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0f));
        }
    }
}
