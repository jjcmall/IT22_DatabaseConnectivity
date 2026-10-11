using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeEscape.Player
{
    /// <summary>
    /// Simple physics-based movement for Alex.
    /// Side View (default): A/D or Left/Right to walk, W/Up/Space to jump.
    /// Top Down: WASD or Arrow Keys move in eight directions with gravity disabled.
    /// Input is read through the Input System package (the project's only active input backend).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PlayerController : MonoBehaviour
    {
        public enum MovementMode
        {
            SideView,
            TopDown
        }

        [Header("Mode")]
        [SerializeField] MovementMode movementMode = MovementMode.SideView;

        [Header("Movement")]
        [SerializeField, Min(0f)] float moveSpeed = 5f;
        [SerializeField, Min(0f), Tooltip("Side View only: upward speed applied when jumping.")]
        float jumpSpeed = 9f;
        [SerializeField, Min(0f), Tooltip("Side View only. Top Down always uses 0.")]
        float gravityScale = 3f;

        [Header("Visuals")]
        [SerializeField, Tooltip("Optional. Mirrored on X so Alex faces the walking direction (2D art). Not used when a facing pivot is set.")]
        Transform visual;
        [SerializeField, Tooltip("Optional. A 3D model turned around Y to face the walking direction instead of being mirrored.")]
        Transform facingPivot;
        [SerializeField, Tooltip("Facing pivot yaw when walking right (turned slightly towards the camera).")]
        float rightYaw = 130f;
        [SerializeField, Tooltip("Facing pivot yaw when walking left.")]
        float leftYaw = 230f;
        [SerializeField, Min(0f), Tooltip("Degrees per second when turning around.")]
        float turnSpeed = 900f;
        [SerializeField, Tooltip("Optional. Alex's Animator (BreathingIdle / Run / Jump states).")]
        Animator animator;
        [SerializeField, Min(0f), Tooltip("Losing the ground for less than this (seams, small steps) keeps the run animation.")]
        float airborneDelay = 0.08f;

        static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");

        Rigidbody2D body;
        ContactFilter2D groundFilter;
        Vector2 moveInput;
        bool jumpRequested;
        float airTime;
        bool facingRight = true;

        public bool IsGrounded { get; private set; }
        public MovementMode Mode => movementMode;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.gravityScale = movementMode == MovementMode.SideView ? gravityScale : 0f;

            // Only contacts whose normal points upwards (surfaces below Alex) count as ground.
            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetNormalAngle(45f, 135f);

            // 2.5D: Alex always stays on the gameplay plane; 2D physics never moves him in depth.
            Vector3 position = transform.position;
            if (!Mathf.Approximately(position.z, CameraView.GameplayPlaneZ))
                transform.position = new Vector3(position.x, position.y, CameraView.GameplayPlaneZ);
        }

        void Update()
        {
            UpdateCutsceneAnimation();
            TurnFacingPivot();

            // Ignore input while paused so nothing is queued up for when the game resumes.
            if (Time.timeScale <= 0f)
            {
                moveInput = Vector2.zero;
                jumpRequested = false;
                return;
            }
            ReadInput();
        }

        void FixedUpdate()
        {
            IsGrounded = body.IsTouching(groundFilter);
            // Velocity after the last physics step: near 0 when walking into a wall.
            Vector2 actualVelocity = body.linearVelocity;

            if (movementMode == MovementMode.SideView)
            {
                Vector2 velocity = body.linearVelocity;
                velocity.x = moveInput.x * moveSpeed;
                if (jumpRequested && IsGrounded)
                    velocity.y = jumpSpeed;
                body.linearVelocity = velocity;
            }
            else
            {
                body.linearVelocity = Vector2.ClampMagnitude(moveInput, 1f) * moveSpeed;
            }

            jumpRequested = false;
            UpdateFacing();
            UpdateAnimator(actualVelocity);
        }

        void UpdateAnimator(Vector2 actualVelocity)
        {
            if (animator == null)
                return;

            float verticalVelocity = body.linearVelocity.y;
            // A jump leaves the ground at once, even while the feet still touch it this step.
            bool onGround = IsGrounded && verticalVelocity <= 0.1f;
            airTime = onGround ? 0f : airTime + Time.fixedDeltaTime;
            bool grounded = onGround || (airTime < airborneDelay && verticalVelocity <= 0.1f);

            animator.SetBool(IsGroundedHash, grounded);
            animator.SetBool(IsRunningHash, grounded && !Mathf.Approximately(moveInput.x, 0f) && Mathf.Abs(actualVelocity.x) > 0.1f);
            animator.SetFloat(VerticalVelocityHash, verticalVelocity);
        }

        // The game is frozen (timeScale 0) during cutscenes; Alex keeps breathing on unscaled time
        // there, but freezes with everything else in the pause menu and challenges.
        void UpdateCutsceneAnimation()
        {
            if (animator == null)
                return;

            bool inCutscene = Game.GameManager.Instance != null && Game.GameManager.Instance.CurrentState == Game.GameState.Cutscene;
            AnimatorUpdateMode mode = inCutscene ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
            if (animator.updateMode != mode)
                animator.updateMode = mode;
            if (inCutscene)
                animator.SetBool(IsRunningHash, false);
        }

        void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            moveInput = new Vector2(x, y);

            if (movementMode == MovementMode.SideView &&
                (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                jumpRequested = true;
            }
        }

        void UpdateFacing()
        {
            if (Mathf.Approximately(moveInput.x, 0f))
                return;
            facingRight = moveInput.x > 0f;
            if (visual == null || facingPivot != null)
                return;

            Vector3 scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveInput.x);
            visual.localScale = scale;
        }

        // A 3D model turns around (through facing the camera) rather than being mirrored.
        void TurnFacingPivot()
        {
            if (facingPivot == null)
                return;
            Quaternion target = Quaternion.Euler(0f, facingRight ? rightYaw : leftYaw, 0f);
            facingPivot.localRotation = Quaternion.RotateTowards(facingPivot.localRotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
