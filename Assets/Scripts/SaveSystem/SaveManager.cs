using UnityEngine;
using CodeEscape.Programming;

namespace CodeEscape.SaveSystem
{
    /// <summary>
    /// Saves the selected language, the settings and the story progress using PlayerPrefs.
    /// Progress (level, story stage, clues, challenges, score, hints, time) is one JSON value.
    /// Level map progress (cleared levels per language) is one JSON value per language.
    /// </summary>
    public static class SaveManager
    {
        public const int SaveVersion = 2;

        const string KeyPrefix = "CodeEscape.";
        const string KeySaveVersion = KeyPrefix + "SaveVersion";
        const string KeyLanguage = KeyPrefix + "SelectedLanguage";
        const string KeyMusicVolume = KeyPrefix + "MusicVolume";
        const string KeySfxVolume = KeyPrefix + "SfxVolume";
        const string KeyFullscreen = KeyPrefix + "Fullscreen";
        const string KeyResolutionWidth = KeyPrefix + "ResolutionWidth";
        const string KeyResolutionHeight = KeyPrefix + "ResolutionHeight";
        const string KeyProgress = KeyPrefix + "Progress";
        // Followed by the language ID, e.g. "CodeEscape.LevelProgress.python".
        const string KeyLevelProgressPrefix = KeyPrefix + "LevelProgress.";

        static readonly ProgrammingLanguage[] Languages = { ProgrammingLanguage.Python, ProgrammingLanguage.PHP, ProgrammingLanguage.SQL };

        static readonly string[] AllKeys =
        {
            KeySaveVersion, KeyLanguage, KeyMusicVolume, KeySfxVolume,
            KeyFullscreen, KeyResolutionWidth, KeyResolutionHeight, KeyProgress
        };

        // ---------- Language ----------

        public static void SaveLanguage(ProgrammingLanguage language)
        {
            PlayerPrefs.SetInt(KeyLanguage, (int)language);
            Commit();
        }

        /// <summary>Returns the saved language, or <see cref="ProgrammingLanguage.None"/> if nothing valid is saved.</summary>
        public static ProgrammingLanguage LoadLanguage()
        {
            var language = (ProgrammingLanguage)PlayerPrefs.GetInt(KeyLanguage, (int)ProgrammingLanguage.None);
            return language.IsValid() ? language : ProgrammingLanguage.None;
        }

        // ---------- Settings ----------

        public static bool HasSavedSettings => PlayerPrefs.HasKey(KeyMusicVolume);

        public static void SaveSettings(SettingsData settings)
        {
            PlayerPrefs.SetFloat(KeyMusicVolume, settings.musicVolume);
            PlayerPrefs.SetFloat(KeySfxVolume, settings.sfxVolume);
            PlayerPrefs.SetInt(KeyFullscreen, settings.fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyResolutionWidth, settings.resolutionWidth);
            PlayerPrefs.SetInt(KeyResolutionHeight, settings.resolutionHeight);
            Commit();
        }

        /// <summary>Returns the saved settings, or defaults if none have been saved yet.</summary>
        public static SettingsData LoadSettings()
        {
            SettingsData settings = SettingsData.CreateDefault();
            if (HasSavedSettings)
            {
                settings.musicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, settings.musicVolume);
                settings.sfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, settings.sfxVolume);
                settings.fullscreen = PlayerPrefs.GetInt(KeyFullscreen, settings.fullscreen ? 1 : 0) == 1;
                settings.resolutionWidth = PlayerPrefs.GetInt(KeyResolutionWidth, 0);
                settings.resolutionHeight = PlayerPrefs.GetInt(KeyResolutionHeight, 0);
            }
            settings.Sanitize();
            return settings;
        }

        // ---------- Story progress ----------

        public static bool HasProgress => PlayerPrefs.HasKey(KeyProgress);

        public static void SaveProgress(ProgressData progress)
        {
            if (progress == null)
                return;
            PlayerPrefs.SetString(KeyProgress, JsonUtility.ToJson(progress));
            Commit();
        }

        /// <summary>Returns the saved progress, or null if there is none (or it can't be read).</summary>
        public static ProgressData LoadProgress()
        {
            if (!HasProgress)
                return null;
            try
            {
                return JsonUtility.FromJson<ProgressData>(PlayerPrefs.GetString(KeyProgress));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveManager] Saved progress could not be read and was ignored: {e.Message}");
                return null;
            }
        }

        public static void DeleteProgress()
        {
            PlayerPrefs.DeleteKey(KeyProgress);
            PlayerPrefs.Save();
        }

        // ---------- Level map progress (per language) ----------

        public static bool HasLevelProgress(ProgrammingLanguage language)
        {
            return language.IsValid() && PlayerPrefs.HasKey(LevelProgressKey(language));
        }

        public static void SaveLevelProgress(ProgrammingLanguage language, LevelProgressData data)
        {
            if (!language.IsValid() || data == null)
                return;
            data.language = language.ToId();
            PlayerPrefs.SetString(LevelProgressKey(language), JsonUtility.ToJson(data));
            Commit();
        }

        /// <summary>Returns the language's level map progress, or null if there is none (or it can't be read).</summary>
        public static LevelProgressData LoadLevelProgress(ProgrammingLanguage language)
        {
            if (!HasLevelProgress(language))
                return null;
            try
            {
                return JsonUtility.FromJson<LevelProgressData>(PlayerPrefs.GetString(LevelProgressKey(language)));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveManager] Saved {language.ToDisplayName()} level progress could not be read and was ignored: {e.Message}");
                return null;
            }
        }

        public static void DeleteLevelProgress(ProgrammingLanguage language)
        {
            if (!language.IsValid())
                return;
            PlayerPrefs.DeleteKey(LevelProgressKey(language));
            PlayerPrefs.Save();
        }

        public static void DeleteAllLevelProgress()
        {
            foreach (ProgrammingLanguage language in Languages)
                PlayerPrefs.DeleteKey(LevelProgressKey(language));
            PlayerPrefs.Save();
        }

        static string LevelProgressKey(ProgrammingLanguage language)
        {
            return KeyLevelProgressPrefix + language.ToId();
        }

        // ---------- Maintenance ----------

        /// <summary>Deletes every key this game has written (handy for testing a fresh install).</summary>
        public static void DeleteAllSaveData()
        {
            foreach (string key in AllKeys)
                PlayerPrefs.DeleteKey(key);
            DeleteAllLevelProgress();
        }

        static void Commit()
        {
            PlayerPrefs.SetInt(KeySaveVersion, SaveVersion);
            PlayerPrefs.Save();
        }
    }
}
