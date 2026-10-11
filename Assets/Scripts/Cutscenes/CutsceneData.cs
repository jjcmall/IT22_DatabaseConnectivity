using System;
using System.Collections.Generic;
using UnityEngine;
using CodeEscape.Audio;
using CodeEscape.Dialogue;

namespace CodeEscape.Cutscenes
{
    public enum CutsceneStepType
    {
        Dialogue,
        FadeOut,
        FadeIn,
        CameraPan,
        CameraZoom,
        CameraFocus,
        CameraReset,
        PlayMusic,
        StopMusic,
        PlaySfx,
        Wait,
        ShowImage,
        HideImage,
        ShowBackground,
        HideBackground,
        ShowTitle,
        HideTitle,
        CompleteStoryEvent,
        LoadScene
    }

    /// <summary>
    /// One cutscene step. Only the fields the step type uses matter:
    /// Dialogue: dialogue. Fades / Wait / camera moves: duration. CameraPan: position (+ zoom).
    /// CameraZoom: zoom. CameraFocus: target object name (+ zoom). PlayMusic: music. PlaySfx: sfx.
    /// ShowImage: image + text caption. ShowBackground: image. ShowTitle: text.
    /// ShowImage / ShowTitle: waitForInput, otherwise duration. CompleteStoryEvent: target event.
    /// LoadScene: target scene.
    /// </summary>
    [Serializable]
    public class CutsceneStep
    {
        public CutsceneStepType type;
        public DialogueData dialogue;
        [Min(0f)] public float duration = 1f;
        public Vector2 position;
        [Tooltip("Orthographic size. 0 = keep the current size.")]
        [Min(0f)] public float zoom;
        public string target;
        public Sprite image;
        [TextArea(1, 5)] public string text;
        public MusicTrack music;
        public SfxType sfx;
        public bool waitForInput;
    }

    /// <summary>A cutscene: an ordered list of steps run by the CutsceneManager.</summary>
    [CreateAssetMenu(fileName = "Cutscene", menuName = "Code Escape/Cutscene Data")]
    public class CutsceneData : ScriptableObject
    {
        [SerializeField, Tooltip("Used by StoryManager steps and CutsceneTriggers.")]
        string id;
        [SerializeField] string displayName;
        [SerializeField] bool skippable = true;
        [SerializeField] List<CutsceneStep> steps = new List<CutsceneStep>();

        public string Id => id;
        public string DisplayName => displayName;
        public bool Skippable => skippable;
        public IReadOnlyList<CutsceneStep> Steps => steps;
    }
}
