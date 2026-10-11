using System.Collections;
using UnityEngine;

namespace CodeEscape.Player
{
    /// <summary>
    /// Simple directed camera moves for cutscenes: pan, zoom, focus on an object and reset back to
    /// following Alex. CameraFollow2D is switched off while the camera is directed. The moves are
    /// IEnumerators run by the CutsceneManager on unscaled time, so they work while the game is frozen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // Caps a single step so a frame hitch doesn't make the camera jump.
        const float MaxStep = 1f / 20f;

        public static CameraController Current { get; private set; }

        [SerializeField, Tooltip("Turned off while the camera is directed. Found on this object if empty.")]
        CameraFollow2D follow;
        [SerializeField, Tooltip("Added to an object's position when focusing on it.")]
        Vector2 focusOffset = new Vector2(0f, 0.75f);
        [SerializeField, Min(0f), Tooltip("View size the cutscene zoom values were written for. Zooms are scaled by " +
            "the camera's own view size / this, so cutscenes keep their framing. 0 uses the zoom values as they are.")]
        float authoredSize = 5.4f;

        Camera cam;
        float defaultSize;

        public bool IsDirected { get; private set; }
        public float DefaultSize => defaultSize;
        public float Size => CameraView.GetSize(cam);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (follow == null)
                follow = GetComponent<CameraFollow2D>();
            defaultSize = CameraView.GetSize(cam);
        }

        void OnEnable()
        {
            Current = this;
        }

        void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>Pans to a world position. A zoom above 0 changes the view size at the same time.</summary>
        public IEnumerator Pan(Vector2 position, float duration, float zoom = 0f)
        {
            float size = zoom > 0f ? ScaleZoom(zoom) : CameraView.GetSize(cam);
            return Move(new Vector3(position.x, position.y, transform.position.z), size, duration);
        }

        public IEnumerator Zoom(float size, float duration)
        {
            return Move(transform.position, size > 0f ? ScaleZoom(size) : defaultSize, duration);
        }

        // Cutscene zooms were written for the authored view size; keep the same framing relative to the default.
        float ScaleZoom(float zoom)
        {
            return authoredSize > 0f ? zoom * defaultSize / authoredSize : zoom;
        }

        /// <summary>Centres the view on an object (plus the focus offset).</summary>
        public IEnumerator Focus(Transform target, float duration, float zoom = 0f)
        {
            Vector3 point = target.position + (Vector3)focusOffset;
            return Pan(point, duration, zoom);
        }

        /// <summary>Returns to the normal view on Alex, then hands the camera back to CameraFollow2D.</summary>
        public IEnumerator ResetView(float duration)
        {
            TakeControl();
            Vector3 home = follow != null ? follow.GetDesiredPosition(defaultSize) : transform.position;
            yield return Move(home, defaultSize, duration);
            Release();
        }

        /// <summary>Jumps straight back to the normal view (used when a cutscene is skipped).</summary>
        public void ResetInstant()
        {
            CameraView.SetSize(cam, defaultSize);
            if (follow != null)
                transform.position = follow.GetDesiredPosition(defaultSize);
            Release();
        }

        IEnumerator Move(Vector3 endPosition, float endSize, float duration)
        {
            TakeControl();
            endPosition.z = transform.position.z;
            endPosition = Clamp(endPosition, endSize);
            Vector3 startPosition = transform.position;
            float startSize = CameraView.GetSize(cam);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                CameraView.SetSize(cam, Mathf.Lerp(startSize, endSize, t));
                transform.position = Vector3.Lerp(startPosition, endPosition, t);
                yield return null;
            }
            CameraView.SetSize(cam, endSize);
            transform.position = endPosition;
        }

        Vector3 Clamp(Vector3 position, float size)
        {
            return follow != null ? follow.ClampToBounds(position, size) : position;
        }

        void TakeControl()
        {
            IsDirected = true;
            if (follow != null)
                follow.enabled = false;
        }

        void Release()
        {
            IsDirected = false;
            if (follow != null)
                follow.enabled = true;
        }
    }
}
