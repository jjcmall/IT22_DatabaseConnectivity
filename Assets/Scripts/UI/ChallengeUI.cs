using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>
    /// Window for programming challenges: question, code, choices or typed answer, hint and result.
    /// It only displays things - every button forwards to ProgrammingChallengeManager, which owns the logic.
    /// When a hazard has caught Alex it shows the CAUGHT banner and hides Close (Esc does nothing): only
    /// a correct answer ends it.
    /// </summary>
    public class ChallengeUI : MonoBehaviour
    {
        const string CodeFormat = "<mspace=0.66em><noparse>{0}</noparse></mspace>";
        static readonly string[] Letters = { "A", "B", "C", "D" };

        [SerializeField] ProgrammingChallengeManager manager;
        [SerializeField, Tooltip("Shown while a challenge is open. Keep this script's own GameObject active.")]
        GameObject overlay;

        [Header("Header")]
        [SerializeField] TMP_Text tagsText;
        [SerializeField] TMP_Text questionTypeText;
        [SerializeField] TMP_Text scoreText;

        [Header("Question")]
        [SerializeField] TMP_Text questionText;
        [SerializeField] GameObject codePanel;
        [SerializeField] TMP_Text codeText;

        [Header("Answer")]
        [SerializeField] GameObject choicesRoot;
        [SerializeField] Button[] choiceButtons = new Button[QuestionData.MaxChoices];
        [SerializeField] TMP_Text[] choiceLabels = new TMP_Text[QuestionData.MaxChoices];
        [SerializeField] GameObject typedAnswerRoot;
        [SerializeField] TMP_InputField answerInput;

        [Header("Result")]
        [SerializeField] GameObject resultRoot;
        [SerializeField] TMP_Text resultTitleText;
        [SerializeField] TMP_Text explanationText;
        [SerializeField] TMP_Text clueRewardText;

        [Header("Hint")]
        [SerializeField] GameObject hintPanel;
        [SerializeField] TMP_Text hintText;

        [Header("Buttons")]
        [SerializeField] Button hintButton;
        [SerializeField] TMP_Text hintButtonLabel;
        [SerializeField] Button submitButton;
        [SerializeField] Button retryButton;
        [SerializeField] Button continueButton;
        [SerializeField] TMP_Text continueLabel;
        [SerializeField, Tooltip("Hidden while Alex is caught: the challenge must be solved.")]
        Button closeButton;

        [Header("Caught by a hazard")]
        [SerializeField, Tooltip("\"CAUGHT! SOLVE THE CODE TO ESCAPE!\" - shown only for hazard challenges.")]
        GameObject caughtBanner;
        [SerializeField] TMP_Text caughtText;
        [SerializeField, Tooltip("Window border and glow, tinted red while Alex is caught.")]
        Graphic windowBorder;
        [SerializeField] Graphic windowGlow;
        [SerializeField] Color caughtColor = new Color(0.97f, 0.27f, 0.27f);

        [Header("Colours")]
        [SerializeField] Color choiceColor = new Color(0.12f, 0.23f, 0.37f);
        [SerializeField] Color selectedChoiceColor = new Color(0.15f, 0.39f, 0.92f);
        [SerializeField] Color correctColor = new Color(0.29f, 0.87f, 0.5f);
        [SerializeField] Color incorrectColor = new Color(0.97f, 0.44f, 0.44f);

        const string CaughtMessage = "CAUGHT!  SOLVE THE CODE TO ESCAPE!";

        QuestionData question;
        int selectedChoice = -1;
        bool hintShown;
        bool caught;
        string continueDefault;
        Color borderDefault;
        Color glowDefault;

        public bool IsOpen => overlay != null && overlay.activeSelf;
        public int SelectedChoice => selectedChoice;
        public bool IsCaught => IsOpen && caught;

        ProgrammingChallengeManager Manager => manager != null ? manager : ProgrammingChallengeManager.Instance;
        bool IsTyped => question != null && question.AnswerFormat == AnswerFormat.TypedAnswer;

        void Awake()
        {
            if (overlay != null)
                overlay.SetActive(false);
            if (continueLabel != null)
                continueDefault = continueLabel.text;
            if (windowBorder != null)
                borderDefault = windowBorder.color;
            if (windowGlow != null)
                glowDefault = windowGlow.color;
        }

        void Update()
        {
            // Esc closes an obstacle's challenge, but never frees Alex from a hazard.
            Keyboard keyboard = Keyboard.current;
            if (IsOpen && !caught && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                OnCloseClicked();
        }

        // ---------- Called by ProgrammingChallengeManager ----------

        public void Show(QuestionData data, string obstacleName, bool hintAlreadyUnlocked, bool caughtByHazard = false)
        {
            question = data;
            hintShown = false;
            caught = caughtByHazard;
            if (closeButton != null)
                closeButton.gameObject.SetActive(!caught);
            if (caughtBanner != null)
                caughtBanner.SetActive(caught);
            if (caughtText != null)
                caughtText.text = CaughtMessage;
            if (continueLabel != null)
                continueLabel.text = caught ? "ESCAPE" : continueDefault;
            if (windowBorder != null)
                windowBorder.color = caught ? new Color(caughtColor.r, caughtColor.g, caughtColor.b, borderDefault.a) : borderDefault;
            if (windowGlow != null)
                windowGlow.color = caught ? new Color(caughtColor.r, caughtColor.g, caughtColor.b, glowDefault.a) : glowDefault;

            tagsText.text = $"{obstacleName}  •  {data.Language.ToDisplayName()}  •  {data.Topic}  •  {data.Difficulty}".ToUpperInvariant();
            questionTypeText.text = data.QuestionType.ToDisplayName().ToUpperInvariant();
            questionText.text = data.QuestionText;

            bool hasCode = !string.IsNullOrWhiteSpace(data.Code);
            codePanel.SetActive(hasCode);
            codeText.text = hasCode ? string.Format(CodeFormat, data.Code) : string.Empty;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool used = data.AnswerFormat == AnswerFormat.Choices && i < data.ChoiceCount;
                choiceButtons[i].gameObject.SetActive(used);
                if (used)
                    choiceLabels[i].text = FormatChoice(i, data.GetChoice(i), data.MonospaceChoices);
            }
            answerInput.SetTextWithoutNotify(string.Empty);

            hintPanel.SetActive(false);
            hintButtonLabel.text = $"Hint (-{ProgrammingChallengeManager.HintPenalty})";
            if (hintAlreadyUnlocked)
                ShowHint(data.Hint);

            overlay.SetActive(true);
            ShowAnswerInput();
        }

        /// <summary>Shows the answer controls (first attempt and every retry).</summary>
        public void ShowAnswerInput()
        {
            selectedChoice = -1;
            RefreshChoiceColours();

            choicesRoot.SetActive(!IsTyped);
            typedAnswerRoot.SetActive(IsTyped);
            resultRoot.SetActive(false);

            submitButton.gameObject.SetActive(true);
            retryButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            hintButton.gameObject.SetActive(question.HasHint);
            hintButton.interactable = !hintShown;
            UpdateSubmitState();

            UIFocus.Clear();
            if (IsTyped)
                answerInput.ActivateInputField();
        }

        public void ShowResult(bool correct, string explanation, int pointsAwarded, string clueReward, bool caughtByHazard = false)
        {
            choicesRoot.SetActive(false);
            typedAnswerRoot.SetActive(false);
            resultRoot.SetActive(true);

            if (caughtByHazard)
                resultTitleText.text = correct ? "CORRECT!  YOU'RE FREE - CAREFUL THIS TIME!" : "INCORRECT - STILL CAUGHT. READ THE EXPLANATION AND TRY AGAIN";
            else
                resultTitleText.text = correct
                    ? (pointsAwarded > 0 ? $"CORRECT!  +{pointsAwarded} POINTS" : "CORRECT!")
                    : "INCORRECT - READ THE EXPLANATION, THEN TRY AGAIN";
            resultTitleText.color = correct ? correctColor : incorrectColor;
            explanationText.text = explanation;

            bool hasClue = correct && !string.IsNullOrWhiteSpace(clueReward);
            clueRewardText.gameObject.SetActive(hasClue);
            clueRewardText.text = hasClue ? "NEW CLUE: " + clueReward : string.Empty;

            submitButton.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(!correct);
            continueButton.gameObject.SetActive(correct);
            hintButton.gameObject.SetActive(!correct && question.HasHint);
            UIFocus.Clear();
        }

        public void ShowHint(string hint)
        {
            hintShown = true;
            hintPanel.SetActive(true);
            hintText.text = "HINT: " + hint;
            hintButtonLabel.text = "Hint shown";
            hintButton.interactable = false;
        }

        public void SetScore(int score, int hintsUsed)
        {
            scoreText.text = $"Score: {score}    Hints used: {hintsUsed}";
        }

        public void Hide()
        {
            if (overlay != null)
                overlay.SetActive(false);
            question = null;
            selectedChoice = -1;
            caught = false;
            if (caughtBanner != null)
                caughtBanner.SetActive(false);
            if (closeButton != null)
                closeButton.gameObject.SetActive(true);
            UIFocus.Clear();
        }

        // ---------- Button / input callbacks (wired in the Inspector) ----------

        public void OnChoiceClicked(int index)
        {
            if (question == null || index < 0 || index >= question.ChoiceCount)
                return;
            selectedChoice = index;
            RefreshChoiceColours();
            UpdateSubmitState();
        }

        public void OnAnswerTextChanged(string text)
        {
            UpdateSubmitState();
        }

        /// <summary>Enter pressed in the typed-answer field.</summary>
        public void OnAnswerSubmitted(string text)
        {
            OnSubmitClicked();
        }

        public void OnSubmitClicked()
        {
            if (question == null || !submitButton.gameObject.activeInHierarchy || Manager == null)
                return;

            if (IsTyped)
            {
                if (!string.IsNullOrWhiteSpace(answerInput.text))
                    Manager.SubmitTypedAnswer(answerInput.text);
            }
            else if (selectedChoice >= 0)
            {
                Manager.SubmitChoice(selectedChoice);
            }
        }

        public void OnHintClicked()
        {
            if (Manager != null)
                Manager.UseHint();
        }

        public void OnRetryClicked()
        {
            if (Manager != null)
                Manager.Retry();
        }

        public void OnContinueClicked()
        {
            if (Manager != null)
                Manager.Continue();
        }

        public void OnCloseClicked()
        {
            if (Manager != null)
                Manager.CloseChallenge();
        }

        // ---------- Helpers ----------

        void RefreshChoiceColours()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null && choiceButtons[i].targetGraphic != null)
                    choiceButtons[i].targetGraphic.color = i == selectedChoice ? selectedChoiceColor : choiceColor;
            }
        }

        void UpdateSubmitState()
        {
            if (question == null)
                return;
            submitButton.interactable = IsTyped ? !string.IsNullOrWhiteSpace(answerInput.text) : selectedChoice >= 0;
        }

        static string FormatChoice(int index, string text, bool monospace)
        {
            string body = monospace ? string.Format(CodeFormat, text) : $"<noparse>{text}</noparse>";
            return $"<b>{Letters[index]}</b>    {body}";
        }
    }
}
