using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Game;

namespace CodeEscape.Cloud
{
    /// <summary>
    /// The cloud save's on-screen parts, built in code on its own overlay canvas so no scene needs editing:
    /// a gate that blocks the game until Cloud Save has initialised and loaded (with an error and Retry),
    /// a save-status chip with Retry, and a progression panel on the main menu and level map that shows the
    /// data last retrieved from, or confirmed by, Cloud Save.
    /// </summary>
    public class ProgressionUI : MonoBehaviour
    {
        const float SavedMessageSeconds = 4f;

        static readonly Color Panel = new Color(0.04f, 0.09f, 0.16f, 0.93f);
        static readonly Color Accent = new Color(0.36f, 0.72f, 1f);

        Font font;
        GameObject gate;
        Text gateTitle;
        Text gateDetail;
        Button gateRetry;
        GameObject chip;
        Text chipText;
        Button chipRetry;
        GameObject panel;
        Text panelText;
        GameObject panelBody;
        Text panelToggleLabel;
        bool dirty = true;
        bool panelCollapsed;
        float savedShownUntil;
        CloudSaveStatus lastStatus = CloudSaveStatus.Synced;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        void OnEnable()
        {
            CloudProgressService.Changed += OnChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            CloudProgressService.Changed -= OnChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnChanged()
        {
            dirty = true;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            dirty = true;
        }

        void Update()
        {
            CloudProgressService service = CloudProgressService.Instance;
            if (service == null)
                return;

            if (service.SaveStatus != lastStatus)
            {
                if (service.SaveStatus == CloudSaveStatus.Synced && lastStatus == CloudSaveStatus.Saving)
                    savedShownUntil = Time.unscaledTime + SavedMessageSeconds;
                lastStatus = service.SaveStatus;
            }

            bool gated = service.State != CloudState.Ready;
            if (gated)
                KeepFocusOnGate();
            if (dirty || (chip.activeSelf && Time.unscaledTime > savedShownUntil && service.SaveStatus == CloudSaveStatus.Synced))
            {
                dirty = false;
                Refresh(service);
            }
        }

        // ---------- Refresh ----------

        void Refresh(CloudProgressService service)
        {
            bool gated = service.State != CloudState.Ready;
            gate.SetActive(gated);
            if (gated)
            {
                bool failed = service.State == CloudState.Failed;
                gateTitle.text = failed ? "Cannot connect to Cloud Save" : "Connecting to Unity Cloud Save";
                gateDetail.text = failed
                    ? service.ConnectionError + "\n\nCode Escape needs an internet connection to load your progress. Gameplay is locked until it loads."
                    : StateMessage(service.State);
                gateRetry.gameObject.SetActive(failed);
                if (failed && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(gateRetry.gameObject);
            }

            RefreshChip(service);
            RefreshPanel(service);
        }

        static string StateMessage(CloudState state)
        {
            switch (state)
            {
                case CloudState.Starting: return "Starting Unity Gaming Services...";
                case CloudState.SigningIn: return "Signing in anonymously (no account needed)...";
                case CloudState.Loading: return "Loading your progress...";
                default: return string.Empty;
            }
        }

        void RefreshChip(CloudProgressService service)
        {
            string message = null;
            bool showRetry = false;
            switch (service.SaveStatus)
            {
                case CloudSaveStatus.Unsaved:
                    message = "<color=#ffd24a>Unsaved changes</color> - not in the cloud yet";
                    break;
                case CloudSaveStatus.Saving:
                    message = "<color=#9ad7ff>Saving to cloud...</color>";
                    break;
                case CloudSaveStatus.Failed:
                    message = "<color=#ff7b7b>Cloud save failed</color>\n" + Shorten(service.SaveError, 110);
                    showRetry = true;
                    break;
                case CloudSaveStatus.Synced:
                    if (Time.unscaledTime <= savedShownUntil)
                    {
                        message = "<color=#6ee89a>Saved to cloud</color> " + LocalTime(service.LastConfirmedSaveUtc);
                        if (!string.IsNullOrEmpty(service.LastUnlockedAchievement))
                            message += "\nAchievement: " + service.LastUnlockedAchievement;
                    }
                    break;
            }
            chip.SetActive(message != null && service.State == CloudState.Ready);
            chipText.text = message ?? string.Empty;
            chipRetry.gameObject.SetActive(showRetry);
        }

        void RefreshPanel(CloudProgressService service)
        {
            string scene = SceneManager.GetActiveScene().name;
            bool visible = service.State == CloudState.Ready && service.Record != null
                           && (scene == SceneNames.MainMenu || scene == SceneNames.LevelMap);
            panel.SetActive(visible);
            panelBody.SetActive(visible && !panelCollapsed);
            panelToggleLabel.text = panelCollapsed ? "CLOUD PROGRESS  +" : "CLOUD PROGRESS  -";
            if (!visible || panelCollapsed)
                return;

            PlayerProgressData r = service.Record;
            LevelManager levels = LevelManager.Instance;
            var sb = new StringBuilder();
            switch (service.SaveStatus)
            {
                case CloudSaveStatus.Synced:
                    sb.AppendLine("<color=#6ee89a>All changes saved</color> " + LocalTime(service.LastConfirmedSaveUtc));
                    break;
                case CloudSaveStatus.Failed:
                    sb.AppendLine("<color=#ff7b7b>Latest changes NOT saved</color>");
                    break;
                default:
                    sb.AppendLine("<color=#ffd24a>Latest changes not saved yet</color>");
                    break;
            }
            sb.AppendLine($"<color=#8fa8c0>Player ID</color>  {ShortId(r.player_id)}");
            sb.AppendLine($"<color=#8fa8c0>Current level</color>  {LevelLabel(levels, r.current_level)}");
            sb.AppendLine($"<color=#8fa8c0>Highest unlocked</color>  {LevelLabel(levels, r.highest_unlocked_level)}");
            int total = levels != null ? levels.LevelCount : 0;
            sb.AppendLine($"<color=#8fa8c0>Completed</color>  {r.completed_levels.Count}{(total > 0 ? " / " + total : string.Empty)}");
            foreach (string id in r.completed_levels)
                sb.AppendLine("   - " + LevelLabel(levels, id));
            sb.AppendLine($"<color=#8fa8c0>Total score</color>  {r.total_score}");
            sb.Append("<color=#8fa8c0>Achievements</color>  ");
            if (r.achievements.Count == 0)
                sb.Append("none yet");
            foreach (string id in r.achievements)
                sb.Append("\n   - " + Achievements.NameOf(id));
            sb.AppendLine();
            sb.Append("<color=#8fa8c0>Last cloud update</color>  " + FormatStamp(r.last_played));
            panelText.text = sb.ToString();
        }

        static string LevelLabel(LevelManager levels, string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return "-";
            LevelInfo info = levels != null ? levels.GetInfoById(levelId) : null;
            return info != null ? $"Level {levels.IndexOf(info) + 1} - {info.displayName}" : levelId;
        }

        static string ShortId(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : (id.Length > 10 ? id.Substring(0, 10) + "..." : id);
        }

        static string LocalTime(DateTime? utc)
        {
            return utc.HasValue ? "(" + utc.Value.ToLocalTime().ToString("HH:mm:ss") + ")" : string.Empty;
        }

        static string FormatStamp(string iso)
        {
            return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t)
                ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";
        }

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        // While the gate is up, keyboard/gamepad focus must not reach the menu behind it.
        void KeepFocusOnGate()
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return;
            GameObject target = gateRetry.gameObject.activeInHierarchy ? gateRetry.gameObject : null;
            if (es.currentSelectedGameObject != target)
                es.SetSelectedGameObject(target);
        }

