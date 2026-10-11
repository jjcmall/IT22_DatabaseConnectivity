using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;

namespace CodeEscape.UI
{
    /// <summary>
    /// Main menu: Play Adventure (continue the saved level, or pick a language and level), Investigation Logs, Settings, SDG 4 and
    /// Terminate Session (exit).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] SettingsMenuUI settingsMenu;
        [SerializeField, Tooltip("Disabled while a panel or the settings screen is open so focus can't reach it.")]
        CanvasGroup menuButtons;
        [SerializeField, Tooltip("Selected on start for keyboard navigation.")]
        Selectable firstSelected;
        [SerializeField, Tooltip("Re-selected when the settings screen closes.")]
        Selectable settingsButton;

        [Header("Info Panels")]
        [SerializeField, Tooltip("Shows the Clue Journal of the saved playthrough.")]
        GameObject investigationLogsPanel;
        [SerializeField] Selectable investigationLogsButton;
        [SerializeField] GameObject sdgPanel;
        [SerializeField] Selectable sdgButton;

        [Header("Continue Panel")]
        [SerializeField, Tooltip("Shown by Play Adventure when a saved playthrough exists.")]
        GameObject continuePanel;
        [SerializeField] TMP_Text continueSummary;
        [SerializeField] Selectable continueButton;

        GameObject openPanel;
        Selectable openPanelButton;

        void Awake()
        {
            SetPanel(investigationLogsPanel, false);
            SetPanel(sdgPanel, false);
            SetPanel(continuePanel, false);
        }

        void OnEnable()
        {
            if (settingsMenu == null)
                return;
            settingsMenu.Opened += HandleSettingsOpened;
            settingsMenu.Closed += HandleSettingsClosed;
        }

        void OnDisable()
        {
            if (settingsMenu == null)
                return;
            settingsMenu.Opened -= HandleSettingsOpened;
            settingsMenu.Closed -= HandleSettingsClosed;
        }

        void Start()
        {
            UIFocus.Select(firstSelected);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (openPanel != null)
                ClosePanel();
            else if (settingsMenu != null && settingsMenu.IsOpen)
                settingsMenu.Back();
        }

        /// <summary>Play Adventure: asks Continue / Select Level when a level in progress is saved.</summary>
        public void OnPlayClicked()
        {
            if (HasSavedLevel() && continuePanel != null)
            {
                if (continueSummary != null)
                    continueSummary.text = DescribeSave(SaveManager.LoadProgress());
                OpenPanel(continuePanel, firstSelected);
                UIFocus.Select(continueButton);
            }
            else
            {
                StartNewGame();
            }
        }

        public void OnContinueClicked()
        {
            LevelProgressManager progress = LevelProgressManager.Instance;
            if (progress != null)
            {
                if (!progress.ResumeSavedLevel(out string reason))
                {
                    Debug.Log($"[MainMenu] Continue: {reason}");
                    StartNewGame();
                }
            }
            else if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ContinueGame();
            }
        }

        /// <summary>Select Level: language select, then that language's level map.</summary>
        public void StartNewGame()
        {
            SceneTransitionManager.Load(SceneNames.LanguageSelect);
        }

        static bool HasSavedLevel()
        {
            LevelProgressManager progress = LevelProgressManager.Instance;
            return progress != null ? progress.TryGetResumableLevel(out _) : SaveManager.HasProgress;
        }

        public void OnInvestigationLogsClicked()
        {
            OpenPanel(investigationLogsPanel, investigationLogsButton);
        }

        public void OnSettingsClicked()
        {
            if (settingsMenu != null)
                settingsMenu.Open();
        }

        public void OnSdgClicked()
        {
            OpenPanel(sdgPanel, sdgButton);
        }

        public void OnExitClicked()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.QuitGame();
            else
                Application.Quit();
        }

        /// <summary>Close / Back button on the panels.</summary>
        public void ClosePanel()
        {
            if (openPanel == null)
                return;
            SetPanel(openPanel, false);
            Selectable returnTo = openPanelButton;
            openPanel = null;
            openPanelButton = null;
            SetMenuInteractable(true);
            UIFocus.Select(returnTo);
        }

        void OpenPanel(GameObject panel, Selectable fromButton)
        {
            if (panel == null)
                return;
            if (openPanel != null)
                SetPanel(openPanel, false);
            openPanel = panel;
            openPanelButton = fromButton;
            SetPanel(panel, true);
            SetMenuInteractable(false);
            UIFocus.Clear();
        }

        static string DescribeSave(ProgressData save)
        {
            if (save == null)
                return string.Empty;
            string location = save.currentLevel;
            LevelInfo info = LevelManager.Instance != null ? LevelManager.Instance.GetInfo(save.currentLevel) : null;
            if (info != null)
                location = $"Level {LevelManager.Instance.IndexOf(info) + 1} - {info.displayName}";
            int minutes = (int)(save.timePlayed / 60f);
            return $"Language: {((ProgrammingLanguage)save.language).ToDisplayName()}\n" +
                   $"Location: {location}\n" +
                   $"Objective: {save.currentObjective}\n" +
                   $"Score: {save.score}   Clues: {save.collectedClues.Count}   Time: {minutes} min";
        }

        void HandleSettingsOpened()
        {
            SetMenuInteractable(false);
        }

        void HandleSettingsClosed()
        {
            SetMenuInteractable(true);
            UIFocus.Select(settingsButton);
        }

        void SetMenuInteractable(bool interactable)
        {
            if (menuButtons != null)
                menuButtons.interactable = interactable;
        }

        static void SetPanel(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
                panel.SetActive(active);
        }
    }
}
