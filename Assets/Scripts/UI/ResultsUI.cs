using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>
    /// Displays the results gathered by ResultsManager: language, score, challenges, clues, hints,
    /// time played, completion status and rank. Its buttons forward to ResultsManager.
    /// </summary>
    public class ResultsUI : MonoBehaviour
    {
        [SerializeField] ResultsManager manager;

        [Header("Values")]
        [SerializeField] TMP_Text languageText;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text hintsText;
        [SerializeField] TMP_Text cluesText;
        [SerializeField] TMP_Text challengesText;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text rankText;
        [SerializeField] Selectable firstSelected;

        [Header("Colours")]
        [SerializeField] Color completeColor = new Color(0.29f, 0.87f, 0.5f);
        [SerializeField] Color incompleteColor = new Color(0.98f, 0.8f, 0.08f);

        void Start()
        {
            if (manager == null)
                manager = FindAnyObjectByType<ResultsManager>();
            Show(manager != null ? manager.Results : ResultsManager.Capture());
            UIFocus.Select(firstSelected);
        }

        public void Show(ResultsData results)
        {
            languageText.text = results.language.IsValid() ? results.language.ToDisplayName() : "-";
            scoreText.text = results.score.ToString();
            hintsText.text = results.hintsUsed.ToString();
            cluesText.text = $"{results.cluesCollected} / {results.totalClues}";
            challengesText.text = $"{results.challengesCompleted} / {results.totalChallenges}";
            timeText.text = results.TimeText;
            if (statusText != null)
            {
                statusText.text = results.CompletionStatus;
                statusText.color = results.storyComplete ? completeColor : incompleteColor;
            }
            rankText.text = results.Rank;
        }

        /// <summary>Return to Main Menu button.</summary>
        public void OnMainMenuClicked()
        {
            if (manager != null)
                manager.ReturnToMainMenu();
            else
                SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        /// <summary>Play Again button.</summary>
        public void OnPlayAgainClicked()
        {
            if (manager != null)
                manager.PlayAgain();
            else
                SceneTransitionManager.Load(SceneNames.LanguageSelect);
        }
    }
}
