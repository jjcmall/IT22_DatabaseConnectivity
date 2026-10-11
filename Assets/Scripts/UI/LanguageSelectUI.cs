using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>
    /// Lets the player choose Python, PHP or SQL, then opens that language's level map. Each language
    /// keeps its own level progress; choosing a different language than before starts a new playthrough.
    /// </summary>
    public class LanguageSelectUI : MonoBehaviour
    {
        [SerializeField, Tooltip("Shows the previous choice, then the confirmation.")]
        TMP_Text selectionStatus;
        [SerializeField, Tooltip("Selected on start for keyboard navigation.")]
        Selectable firstSelected;

        bool choiceLocked;

        void Start()
        {
            ProgrammingLanguage previous = LanguageManager.Current;
            SetStatus(previous.IsValid() ? $"Last choice: {previous.ToDisplayName()}" : "Pick a language to begin.");
            UIFocus.Select(firstSelected);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                BackToMainMenu();
        }

        public void SelectPython()
        {
            Choose(ProgrammingLanguage.Python);
        }

        public void SelectPHP()
        {
            Choose(ProgrammingLanguage.PHP);
        }

        public void SelectSQL()
        {
            Choose(ProgrammingLanguage.SQL);
        }

        public void BackToMainMenu()
        {
            if (choiceLocked)
                return;
            SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        void Choose(ProgrammingLanguage language)
        {
            if (choiceLocked)
                return;
            if (LanguageManager.Instance == null)
            {
                Debug.LogError("[LanguageSelect] LanguageManager is missing, so the choice cannot be stored.");
                return;
            }

            // Picking the same language again keeps the playthrough, so a level in progress can be continued.
            if (LanguageManager.Current != language)
                LanguageManager.Instance.SelectLanguage(language);
            SetStatus($"{language.ToDisplayName()} selected!");
            choiceLocked = SceneTransitionManager.Load(SceneNames.LevelMap);
        }

        void SetStatus(string message)
        {
            if (selectionStatus != null)
                selectionStatus.text = message;
        }
    }
}
