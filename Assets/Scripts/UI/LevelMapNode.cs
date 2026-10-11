using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CodeEscape.Game;

namespace CodeEscape.UI
{
    /// <summary>
    /// One location card on the level map: a picture of the place with its level number, name, a short
    /// description and its status - cleared (check badge, green frame), next to play (accent frame, pulsing
    /// glow and NEXT ribbon) or locked (dimmed picture with a lock). The card lifts and grows a little while
    /// highlighted by the mouse or keyboard. Animations use unscaled time.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LevelMapNode : MonoBehaviour
    {
        [Header("Card")]
        [SerializeField, Tooltip("Scaled up and lifted while the card is highlighted.")]
        RectTransform cardRoot;
        [SerializeField] Image border;
        [SerializeField, Tooltip("Glow behind the card: shown while highlighted, pulses on the level to play next.")]
        Image glow;

        [Header("Picture")]
        [SerializeField] Image picture;
        [SerializeField, Tooltip("Darkens the picture of a locked level.")]
        Graphic pictureDim;
        [SerializeField, Tooltip("Lock icon and text over the picture of a locked level.")]
        GameObject lockOverlay;

        [Header("Badges")]
        [SerializeField] Image numberBadge;
        [SerializeField] TMP_Text numberText;
        [SerializeField, Tooltip("Corner badge: a check on a cleared level.")]
        Image badge;
        [SerializeField] Image badgeIcon;
        [SerializeField, Tooltip("\"NEXT\" ribbon above the level to play next.")]
        RectTransform nextMarker;
        [SerializeField] Graphic nextMarkerFill;

        [Header("Labels")]
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text statusText;

        [Header("Animation")]
        [SerializeField] float highlightScale = 1.06f;
        [SerializeField] float highlightLift = 10f;
        [SerializeField, Min(0.1f)] float pulsePeriod = 1.6f;

        static readonly Color LockedText = new Color(0.47f, 0.5f, 0.56f);
        static readonly Color LockedPicture = new Color(0.5f, 0.53f, 0.58f);

        Button button;
        MenuButtonFX fx;
        Vector2 cardRest;
        Vector2 markerRest;
        bool isNext;
        Color mainColor = Color.white;
        Color glowColor = Color.white;
        float flashUntil = -1f;

        public Button Button => button != null ? button : button = GetComponent<Button>();
        public LevelMapState State { get; private set; }

        void Awake()
        {
            button = GetComponent<Button>();
            fx = GetComponent<MenuButtonFX>();
            if (cardRoot != null)
                cardRest = cardRoot.anchoredPosition;
            if (nextMarker != null)
                markerRest = nextMarker.anchoredPosition;
        }

        /// <summary>Shows the level's picture, number, name, description, status text and the look of its state.</summary>
        public void Show(int number, string levelName, string description, Sprite locationPicture, LevelMapState state,
            bool next, string status, Color accent, Color cleared, Color locked, Sprite checkIcon)
        {
            State = state;
            isNext = next && state == LevelMapState.Available;
            bool isLocked = state == LevelMapState.Locked;
            mainColor = state == LevelMapState.Completed ? cleared : isLocked ? locked : accent;
            glowColor = isNext ? accent : mainColor;

            if (picture != null)
            {
                picture.sprite = locationPicture;
                picture.enabled = locationPicture != null;
                picture.color = isLocked ? LockedPicture : Color.white;
            }
            if (pictureDim != null)
                pictureDim.color = new Color(0.02f, 0.04f, 0.07f, isLocked ? 0.55f : 0f);
            if (lockOverlay != null)
                lockOverlay.SetActive(isLocked);

            if (numberText != null)
            {
                numberText.text = number.ToString();
                numberText.color = isLocked ? new Color(0.75f, 0.78f, 0.82f) : new Color(0.04f, 0.07f, 0.11f);
            }
            if (numberBadge != null)
                numberBadge.color = isLocked ? new Color(0.22f, 0.25f, 0.3f) : mainColor;
            if (levelLabel != null)
            {
                levelLabel.text = $"LEVEL {number}";
                levelLabel.color = isLocked ? LockedText : mainColor;
            }
            if (nameText != null)
            {
                nameText.text = levelName;
                nameText.color = isLocked ? LockedText : new Color(0.93f, 0.95f, 0.97f);
            }
            if (descriptionText != null)
            {
                descriptionText.text = description;
                descriptionText.color = isLocked ? new Color(0.4f, 0.43f, 0.48f) : new Color(0.68f, 0.73f, 0.8f);
            }
            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = isLocked ? LockedText : mainColor;
            }

            bool isCleared = state == LevelMapState.Completed;
            if (badge != null)
            {
                badge.gameObject.SetActive(isCleared);
                badge.color = cleared;
                if (badgeIcon != null)
                {
                    badgeIcon.sprite = checkIcon;
                    badgeIcon.color = new Color(0.03f, 0.1f, 0.06f);
                }
            }

            if (nextMarker != null)
                nextMarker.gameObject.SetActive(isNext);
            if (nextMarkerFill != null)
                nextMarkerFill.color = accent;
            Animate();
        }

        /// <summary>A short burst of glow (a level that was just cleared or unlocked).</summary>
        public void Flash(Color color, float duration = 2.4f)
        {
            glowColor = color;
            flashUntil = Time.unscaledTime + duration;
        }

        void Update()
        {
            Animate();
        }

        void Animate()
        {
            float highlight = fx != null ? fx.HighlightAmount : 0f;
            if (cardRoot != null)
            {
                cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, highlightScale, highlight);
                cardRoot.anchoredPosition = cardRest + new Vector2(0f, highlightLift * highlight);
            }

            // Glow: full while highlighted; a slow pulse on the next level; a quick one after a flash.
            bool flashing = Time.unscaledTime < flashUntil;
            float pulse = 0f;
            if (isNext || flashing)
            {
                float t = Mathf.Repeat(Time.unscaledTime / (flashing ? pulsePeriod * 0.5f : pulsePeriod), 1f);
                pulse = (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f)) * (flashing ? 0.9f : 0.55f);
            }
            if (glow != null)
            {
                Color g = glowColor;
                g.a = Mathf.Max(highlight * 0.95f, pulse);
                glow.color = g;
            }
            if (border != null)
            {
                Color b = mainColor;
                b.a = State == LevelMapState.Locked ? Mathf.Lerp(0.45f, 0.8f, highlight) : Mathf.Lerp(0.75f, 1f, highlight);
                border.color = b;
            }

            if (nextMarker != null && isNext)
                nextMarker.anchoredPosition = markerRest + new Vector2(0f, Mathf.Sin(Time.unscaledTime * 3f) * 5f);
        }
    }
}
