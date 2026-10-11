using System.Collections;
using UnityEngine;

namespace CodeEscape.Cutscenes
{
    /// <summary>
    /// Full-screen black overlay for cutscene fades (scene loads keep their own fade in
    /// SceneTransitionManager). Fades are IEnumerators on unscaled time.
    /// </summary>
    public class FadeManager : MonoBehaviour
    {
        const float MaxStep = 1f / 20f;

        public static FadeManager Instance { get; private set; }

        [SerializeField] CanvasGroup overlay;

        public float Alpha => overlay != null ? overlay.alpha : 0f;

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
            SetAlpha(0f);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetAlpha(float alpha)
        {
            if (overlay == null)
                return;
            overlay.alpha = alpha;
            overlay.blocksRaycasts = alpha > 0.01f;
        }

        /// <summary>Fades to the given alpha (1 = black). A duration of 0 sets it immediately.</summary>
        public IEnumerator FadeTo(float alpha, float duration)
        {
            if (overlay == null)
                yield break;
            float start = overlay.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                SetAlpha(Mathf.Lerp(start, alpha, elapsed / duration));
                yield return null;
            }
            SetAlpha(alpha);
        }
    }
}