        // ---------- Building the overlay ----------

        void BuildCanvas()
        {
            var canvasGo = new GameObject("CloudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = canvasGo.GetComponent<RectTransform>();

            // Gate: full screen, swallows clicks.
            gate = NewImage("Gate", root, new Color(0.02f, 0.05f, 0.1f, 0.97f));
            Stretch(gate.GetComponent<RectTransform>());
            gateTitle = NewText("Title", gate.transform, 44, TextAnchor.MiddleCenter, Color.white);
            Place(gateTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1400, 90), new Vector2(0, 150));
            gateDetail = NewText("Detail", gate.transform, 28, TextAnchor.UpperCenter, new Color(0.8f, 0.88f, 0.96f));
            Place(gateDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1300, 300), new Vector2(0, -20));
            gateRetry = NewButton("Retry", gate.transform, "RETRY", new Vector2(320, 80), new Vector2(0, -280), 34, () =>
            {
                if (CloudProgressService.Instance != null)
                    CloudProgressService.Instance.RetryConnect();
            });
            gateRetry.gameObject.SetActive(false);

            // Status chip (top right).
            chip = NewImage("StatusChip", root, Panel);
            Place(chip.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(560, 120), new Vector2(-300, -80));
            chipText = NewText("Text", chip.transform, 24, TextAnchor.MiddleLeft, Color.white);
            Stretch(chipText.rectTransform, 16, 8, 190, 8);
            chipRetry = NewButton("Retry", chip.transform, "RETRY", new Vector2(150, 60), new Vector2(0, 0), 26, () =>
            {
                if (CloudProgressService.Instance != null)
                    CloudProgressService.Instance.RetrySave();
            });
            var rr = chipRetry.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(1f, 0.5f);
            rr.anchoredPosition = new Vector2(-95, 0);
            chip.SetActive(false);

            // Progression panel (bottom left).
            panel = NewImage("ProgressPanel", root, Panel);
            Place(panel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(620, 600), new Vector2(340, 320));
            var rt = panel.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(24, 24);
            Button toggle = NewButton("Toggle", panel.transform, "CLOUD PROGRESS  -", new Vector2(620, 54), Vector2.zero, 24, () =>
            {
                panelCollapsed = !panelCollapsed;
                SizePanel();
                dirty = true;
            });
            var tr = toggle.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = Vector2.zero;
            panelToggleLabel = toggle.GetComponentInChildren<Text>();
            panelBody = new GameObject("Body", typeof(RectTransform));
            panelBody.transform.SetParent(panel.transform, false);
            Stretch(panelBody.GetComponent<RectTransform>(), 16, 12, 16, 62);
            panelText = NewText("Text", panelBody.transform, 22, TextAnchor.UpperLeft, Color.white);
            Stretch(panelText.rectTransform, 0, 0, 0, 0);
            panelText.verticalOverflow = VerticalWrapMode.Truncate;
            panel.SetActive(false);
            SizePanel();
        }

        void SizePanel()
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(620, panelCollapsed ? 54 : 600);
        }

        GameObject NewImage(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            return text;
        }

        Button NewButton(string name, Transform parent, string label, Vector2 size, Vector2 position, int fontSize, Action onClick)
        {
            GameObject go = NewImage(name, parent.GetComponent<RectTransform>(), new Color(0.12f, 0.36f, 0.6f, 1f));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            var button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Accent;
            colors.selectedColor = Accent;
            colors.pressedColor = new Color(0.8f, 0.9f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            Text text = NewText("Label", go.transform, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
