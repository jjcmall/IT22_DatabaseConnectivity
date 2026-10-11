using UnityEngine;

namespace CodeEscape.Obstacles
{
    /// <summary>How a hazard moves or switches on and off.</summary>
    public enum HazardMotionMode
    {
        /// <summary>Always dangerous, never moves (e.g. a tripwire).</summary>
        Static,
        /// <summary>Moves back and forth between its start and start + offset (patrol bot, drone). Can also scan on a timer.</summary>
        Patrol,
        /// <summary>Switches on and off on a timer, blinking a warning before it turns on (laser gate, alarm floor).</summary>
        OnOffCycle,
        /// <summary>Turns back and forth around Z (security camera sweeping a detection cone).</summary>
        Sweep,
        /// <summary>Swings like a pendulum around Z (hanging hook, swinging lamp).</summary>
        Swing,
        /// <summary>Slides shut and open again on a timer, blinking a warning first (sliding security door).</summary>
        SlideCycle
    }

    /// <summary>
    /// Moves or times a hazard so it is predictable: every hazard repeats the same pattern, and timed
    /// ones blink a warning before they become dangerous. Runs on scaled time, so it stops while the
    /// game is paused, a cutscene plays or Alex is caught. CatchHazard asks <see cref="IsDangerous"/>.
    /// </summary>
    public class HazardMotion : MonoBehaviour
    {
        [SerializeField] HazardMotionMode mode = HazardMotionMode.Static;
        [SerializeField, Tooltip("The moving part. Empty = this object.")]
        Transform target;
        [SerializeField, Tooltip("Seconds added to the clock, so hazards of the same kind don't move in step.")]
        float phase;

        [Header("Patrol")]
        [SerializeField] Vector2 patrolOffset = new Vector2(4f, 0f);
        [SerializeField, Min(0.1f)] float patrolSpeed = 2f;
        [SerializeField, Tooltip("Optional: turned 180 degrees around Y to face the walking direction.")]
        Transform facingPivot;
        [SerializeField, Tooltip("Patrol only: the detection (e.g. a drone's searchlight) also follows the on/off cycle below, so it can be passed while it is off.")]
        bool scanCycle;

        [Header("On / Off cycle")]
        [SerializeField, Min(0f)] float onTime = 2f;
        [SerializeField, Min(0f)] float offTime = 2f;
        [SerializeField, Min(0f), Tooltip("Blinking warning at the end of the off time.")]
        float warningTime = 0.7f;
        [SerializeField, Tooltip("Shown while dangerous (beams, sparks).")]
        GameObject[] activeVisuals = new GameObject[0];
        [SerializeField, Tooltip("Blink during the warning (lights, beam outlines).")]
        GameObject[] warningVisuals = new GameObject[0];

        [Header("Sweep / Swing")]
        [SerializeField, Tooltip("Degrees either side of the start rotation.")]
        float angle = 35f;
        [SerializeField, Min(0.2f)] float period = 4f;

        [Header("Slide cycle")]
        [SerializeField, Tooltip("Closed position, relative to the open (start) position.")]
        Vector2 slideOffset = new Vector2(0f, -3f);
        [SerializeField, Min(0.05f)] float moveTime = 0.45f;
        [SerializeField, Min(0f)] float closedTime = 1.2f;

        Vector3 startLocalPosition;
        float startAngle;
        bool warning;
        bool dangerous = true;
        Rigidbody2D body;

        public HazardMotionMode Mode => mode;
        public bool IsDangerous => dangerous;
        /// <summary>True while a timed hazard is blinking its warning.</summary>
        public bool IsWarning => warning;
        Transform Target => target != null ? target : transform;
        float Clock => Time.time + phase;

        void Awake()
        {
            startLocalPosition = Target.localPosition;
            startAngle = Target.localEulerAngles.z;
            // Moving colliders get a kinematic body so the physics engine follows them smoothly.
            if (mode == HazardMotionMode.Patrol || mode == HazardMotionMode.Sweep || mode == HazardMotionMode.Swing || mode == HazardMotionMode.SlideCycle)
            {
                body = Target.GetComponent<Rigidbody2D>();
                if (body == null && Target.GetComponentInChildren<Collider2D>() != null)
                {
                    body = Target.gameObject.AddComponent<Rigidbody2D>();
                    body.bodyType = RigidbodyType2D.Kinematic;
                }
            }
            Apply();
        }

