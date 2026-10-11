using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CodeEscape.Game;
using CodeEscape.SaveSystem;

namespace CodeEscape.UI
{
    /// <summary>
    /// Settings screen shared by the main menu and the pause menu (SettingsMenu prefab).
    /// Edits a copy of the current settings: Apply commits and saves them, Back discards
    /// anything not applied. Volume sliders preview live.
    /// </summary>
    public class SettingsMenuUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField, Tooltip("Shown while the settings screen is open. Keep this script's own GameObject active.")]
        GameObject overlay;

        [Header("Controls")]
        [SerializeField] Slider musicVolumeSlider;
        [SerializeField] TMP_Text musicVolumeValue;
        [SerializeField] Slider sfxVolumeSlider;
        [SerializeField] TMP_Text sfxVolumeValue;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] TMP_Text statusText;

        [Header("Reset Progress (main menu only)")]
        [SerializeField, Tooltip("Row with the Reset Progress button. Hidden outside the main menu.")]
        GameObject resetProgressRow;
        [SerializeField] TMP_Text resetProgressLabel;

        const string ResetLabel = "RESET PROGRESS";
        const string ConfirmResetLabel = "CLICK AGAIN TO CONFIRM";

        readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        SettingsData pending;
        bool resetArmed;

        public bool IsOpen => overlay != null && overlay.activeSelf;

        public event Action Opened;
        public event Action Closed;

        void Awake()
        {
            if (overlay != null)
                overlay.SetActive(false);
        }

        public void Open()
        {
            pending = SettingsManager.Instance != null ? SettingsManager.Instance.Current : SaveManager.LoadSettings();
            BuildResolutionOptions();
            ShowValues(pending);
            SetStatus(string.Empty);
            // Resetting mid-level would leave the player standing in a level they haven't reached.
            if (resetProgressRow != null)
                resetProgressRow.SetActive(SceneManager.GetActiveScene().name == SceneNames.MainMenu);
            DisarmReset();
            overlay.SetActive(true);
            UIFocus.Select(musicVolumeSlider);
            Opened?.Invoke();
        }

        /// <summary>Apply button.</summary>
        public void Apply()
        {
            if (pending == null)
                return;

            if (SettingsManager.Instance != null)
                SettingsManager.Instance.ApplySettings(pending);
            else
                SaveManager.SaveSettings(pending);
            SetStatus("Settings applied and saved.");
        }

        /// <summary>Back button: closes the screen and discards changes that were not applied.</summary>
        public void Back()
        {
            if (!IsOpen)
                return;

            if (SettingsManager.Instance != null)
                SettingsManager.Instance.CancelPreview();
            DisarmReset();
            overlay.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>Reset Progress button: the first click asks for confirmation, the second resets.</summary>
        public void OnResetProgressClicked()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                if (resetProgressLabel != null)
                    resetProgressLabel.text = ConfirmResetLabel;
                SetStatus("This deletes your story and level map progress (all languages). Settings are kept.");
                return;
            }

            DisarmReset();
            if (ProgressSaveService.Instance != null)
                ProgressSaveService.Instance.ResetProgress();
            else
            {
                SaveManager.DeleteProgress();
                SaveManager.DeleteAllLevelProgress();
            }
            SetStatus("Story and level map progress reset.");
        }

        void DisarmReset()
        {
            resetArmed = false;
            if (resetProgressLabel != null)
                resetProgressLabel.text = ResetLabel;
        }

        // ---------- Control callbacks (wired in the Inspector) ----------

        public void OnMusicVolumeChanged(float value)
        {
            if (pending == null)
                return;
            pending.musicVolume = value;
            UpdateVolumeLabels();
            PreviewVolumes();
        }

        public void OnSfxVolumeChanged(float value)
        {
            if (pending == null)
                return;
            pending.sfxVolume = value;
            UpdateVolumeLabels();
            PreviewVolumes();
        }

        public void OnFullscreenChanged(bool isOn)
        {
            if (pending != null)
                pending.fullscreen = isOn;
        }

        public void OnResolutionChanged(int index)
        {
            if (pending == null || index < 0 || index >= resolutions.Count)
                return;
            pending.resolutionWidth = resolutions[index].x;
            pending.resolutionHeight = resolutions[index].y;
        }

        // ---------- Helpers ----------

        void ShowValues(SettingsData settings)
        {
            musicVolumeSlider.SetValueWithoutNotify(settings.musicVolume);
            sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
            fullscreenToggle.SetIsOnWithoutNotify(settings.fullscreen);
            int resolutionIndex = FindResolutionIndex(settings);
            resolutionDropdown.SetValueWithoutNotify(resolutionIndex);
            resolutionDropdown.RefreshShownValue();
            // A saved size this display doesn't offer: Apply uses the size shown, not the hidden one.
            OnResolutionChanged(resolutionIndex);
            UpdateVolumeLabels();
        }

        void BuildResolutionOptions()
        {
            resolutions.Clear();
            resolutions.AddRange(SettingsManager.GetAvailableResolutions());

            var options = new List<string>(resolutions.Count);
            foreach (Vector2Int size in resolutions)
                options.Add($"{size.x} x {size.y}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
        }

        int FindResolutionIndex(SettingsData settings)
        {
            Vector2Int wanted = settings.HasResolution
                ? new Vector2Int(settings.resolutionWidth, settings.resolutionHeight)
                : new Vector2Int(Screen.width, Screen.height);
            int index = resolutions.IndexOf(wanted);
            if (index < 0)
                index = resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            return index >= 0 ? index : resolutions.Count - 1;
        }

        void UpdateVolumeLabels()
        {
            musicVolumeValue.text = ToPercent(pending.musicVolume);
            sfxVolumeValue.text = ToPercent(pending.sfxVolume);
        }

        void PreviewVolumes()
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.PreviewVolumes(pending.musicVolume, pending.sfxVolume);
        }

        void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        static string ToPercent(float value)
        {
            return $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
