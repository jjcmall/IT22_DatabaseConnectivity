using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CodeEscape.Audio;

namespace CodeEscape.UI
{
    /// <summary>
    /// Neon hover effect for menu buttons: fades in a glow, brightens the border, slides the content a
    /// little and plays UI sounds. Uses unscaled time, so it also works while the game is paused.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler,
        IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        // Ignore the selection that menus make when they first open, so they don't beep on load.
        const float SilentAfterEnable = 0.25f;

        [SerializeField] Graphic glow;
        [SerializeField] Graphic border;
        [SerializeField, Tooltip("Optional. Slides sideways while highlighted.")]
        RectTransform content;
        [SerializeField] float slideDistance = 8f;
        [SerializeField, Range(0f, 1f)] float idleBorderAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] float glowAlpha = 0.9f;
        [SerializeField, Min(0.01f)] float fadeTime = 0.12f;
        [SerializeField] bool playHoverSound = true;
        [SerializeField] bool playClickSound = true;

        Selectable selectable;
        Vector2 contentRest;
        bool hovered;
        bool selected;
        float amount;
        float enabledAt;

        public bool IsHighlighted => (hovered || selected) && selectable != null && selectable.IsInteractable();
        public float HighlightAmount => amount;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (content != null)
                contentRest = content.anchoredPosition;
        }

        void OnEnable()
        {
            enabledAt = Time.unscaledTime;
            Apply(amount);
        }

        void OnDisable()
        {
            hovered = false;
            selected = false;
            amount = 0f;
            Apply(0f);
        }

        void Update()
        {
            float target = IsHighlighted ? 1f : 0f;
            if (Mathf.Approximately(amount, target))
                return;
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / fadeTime);
            Apply(amount);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (!selected)
                PlayHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            // Keyboard navigation: light up and beep. (Mouse clicks also select, but hover already beeped.)
            if (!hovered && Time.unscaledTime - enabledAt > SilentAfterEnable)
                PlayHover();
            selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                PlayClick();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PlayClick();
        }

        void PlayHover()
        {
            if (playHoverSound && selectable.IsInteractable() && AudioManager.Instance != null)
                AudioManager.Instance.PlayUIHover();
        }

        void PlayClick()
        {
            if (playClickSound && selectable.IsInteractable() && AudioManager.Instance != null)
                AudioManager.Instance.PlayUIClick();
        }

        void Apply(float t)
        {
            if (glow != null)
                SetAlpha(glow, glowAlpha * t);
            if (border != null)
                SetAlpha(border, Mathf.Lerp(idleBorderAlpha, 1f, t));
            if (content != null)
                content.anchoredPosition = contentRest + new Vector2(slideDistance * t, 0f);
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }
}
