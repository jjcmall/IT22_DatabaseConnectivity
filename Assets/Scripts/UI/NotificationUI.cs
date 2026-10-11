using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Clues;
using CodeEscape.Cutscenes;
using CodeEscape.Game;
using CodeEscape.Story;

namespace CodeEscape.UI
{
    /// <summary>
    /// Short pop-up messages during gameplay: new clues, new objectives, unlocked locations and
    /// "locked" feedback. Messages queue up and show one at a time (unscaled time).
    /// Lives on the persistent GameSystems object.
    /// </summary>
    public class NotificationUI : MonoBehaviour
    {
        struct Message
        {
            public string header;
            public string body;
            public Color color;
            public float duration;
        }

        public static NotificationUI Instance { get; private set; }

        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text headerText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField, Tooltip("Border / glow tinted with the message colour.")]
        Graphic[] accentGraphics = new Graphic[0];
        [SerializeField, Min(0.01f)] float fadeTime = 0.2f;

        [Header("Colours")]
        [SerializeField] Color clueColor = new Color(0.13f, 0.83f, 0.93f);
        [SerializeField] Color objectiveColor = new Color(0.29f, 0.87f, 0.5f);
        [SerializeField] Color unlockColor = new Color(0.65f, 0.55f, 0.98f);
        [SerializeField] Color warningColor = new Color(0.97f, 0.44f, 0.44f);

        readonly Queue<Message> queue = new Queue<Message>();
        Coroutine running;
        string pendingObjective;
        readonly List<LevelInfo> pendingUnlocks = new List<LevelInfo>();

        public bool IsShowing => running != null;
        public string CurrentHeader => headerText != null ? headerText.text : string.Empty;

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
            if (group != null)
                group.alpha = 0f;
        }

        void OnEnable()
        {
            ClueManager.ClueCollected += HandleClue;
            ObjectiveManager.ObjectiveChanged += HandleObjective;
            LevelManager.LevelUnlocked += HandleUnlock;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            ClueManager.ClueCollected -= HandleClue;
            ObjectiveManager.ObjectiveChanged -= HandleObjective;
            LevelManager.LevelUnlocked -= HandleUnlock;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(string header, string body, Color color, float duration = 3.5f)
        {
            queue.Enqueue(new Message { header = header, body = body, color = color, duration = duration });
            if (running == null && isActiveAndEnabled)
                running = StartCoroutine(Run());
        }

        public void ShowWarning(string header, string body)
        {
            Show(header, body, warningColor, 2.5f);
        }

        public void Clear()
        {
            queue.Clear();
            if (running != null)
                StopCoroutine(running);
            running = null;
            if (group != null)
                group.alpha = 0f;
        }

        IEnumerator Run()
        {
            while (queue.Count > 0)
            {
                Message m = queue.Dequeue();
                headerText.text = m.header;
                headerText.color = m.color;
                bodyText.text = m.body;
                foreach (Graphic g in accentGraphics)
                {
                    if (g == null) continue;
                    Color c = m.color;
                    c.a = g.color.a;
                    g.color = c;
                }

                // Messages wait while a cutscene plays, and pause (hidden) if one starts mid-message.
                while (CutsceneManager.IsActive)
                    yield return null;
                yield return Fade(0f, 1f);
                float shown = 0f;
                while (shown < m.duration)
                {
                    bool hidden = CutsceneManager.IsActive;
                    group.alpha = hidden ? 0f : 1f;
                    if (!hidden)
                        shown += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return Fade(1f, 0f);
            }
            running = null;
        }

        IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                // Stays hidden if a cutscene starts mid-fade.
                group.alpha = CutsceneManager.IsActive ? 0f : Mathf.Lerp(from, to, t / fadeTime);
                yield return null;
            }
            group.alpha = CutsceneManager.IsActive ? 0f : to;
        }

        static bool InGameplay()
        {
            if (GameManager.Instance == null)
                return false;
            GameState s = GameManager.Instance.CurrentState;
            return s == GameState.Playing || s == GameState.Challenge || s == GameState.Paused || s == GameState.Cutscene;
        }

        void HandleClue(ClueData clue)
        {
            Show("NEW CLUE: " + clue.Title.ToUpperInvariant(), clue.Description, clueColor, 6f);
        }

        void HandleObjective(string objective)
        {
            // Shown at the end of the frame, so a clue found in the same frame is announced first.
            pendingObjective = objective;
        }

        void LateUpdate()
        {
            foreach (LevelInfo level in pendingUnlocks)
                Show("LOCATION UNLOCKED", level.displayName, unlockColor, 3.5f);
            pendingUnlocks.Clear();

            if (pendingObjective == null)
                return;
            string objective = pendingObjective;
            pendingObjective = null;
            if (InGameplay() && !string.IsNullOrEmpty(objective))
                Show("NEW OBJECTIVE", objective, objectiveColor, 3.5f);
        }

        void HandleUnlock(LevelInfo level)
        {
            // Shown at the end of the frame, after the clue that unlocked it.
            pendingUnlocks.Add(level);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Messages left over from the previous scene are stale. A new objective raised by entering
            // this scene is still pending (see LateUpdate), so it is shown after this.
            Clear();
        }
    }
}
