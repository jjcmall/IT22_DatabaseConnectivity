using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CodeEscape.Programming;

namespace CodeEscape.Dialogue
{
    /// <summary>
    /// Shows dialogue: name plate, portrait (left or right), background, typing effect and continue.
    /// Space, Enter, E or a click finishes the typing, then moves to the next line. Playback is an
    /// IEnumerator driven by the CutsceneManager on unscaled time. Lives on the persistent cutscene canvas.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        const string LanguageToken = "{LANGUAGE}";
        const float BlinkInterval = 0.5f;

        public static DialogueManager Instance { get; private set; }

        [Header("Dialogue Box")]
        [SerializeField] GameObject dialogueRoot;
        [SerializeField] GameObject namePlate;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField, Tooltip("Blinks in the dialogue box once a line is fully typed.")]
        GameObject continueIndicator;

        [Header("Portraits")]
        [SerializeField] GameObject leftPortraitRoot;
        [SerializeField] Image leftPortrait;
        [SerializeField] GameObject rightPortraitRoot;
        [SerializeField] Image rightPortrait;

        [Header("Background")]
        [SerializeField, Tooltip("Full-screen image behind the dialogue box.")]
        Image background;

        [Header("Input")]
        [SerializeField, Tooltip("Full-screen invisible button: a click anywhere continues.")]
        GameObject clickCatcher;
        [SerializeField, Tooltip("Shown when waiting for input without the dialogue box (images, title cards).")]
        GameObject screenContinueHint;

        [Header("Typing")]
        [SerializeField, Min(1f)] float charactersPerSecond = 45f;
        [SerializeField] Color speechColor = new Color(0.9f, 0.93f, 0.95f);
        [SerializeField] Color narrationColor = new Color(0.72f, 0.78f, 0.84f);

        bool continueRequested;

        public bool IsShowing => dialogueRoot != null && dialogueRoot.activeSelf;
        public bool IsTyping { get; private set; }
        public bool IsWaitingForInput { get; private set; }
        public string CurrentSpeaker => nameText != null && namePlate != null && namePlate.activeSelf ? nameText.text : string.Empty;
        public string CurrentText => bodyText != null ? bodyText.text : string.Empty;
        public int VisibleCharacters => bodyText != null ? bodyText.maxVisibleCharacters : 0;
        public Sprite CurrentBackground => background != null && background.gameObject.activeSelf ? background.sprite : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            HideAll();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>"{LANGUAGE}" becomes the selected programming language.</summary>
        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            ProgrammingLanguage language = LanguageManager.Current;
            return text.Replace(LanguageToken, language.IsValid() ? language.ToDisplayName() : "code");
        }

        /// <summary>Click catcher / continue button.</summary>
        public void RequestContinue()
        {
            continueRequested = true;
        }

        public IEnumerator Play(DialogueData data)
        {
            if (data == null)
                yield break;
            foreach (DialogueLine line in data.Lines)
                yield return PlayLine(line);
            HideBox();
        }

        public IEnumerator PlayLine(DialogueLine line)
        {
            if (line.background != null)
                SetBackground(line.background);
            ShowLine(line);

            // Typing effect: reveal the text a few characters at a time (any continue input skips it).
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;
            bodyText.maxVisibleCharacters = 0;
            continueRequested = false;
            IsTyping = true;
            SetActive(continueIndicator, false);

            float shown = 0f;
            yield return null;
            while (shown < total && !ContinuePressed())
            {
                shown += Mathf.Min(Time.unscaledDeltaTime, 0.05f) * charactersPerSecond;
                bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            bodyText.maxVisibleCharacters = total;
            IsTyping = false;

            yield return WaitForContinue();
        }

        /// <summary>Waits for Space / Enter / E / a click. Starts a frame later so one press can't count twice.</summary>
        public IEnumerator WaitForContinue()
        {
            IsWaitingForInput = true;
            GameObject indicator = IsShowing ? continueIndicator : screenContinueHint;
            continueRequested = false;
            float blink = 0f;
            SetActive(indicator, true);
            yield return null;
            while (!ContinuePressed())
            {
                blink += Time.unscaledDeltaTime;
                SetActive(indicator, Mathf.Repeat(blink, BlinkInterval * 2f) < BlinkInterval);
                yield return null;
            }
            SetActive(indicator, false);
            IsWaitingForInput = false;
        }

        public void SetBackground(Sprite sprite)
        {
            if (background == null)
                return;
            background.sprite = sprite;
            SetActive(background.gameObject, sprite != null);
        }

        /// <summary>The full-screen click catcher also blocks clicks on the game while a cutscene runs.</summary>
        public void SetInputCatcher(bool active)
        {
            SetActive(clickCatcher, active);
        }

        public void HideBox()
        {
            SetActive(dialogueRoot, false);
            SetActive(continueIndicator, false);
            IsTyping = false;
        }

        public void HideAll()
        {
            HideBox();
            SetActive(screenContinueHint, false);
            SetBackground(null);
            SetInputCatcher(false);
            IsWaitingForInput = false;
        }

        void ShowLine(DialogueLine line)
        {
            bool narration = string.IsNullOrWhiteSpace(line.speaker);
            SetActive(dialogueRoot, true);
            SetActive(namePlate, !narration);
            if (!narration)
            {
                nameText.text = line.speaker.ToUpperInvariant();
                nameText.color = line.nameColor;
            }

            bool hasPortrait = !narration && line.portrait != null;
            bool left = line.side == PortraitSide.Left;
            SetPortrait(leftPortraitRoot, leftPortrait, hasPortrait && left ? line.portrait : null);
            SetPortrait(rightPortraitRoot, rightPortrait, hasPortrait && !left ? line.portrait : null);

            bodyText.text = Format(line.text);
            bodyText.fontStyle = narration ? FontStyles.Italic : FontStyles.Normal;
            bodyText.color = narration ? narrationColor : speechColor;
        }

        static void SetPortrait(GameObject root, Image image, Sprite sprite)
        {
            SetActive(root, sprite != null);
            if (image != null && sprite != null)
                image.sprite = sprite;
        }

        bool ContinuePressed()
        {
            if (continueRequested)
            {
                continueRequested = false;
                return true;
            }
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
