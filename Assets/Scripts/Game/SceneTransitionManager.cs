using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodeEscape.Game
{
    /// <summary>
    /// Loads scenes with a short fade to black. Ignores requests while a scene is being left, rejects
    /// scenes missing from Build Settings and always restores Time.timeScale to 1. A request while the
    /// new scene is still fading in (e.g. a cutscene skipped at once) takes over from that fade.
    /// The fade is advanced in Update on unscaled time (not a coroutine), so it always finishes, even
    /// while the game is frozen by a cutscene or the pause menu. Lives on the persistent GameSystems object.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            FadingOut,
            Loading,
            FadingIn
        }

        public static SceneTransitionManager Instance { get; private set; }

        [SerializeField, Tooltip("Full-screen black overlay whose alpha is animated during transitions.")]
        CanvasGroup fadeOverlay;
        [SerializeField, Min(0f)] float fadeOutDuration = 0.3f;
        [SerializeField, Min(0f)] float fadeInDuration = 0.3f;

        // Caps each fade step so a long loading hitch doesn't skip the fade-in.
        const float MaxFadeStep = 1f / 30f;

        Phase phase;
        string targetScene;
        AsyncOperation load;
        float fadeFrom;
        float fadeTo;
        float fadeDuration;
        float fadeElapsed;

        public bool IsTransitioning { get; private set; }
        /// <summary>True from the start of a transition until the new scene has loaded (the fade-in comes after).</summary>
        public bool IsLeavingScene { get; private set; }

        float Alpha => fadeOverlay != null ? fadeOverlay.alpha : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SetOverlay(0f, false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Loads a scene with a fade, or directly if no SceneTransitionManager exists.</summary>
        public static bool Load(string sceneName)
        {
            if (Instance != null)
                return Instance.LoadScene(sceneName);

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneTransition] Scene '{sceneName}' is not in Build Settings.");
                return false;
            }
            Debug.LogWarning("[SceneTransition] No SceneTransitionManager found - loading without a fade.");
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            return true;
        }

        public static bool ReloadActiveScene()
        {
            return Load(SceneManager.GetActiveScene().name);
        }

        public bool LoadScene(string sceneName)
        {
            if (IsLeavingScene)
            {
                Debug.LogWarning($"[SceneTransition] Already loading a scene - ignored request for '{sceneName}'.");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneTransition] Scene '{sceneName}' is not in Build Settings.");
                return false;
            }

            Debug.Log($"[SceneTransition] Loading '{sceneName}'");
            targetScene = sceneName;
            IsTransitioning = true;
            IsLeavingScene = true;
            // Block clicks immediately so nothing else can be pressed during the fade. Starts from the
            // current alpha, so taking over a fade-in doesn't flash.
            float start = Alpha;
            SetOverlay(start, true);
            BeginFade(start, 1f, fadeOutDuration * (1f - start));
            phase = Phase.FadingOut;
            return true;
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.FadingOut:
                    if (!StepFade())
                        return;
                    // A scene left while paused must never pass a frozen time scale to the next scene.
                    Time.timeScale = 1f;
                    load = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
                    phase = Phase.Loading;
                    break;
                case Phase.Loading:
                    if (load != null && !load.isDone)
                        return;
                    load = null;
                    IsLeavingScene = false;
                    BeginFade(Alpha, 0f, fadeInDuration);
                    phase = Phase.FadingIn;
                    break;
                case Phase.FadingIn:
                    if (!StepFade())
                        return;
                    SetOverlay(0f, false);
                    IsTransitioning = false;
                    phase = Phase.Idle;
                    break;
            }
        }

        void BeginFade(float from, float to, float duration)
        {
            fadeFrom = from;
            fadeTo = to;
            fadeDuration = duration;
            fadeElapsed = 0f;
        }

        // Advances the fade on unscaled time (so it also runs while the game is paused). True when done.
        bool StepFade()
        {
            fadeElapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            if (fadeOverlay != null)
                fadeOverlay.alpha = Mathf.Lerp(fadeFrom, fadeTo, t);
            return t >= 1f;
        }

        void SetOverlay(float alpha, bool blockInput)
        {
            if (fadeOverlay == null)
                return;
            fadeOverlay.alpha = alpha;
            fadeOverlay.blocksRaycasts = blockInput;
        }
    }
}
