using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;

namespace CodeEscape.Game
{
    /// <summary>
    /// Owns the basic global game state: which part of the game the player is in, whether it is
    /// paused, and how long the current playthrough has been played. Language, settings, saving,
    /// story and scene loading have their own managers. Lives on the persistent GameSystems object.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Raised with (previous, current) whenever the state changes.</summary>
        public static event Action<GameState, GameState> StateChanged;

        [SerializeField, Tooltip("Shown for debugging. Driven by scene loads, the pause menu and challenges.")]
        GameState currentState = GameState.Boot;
        [SerializeField, Tooltip("Seconds played in this playthrough (gameplay scenes only).")]
        float playTime;

        public GameState CurrentState => currentState;
        public bool IsPaused => currentState == GameState.Paused;
        public float PlayTime => playTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload is disabled in this project, so statics must be cleared manually.
            Instance = null;
            StateChanged = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] A second GameSystems object was found and destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ProgressData progress = SaveManager.LoadProgress();
            if (progress != null)
                playTime = progress.timePlayed;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LanguageManager.LanguageChanged += HandleNewGame;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LanguageManager.LanguageChanged -= HandleNewGame;
        }

        void Start()
        {
            SetState(StateForScene(SceneManager.GetActiveScene().name));
        }

        void Update()
        {
            if (currentState == GameState.Playing || currentState == GameState.Challenge || currentState == GameState.Paused
                || currentState == GameState.Cutscene)
                playTime += Time.unscaledDeltaTime;

            // Safety net: only the pause menu, a challenge or a cutscene may freeze the game.
            if (Time.timeScale <= 0f && !IsFrozenState(currentState))
            {
                Debug.LogWarning($"[GameManager] Time.timeScale was 0 in state {currentState} - restored to 1.");
                Time.timeScale = 1f;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetState(GameState newState)
        {
            if (newState == currentState)
                return;

            GameState previous = currentState;
            currentState = newState;
            // Leaving a frozen state always unfreezes the game.
            if (!IsFrozenState(newState))
                Time.timeScale = 1f;
            Debug.Log($"[GameManager] State: {previous} -> {newState}");
            StateChanged?.Invoke(previous, newState);
        }

        /// <summary>Starts the play-time counter again (used when progress is reset).</summary>
        public void ResetPlayTime()
        {
            playTime = 0f;
        }

        /// <summary>Sets the play time of a restored playthrough (a level started from the level map).</summary>
        public void SetPlayTime(float seconds)
        {
            playTime = Mathf.Max(0f, seconds);
        }

        /// <summary>States in which Time.timeScale is allowed to be 0.</summary>
        public static bool IsFrozenState(GameState state)
        {
            return state == GameState.Paused || state == GameState.Challenge || state == GameState.Cutscene;
        }

        public void QuitGame()
        {
            Debug.Log("[GameManager] Quit requested.");
#if UNITY_EDITOR
            Debug.Log("[GameManager] Application.Quit() is ignored in the Editor, so Play Mode is stopped instead.");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                SetState(StateForScene(scene.name));
        }

        void HandleNewGame(ProgrammingLanguage language)
        {
            ResetPlayTime();
        }

        static GameState StateForScene(string sceneName)
        {
            switch (sceneName)
            {
                case SceneNames.MainMenu: return GameState.MainMenu;
                case SceneNames.LanguageSelect: return GameState.LanguageSelect;
                case SceneNames.LevelMap: return GameState.LevelMap;
                case SceneNames.Intro: return GameState.Intro;
                case SceneNames.Ending: return GameState.Ending;
                case SceneNames.Results: return GameState.Results;
                default: return GameState.Playing;
            }
        }
    }
}
