using TMPro;
using UnityEngine;
using CodeEscape.Programming;

namespace CodeEscape.UI
{
    /// <summary>Displays the challenge score and hints used, updating whenever they change.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class ScoreLabel : MonoBehaviour
    {
        [SerializeField, Tooltip("{0} = score, {1} = hints used.")]
        string format = "Score: {0}   Hints used: {1}";

        TMP_Text label;

        void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            ProgrammingChallengeManager.ScoreChanged += Refresh;
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            Refresh(manager != null ? manager.Score : 0, manager != null ? manager.HintsUsed : 0);
        }

        void OnDisable()
        {
            ProgrammingChallengeManager.ScoreChanged -= Refresh;
        }

        void Refresh(int score, int hintsUsed)
        {
            label.text = string.Format(format, score, hintsUsed);
        }
    }
}
