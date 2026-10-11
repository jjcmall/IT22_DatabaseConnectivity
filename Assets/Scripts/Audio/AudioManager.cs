using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Clues;
using CodeEscape.Game;
using CodeEscape.Programming;

namespace CodeEscape.Audio
{
    public enum MusicTrack
    {
        None,
        Menu,
        Exploration,
        Mystery,
        Challenge,
        Ending
    }

    public enum SfxType
    {
        Correct,
        Incorrect,
        Hint,
        Clue,
        Door,
        Button,
        Cutscene,
        Alarm
    }

    /// <summary>
    /// Plays music, sound effects and UI sounds on two AudioSources and keeps their volumes in sync
    /// with the Music / SFX settings. Each scene has a music track (menu, exploration, mystery,
    /// ending); challenges switch to challenge music, cutscenes may pick their own. Gameplay sounds
    /// (answers, hints, clues, doors) are played from the game's events. Lives on GameSystems.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // Stops a burst of hover sounds when the mouse sweeps across several buttons at once.
        const float MinHoverInterval = 0.05f;

        public static AudioManager Instance { get; private set; }

        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource sfxSource;

        [Header("UI Sounds")]
        [SerializeField] AudioClip uiHoverClip;
        [SerializeField] AudioClip uiClickClip;
        [SerializeField, Range(0f, 1f)] float uiVolume = 0.6f;

        [Header("Music")]
        [SerializeField] AudioClip menuMusic;
        [SerializeField] AudioClip explorationMusic;
        [SerializeField] AudioClip mysteryMusic;
        [SerializeField] AudioClip challengeMusic;
        [SerializeField] AudioClip endingMusic;
        [SerializeField, Min(0f), Tooltip("Seconds for a full cross-fade between tracks.")]
        float musicFadeTime = 1f;

        [Header("Sound Effects")]
        [SerializeField] AudioClip correctClip;
        [SerializeField] AudioClip incorrectClip;
        [SerializeField] AudioClip hintClip;
        [SerializeField] AudioClip clueClip;
        [SerializeField] AudioClip doorClip;
        [SerializeField] AudioClip cutsceneClip;
        [SerializeField, Tooltip("Played when a hazard catches Alex.")]
        AudioClip alarmClip;
        [SerializeField, Range(0f, 1f)] float effectsVolume = 0.8f;

        float lastHoverTime = -1f;
        float musicVolume = 1f;
        float musicFade = 1f;
        Coroutine musicRoutine;

        public MusicTrack CurrentTrack { get; private set; }
        public AudioClip CurrentMusicClip => musicSource != null ? musicSource.clip : null;
        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;
        public AudioClip LastSfx { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            SettingsManager.VolumeChanged += ApplyVolumes;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ProgrammingChallengeManager.ChallengeOpened += HandleChallengeOpened;
            ProgrammingChallengeManager.ChallengeClosed += HandleChallengeClosed;
            ProgrammingChallengeManager.ChallengeCompleted += HandleChallengeCompleted;
            ProgrammingChallengeManager.AnswerChecked += HandleAnswerChecked;
            ProgrammingChallengeManager.HintShown += HandleHintShown;
            ProgrammingChallengeManager.HazardCaught += HandleHazardCaught;
            ClueManager.ClueCollected += HandleClueCollected;
        }

        void OnDisable()
        {
            SettingsManager.VolumeChanged -= ApplyVolumes;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ProgrammingChallengeManager.ChallengeOpened -= HandleChallengeOpened;
            ProgrammingChallengeManager.ChallengeClosed -= HandleChallengeClosed;
            ProgrammingChallengeManager.ChallengeCompleted -= HandleChallengeCompleted;
            ProgrammingChallengeManager.AnswerChecked -= HandleAnswerChecked;
            ProgrammingChallengeManager.HintShown -= HandleHintShown;
            ProgrammingChallengeManager.HazardCaught -= HandleHazardCaught;
            ClueManager.ClueCollected -= HandleClueCollected;
        }

