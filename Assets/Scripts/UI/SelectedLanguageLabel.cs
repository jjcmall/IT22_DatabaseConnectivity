using TMPro;
using UnityEngine;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>Displays the currently selected programming language and updates when it changes.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class SelectedLanguageLabel : MonoBehaviour
    {
        [SerializeField, Tooltip("{0} is replaced with the language name.")]
        string format = "Language: {0}";

        TMP_Text label;

        void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            LanguageManager.LanguageChanged += Refresh;
            Refresh(LanguageManager.Current);
        }

        void OnDisable()
        {
            LanguageManager.LanguageChanged -= Refresh;
        }

        void Refresh(ProgrammingLanguage language)
        {
            label.text = string.Format(format, language.ToDisplayName());
        }
    }
}
