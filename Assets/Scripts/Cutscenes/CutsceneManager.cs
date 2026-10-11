using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Audio;
using CodeEscape.Dialogue;
using CodeEscape.Game;
using CodeEscape.Player;
using CodeEscape.Programming;
using CodeEscape.Story;
using CodeEscape.UI;

namespace CodeEscape.Cutscenes
{
    /// <summary>
    /// Plays cutscenes: dialogue, portraits, backgrounds, images, title cards, fades, camera moves,
    /// music, sound effects, waits and scene loads. While one plays the game is frozen
    /// (Time.timeScale 0, GameState.Cutscene) and the steps run on unscaled time.
    /// ESC / SKIP asks for confirmation; skipping still applies the cutscene's story events and
    /// scene load, so progression is never lost. Cutscenes are requested by StoryManager steps or
    /// CutsceneTriggers and queue until gameplay can be interrupted. Lives on GameSystems.
    /// </summary>
    public class CutsceneManager : MonoBehaviour
    {
        const float MaxStep = 1f / 20f;
        /// <summary>Story event recorded when a cutscene finishes or is skipped ("cutscene:intro").</summary>
        public const string PlayedEventPrefix = "cutscene:";

        public static CutsceneManager Instance { get; private set; }

        public static event Action<string> CutsceneStarted;
        /// <summary>Raised with (cutscene ID, skipped).</summary>
        public static event Action<string, bool> CutsceneFinished;

        [SerializeField] List<CutsceneData> cutscenes = new List<CutsceneData>();
        [SerializeField] DialogueManager dialogue;
        [SerializeField] FadeManager fade;

        [Header("Cutscene UI")]
        [SerializeField] CanvasGroup letterbox;
        [SerializeField] GameObject imagePanel;
        [SerializeField] Image image;
        [SerializeField] TMP_Text imageCaption;
        [SerializeField] CanvasGroup titleCard;
        [SerializeField] TMP_Text titleText;
        [SerializeField] GameObject skipButton;
        [SerializeField] GameObject skipConfirm;
        [SerializeField] Selectable skipConfirmFirstSelected;

        readonly Queue<CutsceneData> pending = new Queue<CutsceneData>();
        // The running cutscene is a stack of IEnumerators ticked by Update, so it can be paused
        // (skip confirmation) or dropped (skip) without any stray coroutines.
        readonly Stack<IEnumerator> running = new Stack<IEnumerator>();

        CutsceneData current;
        GameState stateBefore;
        bool finishing;
        bool skipConfirmOpen;
        bool sceneLoaded;
        bool musicChanged;
        int resumeFrame = -1;

        public bool IsPlaying => current != null;
        public static bool IsActive => Instance != null && Instance.IsPlaying;
        public string CurrentId => current != null ? current.Id : string.Empty;
        public int CurrentStepIndex { get; private set; } = -1;
        public CutsceneStepType? CurrentStepType => current != null && CurrentStepIndex >= 0 && CurrentStepIndex < current.Steps.Count
            ? current.Steps[CurrentStepIndex].type
            : (CutsceneStepType?)null;
        public bool IsSkipConfirmOpen => skipConfirmOpen;
        public int PendingCount => pending.Count;
        public IReadOnlyList<CutsceneData> Cutscenes => cutscenes;

        static float Step => Mathf.Min(Time.unscaledDeltaTime, MaxStep);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            CutsceneStarted = null;
            CutsceneFinished = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            HideCutsceneUI();
        }

        void OnEnable()
        {
            StoryManager.CutsceneRequested += HandleCutsceneRequested;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LanguageManager.LanguageChanged += HandleNewGame;
        }

        void OnDisable()
        {
            StoryManager.CutsceneRequested -= HandleCutsceneRequested;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LanguageManager.LanguageChanged -= HandleNewGame;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (IsPlaying)
            {
                HandleSkipInput();
                Tick();
            }
            else if (pending.Count > 0 && CanStart())
            {
                Begin(pending.Dequeue());
            }
        }

        // ---------- Public API ----------

