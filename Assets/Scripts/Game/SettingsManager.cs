using System;
using System.Collections.Generic;
using UnityEngine;
using CodeEscape.SaveSystem;

namespace CodeEscape.Game
{
    /// <summary>
    /// Holds the applied settings, applies display settings, broadcasts volume changes and saves
    /// through SaveManager. The settings screen (SettingsMenuUI) edits a copy and calls ApplySettings.
    /// Lives on the persistent GameSystems object.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        /// <summary>Raised with (music, sfx) when volumes are applied or previewed.</summary>
        public static event Action<float, float> VolumeChanged;

        SettingsData applied;

        /// <summary>A copy of the currently applied settings.</summary>
        public SettingsData Current => applied.Clone();
        public float MusicVolume => applied.musicVolume;
        public float SfxVolume => applied.sfxVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            VolumeChanged = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            applied = SaveManager.LoadSettings();
        }

        void Start()
        {
            ApplyDisplay(applied);
            VolumeChanged?.Invoke(applied.musicVolume, applied.sfxVolume);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Applies the given settings and saves them.</summary>
        public void ApplySettings(SettingsData settings)
        {
            applied = settings.Clone();
            applied.Sanitize();
            ApplyDisplay(applied);
            VolumeChanged?.Invoke(applied.musicVolume, applied.sfxVolume);
            SaveManager.SaveSettings(applied);

            string resolution = applied.HasResolution ? $"{applied.resolutionWidth}x{applied.resolutionHeight}" : "unchanged";
            Debug.Log($"[SettingsManager] Applied and saved - music {applied.musicVolume:0.00}, sfx {applied.sfxVolume:0.00}, fullscreen {applied.fullscreen}, resolution {resolution}");
        }

        /// <summary>Temporarily changes volumes without applying or saving them.</summary>
        public void PreviewVolumes(float music, float sfx)
        {
            VolumeChanged?.Invoke(Mathf.Clamp01(music), Mathf.Clamp01(sfx));
        }

        /// <summary>Restores the applied volumes after a preview.</summary>
        public void CancelPreview()
        {
            VolumeChanged?.Invoke(applied.musicVolume, applied.sfxVolume);
        }

        /// <summary>Distinct supported resolutions (smallest first), always including the current window size.</summary>
        public static List<Vector2Int> GetAvailableResolutions()
        {
            var sizes = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!sizes.Contains(size))
                    sizes.Add(size);
            }

            var current = new Vector2Int(Screen.width, Screen.height);
            if (!sizes.Contains(current))
                sizes.Add(current);

            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return sizes;
        }

        static void ApplyDisplay(SettingsData settings)
        {
            FullScreenMode mode = settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (settings.HasResolution)
                Screen.SetResolution(settings.resolutionWidth, settings.resolutionHeight, mode);
            else
                Screen.fullScreenMode = mode;
        }
    }
}
