using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.SaveSystem;
using CodeEscape.UI;

namespace CodeEscape.Game
{
    /// <summary>
    /// ESC pause menu for gameplay scenes. Pausing sets Time.timeScale to 0; resuming, leaving the
    /// scene and every scene load restore it to 1. Lives in the PauseMenu prefab (not persistent).
    /// Runs before other scripts so an ESC that closes a programming challenge isn't also read as "pause".
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PauseManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField, Tooltip("Root of the pause overlay (dim background and windows).")]
        GameObject pauseRoot;
        [SerializeField] GameObject pausePanel;
        [SerializeField, Tooltip("Clue Journal window (lists the collected clues).")]
        GameObject clueJournalPanel;
        [SerializeField] SettingsMenuUI settingsMenu;

        [Header("Keyboard Focus")]
        [SerializeField] Selectable pauseFirstSelected;
        [SerializeField] Selectable clueJournalFirstSelected;

        public bool IsPaused { get; private set; }

        void Awake()
        {
            HideAllPanels();
        }

        void OnEnable()
        {
            if (settingsMenu != null)
                settingsMenu.Closed += HandleSettingsClosed;
        }

        void OnDisable()
        {
            if (settingsMenu != null)
                settingsMenu.Closed -= HandleSettingsClosed;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                HandleEscape();
        }

        void OnDestroy()
        {
            // Never leave the game frozen if this scene is unloaded while paused.
            if (IsPaused)
                Time.timeScale = 1f;
        }

        /// <summary>ESC: pause, or step back one level in the pause menu, or resume.</summary>
        public void HandleEscape()
        {
            if (IsSceneTransitioning() || IsChallengeOpen())
                return;

            if (!IsPaused)
                Pause();
            else if (settingsMenu != null && settingsMenu.IsOpen)
                settingsMenu.Back();
            else if (clueJournalPanel != null && clueJournalPanel.activeSelf)
                CloseClueJournal();
            else
                Resume();
        }

        public void Pause()
        {
            if (IsPaused || IsSceneTransitioning() || IsChallengeOpen())
                return;

            IsPaused = true;
            Time.timeScale = 0f;
            SetActive(pauseRoot, true);
            ShowPausePanel();
            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Paused);
            Debug.Log("[PauseManager] Paused");
        }

        /// <summary>Resume button.</summary>
        public void Resume()
        {
            if (!IsPaused)
                return;

            if (settingsMenu != null && settingsMenu.IsOpen)
                settingsMenu.Back();
            HideAllPanels();
            IsPaused = false;
            Time.timeScale = 1f;
            UIFocus.Clear();
            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Playing);
            Debug.Log("[PauseManager] Resumed");
        }

        /// <summary>Clue Journal button.</summary>
        public void OpenClueJournal()
        {
            SetActive(pausePanel, false);
            SetActive(clueJournalPanel, true);
            UIFocus.Select(clueJournalFirstSelected);
        }

        public void CloseClueJournal()
        {
            SetActive(clueJournalPanel, false);
            ShowPausePanel();
        }

        /// <summary>Settings button.</summary>
        public void OpenSettings()
        {
            if (settingsMenu == null)
                return;
            SetActive(pausePanel, false);
            settingsMenu.Open();
        }

        /// <summary>Restart Level button: reloads the current scene.</summary>
        public void RestartLevel()
        {
            Debug.Log("[PauseManager] Restart Level");
            SceneTransitionManager.ReloadActiveScene();
        }

        /// <summary>Level Map button: progress inside the level stays saved, so it can be continued from the map.</summary>
        public void ReturnToLevelMap()
        {
            Debug.Log("[PauseManager] Level Map");
            if (ProgressSaveService.Instance != null)
                ProgressSaveService.Instance.SaveNow();
            if (!SceneTransitionManager.Load(SceneNames.LevelMap))
                SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        /// <summary>Main Menu button.</summary>
        public void ReturnToMainMenu()
        {
            Debug.Log("[PauseManager] Main Menu");
            SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        void HandleSettingsClosed()
        {
            if (IsPaused)
                ShowPausePanel();
        }

        void ShowPausePanel()
        {
            SetActive(clueJournalPanel, false);
            SetActive(pausePanel, true);
            UIFocus.Select(pauseFirstSelected);
        }

        void HideAllPanels()
        {
            SetActive(pausePanel, false);
            SetActive(clueJournalPanel, false);
            SetActive(pauseRoot, false);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        static bool IsSceneTransitioning()
        {
            return SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning;
        }

        // While a programming challenge or a cutscene is running, ESC belongs to it, not the pause menu.
        static bool IsChallengeOpen()
        {
            if (GameManager.Instance == null)
                return false;
            GameState state = GameManager.Instance.CurrentState;
            return state == GameState.Challenge || state == GameState.Cutscene;
        }
    }
}