        public CutsceneData Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (CutsceneData data in cutscenes)
            {
                if (data != null && data.Id == id)
                    return data;
            }
            return null;
        }

        public bool HasCutscene(string id) => Find(id) != null;

        public bool HasPlayed(string id)
        {
            return StoryManager.Instance != null && StoryManager.Instance.HasCompletedEvent(PlayedEventPrefix + id);
        }

        /// <summary>Queues a cutscene. It starts as soon as gameplay can be interrupted.</summary>
        public bool Play(string id)
        {
            CutsceneData data = Find(id);
            if (data == null)
            {
                Debug.LogWarning($"[Cutscene] No cutscene with ID '{id}'.");
                return false;
            }
            return Play(data);
        }

        public bool Play(CutsceneData data)
        {
            if (data == null || data == current || pending.Contains(data))
                return false;
            pending.Enqueue(data);
            Debug.Log($"[Cutscene] Queued '{data.Id}'");
            return true;
        }

        /// <summary>Drops cutscenes that are waiting to play (used when progress is reset).</summary>
        public void ClearQueue()
        {
            pending.Clear();
        }

        /// <summary>SKIP button / ESC: asks for confirmation first.</summary>
        public void RequestSkip()
        {
            if (!IsPlaying || finishing || !current.Skippable || skipConfirmOpen)
                return;
            skipConfirmOpen = true;
            SetActive(skipConfirm, true);
            UIFocus.Select(skipConfirmFirstSelected);
        }

        /// <summary>"No" on the confirmation: the cutscene carries on where it was.</summary>
        public void CancelSkip()
        {
            if (!skipConfirmOpen)
                return;
            skipConfirmOpen = false;
            SetActive(skipConfirm, false);
            UIFocus.Clear();
            // The key or click that closed the window must not also continue the dialogue.
            resumeFrame = Time.frameCount;
        }

        /// <summary>"Yes" on the confirmation: ends the cutscene but keeps its story effects.</summary>
        public void ConfirmSkip()
        {
            if (!IsPlaying || finishing)
                return;
            CancelSkip();

            // Apply what the rest of the cutscene would have changed: story events and the scene load.
            string sceneToLoad = null;
            for (int i = Mathf.Max(0, CurrentStepIndex); i < current.Steps.Count; i++)
            {
                CutsceneStep step = current.Steps[i];
                if (step.type == CutsceneStepType.CompleteStoryEvent && StoryManager.Instance != null)
                    StoryManager.Instance.CompleteEvent(step.target);
                else if (step.type == CutsceneStepType.LoadScene)
                    sceneToLoad = step.target;
            }

            Debug.Log($"[Cutscene] Skipped '{current.Id}' at step {CurrentStepIndex}");
            running.Clear();
            running.Push(SkipRoutine(sceneToLoad));
            // Further skip requests (a second click or key press) are ignored while it winds down.
            finishing = true;
            SetActive(skipButton, false);
        }

        // ---------- Running ----------

        bool CanStart()
        {
            SceneTransitionManager transition = SceneTransitionManager.Instance;
            if (transition != null && transition.IsLeavingScene)
                return false;
            if (GameManager.Instance == null)
                return true;
            GameState state = GameManager.Instance.CurrentState;
            return state == GameState.Playing || state == GameState.Intro || state == GameState.Ending;
        }

        void Begin(CutsceneData data)
        {
            current = data;
            CurrentStepIndex = -1;
            finishing = false;
            sceneLoaded = false;
            musicChanged = false;

            stateBefore = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Playing;
            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Cutscene);
            Time.timeScale = 0f;
            UIFocus.Clear();

            dialogue.SetInputCatcher(true);
            SetActive(skipButton, data.Skippable);
            Debug.Log($"[Cutscene] Playing '{data.Id}' ({data.Steps.Count} steps)");
            CutsceneStarted?.Invoke(data.Id);

            running.Clear();
            running.Push(RunSteps(data));
        }

        // Advances the running cutscene until it waits for the next frame.
        void Tick()
        {
            if (skipConfirmOpen || Time.frameCount <= resumeFrame)
                return;
            while (running.Count > 0)
            {
                IEnumerator top = running.Peek();
                bool alive = top.MoveNext();
                // The cutscene may have ended (and cleared the stack) during this step.
                if (running.Count == 0 || running.Peek() != top)
                    break;
                if (!alive)
                {
                    running.Pop();
                    continue;
                }
                if (top.Current is IEnumerator child)
                {
                    running.Push(child);
                    continue;
                }
                break;
            }
        }

        IEnumerator RunSteps(CutsceneData data)
        {
            int i = 0;
            // Instant set-up steps (e.g. "start on black") happen before the letterbox slides in.
            for (; i < data.Steps.Count && IsInstantSetup(data.Steps[i]); i++)
            {
                CurrentStepIndex = i;
                yield return Execute(data.Steps[i]);
            }
            yield return FadeGroup(letterbox, 1f, 0.3f);
            for (; i < data.Steps.Count && !sceneLoaded; i++)
            {
                CurrentStepIndex = i;
                yield return Execute(data.Steps[i]);
            }
            yield return Finish(false);
        }

        static bool IsInstantSetup(CutsceneStep step)
        {
            switch (step.type)
            {
                case CutsceneStepType.FadeOut:
                case CutsceneStepType.FadeIn:
                    return step.duration <= 0f;
                case CutsceneStepType.PlayMusic:
                case CutsceneStepType.StopMusic:
                case CutsceneStepType.ShowBackground:
                    return true;
                default:
                    return false;
            }
        }

        IEnumerator Execute(CutsceneStep step)
        {
            CameraController cam = CameraController.Current;
            AudioManager audio = AudioManager.Instance;
            switch (step.type)
            {
                case CutsceneStepType.Dialogue:
                    if (step.dialogue != null)
                        yield return dialogue.Play(step.dialogue);
                    break;
                case CutsceneStepType.FadeOut:
                    yield return fade.FadeTo(1f, step.duration);
                    break;
                case CutsceneStepType.FadeIn:
                    yield return fade.FadeTo(0f, step.duration);
                    break;
                case CutsceneStepType.CameraPan:
                    if (cam != null)
                        yield return cam.Pan(step.position, step.duration, step.zoom);
                    break;
                case CutsceneStepType.CameraZoom:
                    if (cam != null)
                        yield return cam.Zoom(step.zoom, step.duration);
                    break;
                case CutsceneStepType.CameraFocus:
                    GameObject focus = GameObject.Find(step.target);
                    if (cam != null && focus != null)
                        yield return cam.Focus(focus.transform, step.duration, step.zoom);
                    else if (focus == null)
                        Debug.LogWarning($"[Cutscene] Camera focus target '{step.target}' not found.");
                    break;
                case CutsceneStepType.CameraReset:
                    if (cam != null)
                        yield return cam.ResetView(step.duration);
                    break;
                case CutsceneStepType.PlayMusic:
                    if (audio != null)
                        audio.PlayMusic(step.music);
                    musicChanged = true;
                    break;
                case CutsceneStepType.StopMusic:
                    if (audio != null)
                        audio.StopMusic();
                    musicChanged = true;
                    break;
                case CutsceneStepType.PlaySfx:
                    if (audio != null)
                        audio.PlaySfx(step.sfx);
                    break;
                case CutsceneStepType.Wait:
                    yield return Wait(step.duration);
                    break;
                case CutsceneStepType.ShowImage:
                    ShowImage(step.image, step.text);
                    yield return step.waitForInput ? dialogue.WaitForContinue() : Wait(step.duration);
                    break;
                case CutsceneStepType.HideImage:
                    SetActive(imagePanel, false);
                    break;
                case CutsceneStepType.ShowBackground:
                    dialogue.SetBackground(step.image);
                    break;
                case CutsceneStepType.HideBackground:
                    dialogue.SetBackground(null);
                    break;
                case CutsceneStepType.ShowTitle:
                    titleText.text = DialogueManager.Format(step.text);
                    yield return FadeGroup(titleCard, 1f, 0.6f);
                    yield return step.waitForInput ? dialogue.WaitForContinue() : Wait(step.duration);
                    break;
                case CutsceneStepType.HideTitle:
                    yield return FadeGroup(titleCard, 0f, Mathf.Max(0.01f, step.duration));
                    break;
                case CutsceneStepType.CompleteStoryEvent:
                    if (StoryManager.Instance != null)
                        StoryManager.Instance.CompleteEvent(step.target);
                    break;
                case CutsceneStepType.LoadScene:
                    yield return LoadScene(step.target);
                    break;
            }
        }

        IEnumerator SkipRoutine(string sceneToLoad)
        {
            dialogue.HideBox();
            SetActive(imagePanel, false);
            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                yield return fade.FadeTo(1f, 0.25f);
                yield return LoadScene(sceneToLoad);
            }
            yield return Finish(true);
        }

        IEnumerator LoadScene(string sceneName)
        {
            // A scene that is still being left can't take a new load yet (one that is fading in can).
            while (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLeavingScene)
                yield return null;
            if (!SceneTransitionManager.Load(sceneName))
                yield break;
            while (!sceneLoaded)
                yield return null;
        }

        IEnumerator Finish(bool skipped)
        {
            finishing = true;
            CancelSkip();
            SetActive(skipButton, false);
            dialogue.HideBox();
            SetActive(imagePanel, false);

            if (sceneLoaded)
            {
                // The scene transition's own black screen covers this.
                HideCutsceneUI();
            }
            else
            {
                CameraController cam = CameraController.Current;
                if (cam != null && cam.IsDirected)
                    yield return cam.ResetView(skipped ? 0.35f : 0.6f);
                if (titleCard != null && titleCard.alpha > 0f)
                    yield return FadeGroup(titleCard, 0f, 0.3f);
                if (fade.Alpha > 0f)
                    yield return fade.FadeTo(0f, 0.35f);
                dialogue.SetBackground(null);
                yield return FadeGroup(letterbox, 0f, 0.25f);
            }

            // One more frame, so the key that ended the cutscene isn't also read by gameplay.
            yield return null;
            Complete(skipped);
        }

        void Complete(bool skipped)
        {
            string id = current.Id;
            HideCutsceneUI();
            running.Clear();
            current = null;
            CurrentStepIndex = -1;
            finishing = false;

            if (!sceneLoaded)
            {
                Time.timeScale = 1f;
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Cutscene)
                    GameManager.Instance.SetState(stateBefore);
                if (musicChanged && AudioManager.Instance != null)
                    AudioManager.Instance.PlaySceneMusic();
            }
            // Recorded as a story event, so play-once triggers remember it across saves.
            if (StoryManager.Instance != null)
                StoryManager.Instance.CompleteEvent(PlayedEventPrefix + id);

            Debug.Log($"[Cutscene] Finished '{id}'{(skipped ? " (skipped)" : string.Empty)}");
            CutsceneFinished?.Invoke(id, skipped);
        }

        // ---------- Helpers ----------

        void HandleSkipInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || finishing)
                return;
            if (skipConfirmOpen)
            {
                // Enter is left to the UI: it presses the selected button (NO by default, so a stray Enter never skips).
                if (keyboard.yKey.wasPressedThisFrame)
                    ConfirmSkip();
                else if (keyboard.nKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                    CancelSkip();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                RequestSkip();
            }
        }

        void ShowImage(Sprite sprite, string caption)
        {
            image.sprite = sprite;
            SetActive(image.gameObject, sprite != null);
            imageCaption.text = DialogueManager.Format(caption);
            SetActive(imageCaption.gameObject, !string.IsNullOrEmpty(caption));
            SetActive(imagePanel, true);
        }

        void HideCutsceneUI()
        {
            if (dialogue != null)
                dialogue.HideAll();
            if (fade != null)
                fade.SetAlpha(0f);
            if (letterbox != null)
                letterbox.alpha = 0f;
            if (titleCard != null)
                titleCard.alpha = 0f;
            SetActive(imagePanel, false);
            SetActive(skipButton, false);
            SetActive(skipConfirm, false);
            skipConfirmOpen = false;
        }

        static IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Step;
                yield return null;
            }
        }

        static IEnumerator FadeGroup(CanvasGroup group, float alpha, float duration)
        {
            if (group == null)
                yield break;
            float start = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Step;
                group.alpha = Mathf.Lerp(start, alpha, elapsed / duration);
                yield return null;
            }
            group.alpha = alpha;
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        void HandleCutsceneRequested(string id)
        {
            if (HasCutscene(id))
                Play(id);
            else
                Debug.Log($"[Cutscene] Story asked for '{id}', which has no cutscene.");
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsPlaying)
                sceneLoaded = true;
            // Cutscenes waiting for a gameplay scene must not carry over to the menus.
            if (scene.name == SceneNames.MainMenu || scene.name == SceneNames.LanguageSelect || scene.name == SceneNames.LevelMap)
                pending.Clear();
        }

        void HandleNewGame(ProgrammingLanguage language)
        {
            pending.Clear();
        }
    }
}