        void Start()
        {
            if (SettingsManager.Instance != null)
                ApplyVolumes(SettingsManager.Instance.MusicVolume, SettingsManager.Instance.SfxVolume);
            PlaySceneMusic();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---------- Music ----------

        /// <summary>Cross-fades to a track. Works while the game is paused (unscaled time).</summary>
        public void PlayMusic(MusicTrack track)
        {
            AudioClip clip = ClipFor(track);
            if (track == CurrentTrack && (clip == null || (musicSource != null && musicSource.isPlaying && musicSource.clip == clip)))
                return;
            CurrentTrack = track;
            if (musicSource == null || !isActiveAndEnabled)
                return;
            if (musicRoutine != null)
                StopCoroutine(musicRoutine);
            musicRoutine = StartCoroutine(CrossFade(clip));
        }

        /// <summary>The current scene's normal track (used after challenges and cutscenes).</summary>
        public void PlaySceneMusic()
        {
            PlayMusic(TrackForScene(SceneManager.GetActiveScene().name));
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (musicSource == null || clip == null)
                return;
            if (musicSource.clip == clip && musicSource.isPlaying)
                return;

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();
        }

        public void StopMusic()
        {
            PlayMusic(MusicTrack.None);
        }

        public static MusicTrack TrackForScene(string sceneName)
        {
            switch (sceneName)
            {
                case SceneNames.MainMenu:
                case SceneNames.LanguageSelect:
                case SceneNames.LevelMap:
                    return MusicTrack.Menu;
                case SceneNames.Intro:
                case SceneNames.Warehouse:
                case SceneNames.Facility:
                case SceneNames.Station:
                case SceneNames.FinalArea:
                    return MusicTrack.Mystery;
                case SceneNames.Home:
                case SceneNames.Neighborhood:
                    return MusicTrack.Exploration;
                case SceneNames.Ending:
                case SceneNames.Results:
                    return MusicTrack.Ending;
                default:
                    return MusicTrack.None;
            }
        }

        IEnumerator CrossFade(AudioClip clip)
        {
            float half = musicFadeTime * 0.5f;
            if (musicSource.isPlaying && musicSource.clip != clip)
                yield return FadeMusic(0f, half);

            if (clip == null)
            {
                musicSource.Stop();
                musicRoutine = null;
                yield break;
            }
            if (musicSource.clip != clip || !musicSource.isPlaying)
            {
                musicSource.clip = clip;
                musicSource.loop = true;
                musicFade = 0f;
                UpdateMusicVolume();
                musicSource.Play();
            }
            yield return FadeMusic(1f, half);
            musicRoutine = null;
        }

        IEnumerator FadeMusic(float target, float duration)
        {
            float start = musicFade;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicFade = Mathf.Lerp(start, target, elapsed / duration);
                UpdateMusicVolume();
                yield return null;
            }
            musicFade = target;
            UpdateMusicVolume();
        }

        AudioClip ClipFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Menu: return menuMusic;
                case MusicTrack.Exploration: return explorationMusic;
                case MusicTrack.Mystery: return mysteryMusic;
                case MusicTrack.Challenge: return challengeMusic;
                case MusicTrack.Ending: return endingMusic;
                default: return null;
            }
        }

        // ---------- Sound effects ----------

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (sfxSource != null && clip != null)
            {
                sfxSource.PlayOneShot(clip, volumeScale);
                LastSfx = clip;
            }
        }

        public void PlaySfx(SfxType type)
        {
            if (type == SfxType.Button)
                PlayUIClick();
            else
                PlaySfx(ClipFor(type), effectsVolume);
        }

        /// <summary>Button hover sound (follows the SFX volume).</summary>
        public void PlayUIHover()
        {
            if (Time.unscaledTime - lastHoverTime < MinHoverInterval)
                return;
            lastHoverTime = Time.unscaledTime;
            PlaySfx(uiHoverClip, uiVolume);
        }

        /// <summary>Button click sound (follows the SFX volume).</summary>
        public void PlayUIClick()
        {
            PlaySfx(uiClickClip, uiVolume);
        }

        AudioClip ClipFor(SfxType type)
        {
            switch (type)
            {
                case SfxType.Correct: return correctClip;
                case SfxType.Incorrect: return incorrectClip;
                case SfxType.Hint: return hintClip;
                case SfxType.Clue: return clueClip;
                case SfxType.Door: return doorClip;
                case SfxType.Button: return uiClickClip;
                case SfxType.Cutscene: return cutsceneClip;
                case SfxType.Alarm: return alarmClip;
                default: return null;
            }
        }

        // ---------- Volume ----------

        void ApplyVolumes(float music, float sfx)
        {
            musicVolume = music;
            UpdateMusicVolume();
            if (sfxSource != null)
                sfxSource.volume = sfx;
        }

        void UpdateMusicVolume()
        {
            if (musicSource != null)
                musicSource.volume = musicVolume * musicFade;
        }

        // ---------- Game events ----------

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                PlayMusic(TrackForScene(scene.name));
        }

        void HandleChallengeOpened(QuestionData question) => PlayMusic(MusicTrack.Challenge);
        void HandleChallengeClosed() => PlaySceneMusic();
        void HandleChallengeCompleted(string obstacleId, QuestionData question) => PlaySfx(SfxType.Door);
        void HandleAnswerChecked(bool correct) => PlaySfx(correct ? SfxType.Correct : SfxType.Incorrect);
        void HandleHintShown() => PlaySfx(SfxType.Hint);
        void HandleHazardCaught(string hazardId) => PlaySfx(SfxType.Alarm);
        void HandleClueCollected(ClueData clue) => PlaySfx(SfxType.Clue);
    }
}
