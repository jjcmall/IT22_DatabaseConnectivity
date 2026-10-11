using System;
using UnityEngine;

namespace CodeEscape.SaveSystem
{
    /// <summary>
    /// Player-adjustable settings, persisted by <see cref="SaveManager"/>.
    /// A resolution of 0 x 0 means "keep whatever resolution the game is already using".
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        public const float DefaultVolume = 0.8f;

        [Range(0f, 1f)] public float musicVolume = DefaultVolume;
        [Range(0f, 1f)] public float sfxVolume = DefaultVolume;
        public bool fullscreen = true;
        public int resolutionWidth;
        public int resolutionHeight;

        public bool HasResolution => resolutionWidth > 0 && resolutionHeight > 0;

        public static SettingsData CreateDefault()
        {
            return new SettingsData { fullscreen = Screen.fullScreen };
        }

        public SettingsData Clone()
        {
            return (SettingsData)MemberwiseClone();
        }

        /// <summary>Clamps values into their valid ranges.</summary>
        public void Sanitize()
        {
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            if (!HasResolution)
            {
                resolutionWidth = 0;
                resolutionHeight = 0;
            }
        }
    }
}