        void Update()
        {
            Apply();
        }

        void Apply()
        {
            switch (mode)
            {
                case HazardMotionMode.Static:
                    dangerous = true;
                    warning = false;
                    break;
                case HazardMotionMode.Patrol:
                    UpdatePatrol();
                    break;
                case HazardMotionMode.OnOffCycle:
                    UpdateCycle();
                    break;
                case HazardMotionMode.Sweep:
                    dangerous = true;
                    SetAngle(startAngle + Mathf.Sin(Clock * Mathf.PI * 2f / period) * angle);
                    break;
                case HazardMotionMode.Swing:
                    dangerous = true;
                    // Eases at the ends like a real pendulum.
                    SetAngle(startAngle + Mathf.Cos(Clock * Mathf.PI * 2f / period) * angle);
                    break;
                case HazardMotionMode.SlideCycle:
                    UpdateSlide();
                    break;
            }
        }

        void UpdatePatrol()
        {
            if (scanCycle)
                UpdateCycle();
            else
                dangerous = true;
            float distance = patrolOffset.magnitude;
            if (distance < 0.01f)
                return;
            float travel = distance / patrolSpeed;
            float t = Mathf.PingPong(Clock / travel, 1f);
            // Smooth turnarounds read better and make the timing easier to judge.
            float eased = Mathf.SmoothStep(0f, 1f, t);
            SetLocalPosition(startLocalPosition + (Vector3)(patrolOffset * eased));
            if (facingPivot != null)
            {
                bool forward = Mathf.Repeat(Clock / travel, 2f) < 1f;
                bool towardsRight = forward == (patrolOffset.x >= 0f);
                facingPivot.localRotation = Quaternion.Euler(0f, towardsRight ? 0f : 180f, 0f);
            }
        }

        void UpdateCycle()
        {
            float cycle = onTime + offTime;
            float t = cycle > 0f ? Mathf.Repeat(Clock, cycle) : 0f;
            dangerous = t < onTime;
            warning = !dangerous && t >= cycle - warningTime;
            SetActive(activeVisuals, dangerous);
            SetActive(warningVisuals, warning && Mathf.Repeat(Time.time * 8f, 1f) < 0.55f);
        }

        void UpdateSlide()
        {
            // open (offTime) -> warning -> closing (moveTime) -> closed (closedTime) -> opening (moveTime)
            float cycle = offTime + moveTime + closedTime + moveTime;
            float t = Mathf.Repeat(Clock, cycle);
            float closed;
            if (t < offTime)
                closed = 0f;
            else if (t < offTime + moveTime)
                closed = (t - offTime) / moveTime;
            else if (t < offTime + moveTime + closedTime)
                closed = 1f;
            else
                closed = 1f - (t - offTime - moveTime - closedTime) / moveTime;
            dangerous = closed > 0.02f;
            warning = !dangerous && t >= offTime - warningTime;
            SetLocalPosition(startLocalPosition + (Vector3)(slideOffset * Mathf.SmoothStep(0f, 1f, closed)));
            SetActive(activeVisuals, dangerous);
            SetActive(warningVisuals, warning && Mathf.Repeat(Time.time * 8f, 1f) < 0.55f);
        }

        void SetLocalPosition(Vector3 local)
        {
            Transform t = Target;
            t.localPosition = local;
            if (body != null)
                body.position = t.position;
        }

        void SetAngle(float z)
        {
            Transform t = Target;
            Vector3 e = t.localEulerAngles;
            t.localRotation = Quaternion.Euler(e.x, e.y, z);
            if (body != null)
                body.rotation = t.eulerAngles.z;
        }

        static void SetActive(GameObject[] objects, bool active)
        {
            foreach (GameObject o in objects)
            {
                if (o != null && o.activeSelf != active)
                    o.SetActive(active);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (mode == HazardMotionMode.Patrol)
            {
                Gizmos.color = Color.red;
                Vector3 a = Application.isPlaying ? (Target.parent != null ? Target.parent.TransformPoint(startLocalPosition) : startLocalPosition) : Target.position;
                Gizmos.DrawLine(a, a + (Vector3)patrolOffset);
            }
        }
    }
}
