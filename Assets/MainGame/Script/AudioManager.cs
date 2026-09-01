using System.Collections;
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    private const string ManagerName = "AudioManager";
    private const string ResourcesAudioDataPath = "Audio/AudioData";

    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioData audioData;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField, Min(0f)] private float playerVoiceVolumeMultiplier = 2f;

    private int lastGroundFootstepIndex = -1;
    private int lastWoodFootstepIndex = -1;
    private float backgroundDuckMultiplier = 1f;
    private bool maVuDaiPatrolPlayed;
    private Coroutine gameplayAmbienceRoutine;
    private bool gameplayAmbienceSequenceActive;
    private bool gameplayAmbienceMomentRequested;
    private bool gameplayAmbienceClipPlaying;
    private bool gameplayAmbienceSuppressed;
    private bool gameplayAmbienceDisabledByPiano;
    private bool gameplayAmbiencePlayedOnce;
    private bool stairEncounterThreatPlayed;
    private bool sanityWarningAllowedAfterPianoDoor;
    private bool voiceSourcePlayingMonsterVoice;
    private float voiceSourceVolumeMultiplier = 1f;
    private float gameplayAmbienceBlockedUntil;
    private Coroutine dialoguePauseRoutine;
    private Coroutine stairEncounterThreatRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static AudioManager EnsureInstance(AudioData data = null)
    {
        if (Instance != null)
        {
            if (data != null)
                Instance.SetAudioData(data);
            return Instance;
        }

        var existing = FindFirstObjectByType<AudioManager>();
        if (existing != null)
        {
            existing.InitializeAsSingleton(data);
            return existing;
        }

        var gameObject = new GameObject(ManagerName);
        var manager = gameObject.AddComponent<AudioManager>();
        manager.InitializeAsSingleton(data);
        return manager;
    }

    private void Awake()
    {
        InitializeAsSingleton(audioData);
    }

    private void OnEnable()
    {
        GameData.SettingsChanged += ApplySettings;
    }

    private void OnDisable()
    {
        GameData.SettingsChanged -= ApplySettings;
    }

    public void SetAudioData(AudioData data)
    {
        audioData = data;
        PlayMenuMusic();
    }

    public void ApplySettings(GameSettings settings)
    {
        AudioListener.volume = settings.MasterVolume / 100f;

        if (musicSource != null)
            musicSource.volume = settings.MusicVolume / 100f * backgroundDuckMultiplier;
        if (ambienceSource != null)
            ambienceSource.volume = settings.MusicVolume / 100f * backgroundDuckMultiplier;
        if (sfxSource != null)
            sfxSource.volume = settings.SfxVolume / 100f;
        if (uiSource != null)
            uiSource.volume = settings.SfxVolume / 100f;
        if (voiceSource != null)
            voiceSource.volume = GetVoiceSourceVolume(settings.SfxVolume / 100f, voiceSourceVolumeMultiplier);
    }

    public void PlayMenuMusic()
    {
        PlayLoop(musicSource, audioData != null ? audioData.menuMusic : null);
    }

    public void PlayGameplayAmbience()
    {
        if (gameplayAmbiencePlayedOnce
            || gameplayAmbienceSuppressed
            || gameplayAmbienceDisabledByPiano
            || FpsHorrorKit.PhysicalPianoController.IsAnyActive)
        {
            StopGameplayAmbienceSequence();
            StopAmbienceSource();
            return;
        }

        var clips = audioData != null ? audioData.gameplayAmbiences : null;
        if (HasPlayableClip(clips))
        {
            if (gameplayAmbienceSequenceActive)
                return;

            StopGameplayAmbienceSequence();
            StopAmbienceSource();
            gameplayAmbienceSequenceActive = true;
            gameplayAmbiencePlayedOnce = true;
            gameplayAmbienceRoutine = StartCoroutine(PlayGameplayAmbienceSequence(clips, audioData.gameplayAmbienceSilenceSeconds));
            return;
        }

        StopGameplayAmbienceSequence();
        PlayGameplayAmbienceClipOnce(audioData != null ? audioData.gameplayAmbience : null);
    }

    public void PlayIntroAmbience()
    {
        maVuDaiPatrolPlayed = false;
        gameplayAmbienceDisabledByPiano = false;
        gameplayAmbiencePlayedOnce = false;
        stairEncounterThreatPlayed = false;
        sanityWarningAllowedAfterPianoDoor = false;
        gameplayAmbienceBlockedUntil = 0f;
        StopMusic();
        StopGameplayAmbienceSequence();
        PlayLoop(ambienceSource, audioData != null ? audioData.introAmbience : null);
    }

    public void PlayGhostAmbience()
    {
        StopGameplayAmbienceSequence();
        PlayLoop(ambienceSource, audioData != null ? audioData.ghostAmbience : null);
    }

    public void PlayChaseMusic()
    {
        StopGameplayAmbienceSequence();
        PlayLoop(musicSource, audioData != null ? audioData.chaseMusic : null);
    }

    public void PlayDeathMusic()
    {
        StopGameplayAmbienceSequence();
        PlayLoop(musicSource, audioData != null ? audioData.deathMusic : null);
    }

    public void StopMonsterThreatAudio()
    {
        StopVoice();

        if (musicSource != null && audioData != null && musicSource.clip == audioData.chaseMusic)
            StopLoop(musicSource);

        if (ambienceSource != null && audioData != null && ambienceSource.clip == audioData.ghostAmbience)
            StopLoop(ambienceSource);
    }

    public void StopMusic()
    {
        StopLoop(musicSource);
    }

    public void SetBackgroundDuck(float multiplier)
    {
        backgroundDuckMultiplier = Mathf.Clamp01(multiplier);
        if (backgroundDuckMultiplier < 0.99f)
            BlockGameplayAmbience(1f);
        if (GameData.Instance != null)
            ApplySettings(GameData.Instance.Settings);
    }

    public void ClearBackgroundDuck()
    {
        backgroundDuckMultiplier = 1f;
        if (GameData.Instance != null)
            ApplySettings(GameData.Instance.Settings);
    }

    public void SetGameplayAmbienceSuppressed(bool suppressed)
    {
        gameplayAmbienceSuppressed = suppressed;
        if (!gameplayAmbienceSuppressed)
            return;

        gameplayAmbienceMomentRequested = false;
        BlockGameplayAmbience(1f);
        StopGameplayAmbienceSequence();
        StopAmbienceSource();
    }

    public void DisableGameplayAmbienceAfterPiano()
    {
        gameplayAmbienceDisabledByPiano = true;
        gameplayAmbienceMomentRequested = false;
        BlockGameplayAmbience(1f);
        StopGameplayAmbienceSequence();
        StopAmbienceSource();
    }

    public void PlayButtonHover()
    {
        PlayUi(audioData != null ? audioData.buttonHover : null);
    }

    public void PlayButtonClick()
    {
        PlayUi(audioData != null ? audioData.buttonClick : null);
    }

    public void PlayApplySettings()
    {
        PlayUi(audioData != null ? audioData.applySettings : null);
    }

    public void PlayBack()
    {
        PlayUi(audioData != null ? audioData.back : null);
    }

    public void PlayFlashlightToggle()
    {
        PlaySfx(audioData != null ? audioData.flashlightToggle : null);
    }

    public void PlayFlashlightBatteryUse()
    {
        AudioClip clip = null;
        if (audioData != null)
            clip = audioData.flashlightBatteryUse != null ? audioData.flashlightBatteryUse : audioData.flashlightToggle;

        PlaySfx(clip);
    }

    public void PlayCameraShot()
    {
        PlaySfx(audioData != null ? audioData.cameraShot : null);
    }

    public void PlayKeyPickup()
    {
        PlaySfx(audioData != null ? audioData.keyPickup : null);
    }

    public void PlayNotePickup()
    {
        PlaySfx(audioData != null ? audioData.notePickup : null);
    }

    public void PlayPaperPickup()
    {
        PlaySfx(audioData != null ? audioData.paperPickup : null);
    }

    public void PlayDiaryPageFlip()
    {
        AudioClip clip = null;
        if (audioData != null)
            clip = audioData.diaryPageFlip != null ? audioData.diaryPageFlip : audioData.paperPickup;

        PlaySfx(clip);
    }

    public void PlayGenericInteract()
    {
        PlaySfx(audioData != null ? audioData.genericInteract : null);
    }

    public void PlayDoorLocked()
    {
        PlaySfx(audioData != null ? audioData.doorLocked : null);
    }

    public void PlayDoorOpenSlow()
    {
        PlaySfx(audioData != null ? audioData.doorOpenSlow : null);
    }

    public void PlayDoorUnlock()
    {
        PlaySfx(audioData != null ? audioData.doorUnlock : null);
    }

    public void PlayItemUnlock()
    {
        PlaySfx(audioData != null ? audioData.itemUnlock : null);
    }

    public void PlayMusicBoxStartup()
    {
        PlayBlockingSfx(audioData != null ? audioData.musicBoxStartup : null);
    }

    public void PlayPianoWrong()
    {
        if (!CanPlaySanityWarningAudio())
            return;

        PlayBlockingSfx(audioData != null ? audioData.pianoWrong : null);
    }

    public void PlaySanityWarning()
    {
        if (!CanPlaySanityWarningAudio())
            return;

        PlayBlockingSfx(audioData != null ? audioData.sanityWarning : null);
    }

    public bool CanPlaySanityWarningAudio()
    {
        return sanityWarningAllowedAfterPianoDoor;
    }

    public void AllowSanityWarningAfterPianoDoorOpened(bool playOpeningWarning = false)
    {
        sanityWarningAllowedAfterPianoDoor = true;
        if (playOpeningWarning)
            PlayBlockingSfx(audioData != null ? audioData.pianoWrong : null);
    }

    public float PlayGhostJumpscare(float volume = 1f)
    {
        return PlayBlockingSfx(audioData != null ? audioData.ghostJumpscare : null, volume);
    }

    public void PauseDialogueForMonsterRoar(float seconds, System.Func<bool> shouldResume = null)
    {
        if (dialoguePauseRoutine != null)
            StopCoroutine(dialoguePauseRoutine);

        dialoguePauseRoutine = StartCoroutine(PauseVoiceForMonsterRoarRoutine(Mathf.Max(0f, seconds), shouldResume, false));
    }

    public void PauseMonsterVoiceForRoar(float seconds, System.Func<bool> shouldResume = null)
    {
        if (dialoguePauseRoutine != null)
            StopCoroutine(dialoguePauseRoutine);

        dialoguePauseRoutine = StartCoroutine(PauseVoiceForMonsterRoarRoutine(Mathf.Max(0f, seconds), shouldResume, true));
    }

    public float PlayWellJumpscare()
    {
        return PlayBlockingSfx(audioData != null ? audioData.wellJumpscare : null);
    }

    public void PlayGroundFootstep(float volume = 0.1f)
    {
        PlayFootstep(audioData != null ? audioData.footstepGround : null, ref lastGroundFootstepIndex, volume);
    }

    public void PlayWoodFootstep(float volume = 0.5f)
    {
        PlayFootstep(audioData != null ? audioData.footstepWood : null, ref lastWoodFootstepIndex, volume);
    }

    public void PlayItemPickup(FpsHorrorKit.ItemData item)
    {
        if (item == null)
        {
            PlayGenericInteract();
            return;
        }

        switch (item.itemType)
        {
            case FpsHorrorKit.ItemType.Key:
                PlayKeyPickup();
                break;
            case FpsHorrorKit.ItemType.MusicSheet:
                PlayNotePickup();
                break;
            case FpsHorrorKit.ItemType.QuestItem:
                PlayPaperPickup();
                break;
            default:
                PlayGenericInteract();
                break;
        }
    }

    public float PlayBaLanTape()
    {
        return PlayVoice(audioData != null ? audioData.baLanTapeFull : null);
    }

    public AudioClip GetIntroVoiceClip(string dialogueId)
    {
        return dialogueId switch
        {
            "intro_arrived_line" => audioData != null ? audioData.intro01 : null,
            "intro_villa_history" => audioData != null ? audioData.intro02 : null,
            "intro_thesis_line" => audioData != null ? audioData.intro03 : null,
            "intro_front_sketch_line" => audioData != null ? audioData.intro04 : null,
            "intro_enter_window_line" => audioData != null ? audioData.intro05 : null,
            _ => null
        };
    }

    public float PlayMaVuDaiPatrol(float volume = -1f)
    {
        if (maVuDaiPatrolPlayed)
            return 0f;

        maVuDaiPatrolPlayed = true;
        float effectiveVolume = volume >= 0f ? volume : CalculateMonsterVoiceDistanceVolume(1f);
        return PlayMonsterVoiceOverlay(audioData != null ? audioData.maVuDaiPatrolFull : null, effectiveVolume);
    }

    public bool HasMaVuDaiPatrolPlayed => maVuDaiPatrolPlayed;

    public void MarkMaVuDaiPatrolPlayed()
    {
        maVuDaiPatrolPlayed = true;
    }

    public void ResetMaVuDaiPatrolPlayback()
    {
        maVuDaiPatrolPlayed = false;
    }

    public float PlayDiaryReaction(int index)
    {
        return PlayPlayerVoice(index switch
        {
            1 => audioData != null ? audioData.diaryReaction01 : null,
            2 => audioData != null ? audioData.diaryReaction02 : null,
            3 => audioData != null ? audioData.diaryReaction03 : null,
            _ => null
        });
    }

    public float PlayHideVoice(int index)
    {
        return PlayPlayerVoice(index switch
        {
            1 => audioData != null ? audioData.mkHide01 : null,
            2 => audioData != null ? audioData.mkHide02 : null,
            3 => audioData != null ? audioData.mkHide03 : null,
            4 => audioData != null ? audioData.mkHide04 : null,
            5 => audioData != null ? audioData.mkHide05 : null,
            6 => audioData != null ? audioData.mkHide06 : null,
            _ => null
        });
    }

    public float PlayDeathVoice(int index)
    {
        return PlayPlayerVoice(index switch
        {
            1 => audioData != null ? audioData.mkDeath01 : null,
            2 => audioData != null ? audioData.mkDeath02 : null,
            3 => audioData != null ? audioData.mkDeath03 : null,
            _ => null
        });
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void PlayFootstep(AudioClip[] clips, ref int lastIndex, float volume)
    {
        if (clips == null || clips.Length == 0 || sfxSource == null)
            return;

        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == lastIndex)
            index = (index + 1) % clips.Length;

        var clip = clips[index];
        if (clip == null)
            return;

        lastIndex = index;
        float originalPitch = sfxSource.pitch;
        sfxSource.pitch = Random.Range(0.94f, 1.06f);
        sfxSource.PlayOneShot(clip, volume);
        sfxSource.pitch = originalPitch;
    }

    public float PlayVoice(AudioClip clip)
    {
        return PlayVoiceInternal(clip, 1f, false);
    }

    public float PlayPlayerVoice(AudioClip clip)
    {
        return PlayVoiceInternal(clip, playerVoiceVolumeMultiplier, false);
    }

    private float PlayVoiceInternal(AudioClip clip, float volumeMultiplier, bool playingMonsterVoice)
    {
        if (voiceSource == null || clip == null)
            return 0f;

        BlockGameplayAmbience(clip.length);
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.loop = false;
        voiceSourceVolumeMultiplier = Mathf.Max(0f, volumeMultiplier);
        voiceSource.volume = GetVoiceSourceVolume(GetSettingsSfxVolume(), voiceSourceVolumeMultiplier);
        voiceSourcePlayingMonsterVoice = playingMonsterVoice;
        if (voiceSourceVolumeMultiplier > 1f)
            voiceSource.PlayOneShot(clip, voiceSourceVolumeMultiplier);
        else
            voiceSource.Play();
        return clip.length;
    }

    public float PlayMonsterVoice(AudioClip clip, float volume = 1f)
    {
        return PlayMonsterVoiceOverlay(clip, volume);
    }

    private float PlayMonsterVoiceOverlay(AudioClip clip, float volume)
    {
        return PlayVoiceInternal(clip, Mathf.Clamp01(volume), true);
    }

    private static float GetSettingsSfxVolume()
    {
        return GameData.Instance != null ? GameData.Instance.Settings.SfxVolume / 100f : 1f;
    }

    private static float GetVoiceSourceVolume(float settingsVolume, float multiplier)
    {
        multiplier = Mathf.Max(0f, multiplier);
        return multiplier > 1f ? Mathf.Clamp01(settingsVolume) : Mathf.Clamp01(settingsVolume * multiplier);
    }

    private static float CalculateMonsterVoiceDistanceVolume(float baseVolume)
    {
        const float minAudibleVolume = 0.22f;
        const float fullVolumeDistance = 3f;
        const float fadeDistance = 30f;

        var monster = FindFirstObjectByType<MonsterAI>(FindObjectsInactive.Include);
        var playerController = FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Exclude);
        if (monster == null || playerController == null)
            return Mathf.Clamp01(baseVolume);

        float distance = Vector3.Distance(monster.transform.position, playerController.transform.position);
        float t = Mathf.InverseLerp(fullVolumeDistance, fadeDistance, distance);
        float distanceMultiplier = Mathf.Lerp(1f, minAudibleVolume, t);
        return Mathf.Clamp01(Mathf.Clamp01(baseVolume) * distanceMultiplier);
    }

    private IEnumerator PauseVoiceForMonsterRoarRoutine(float seconds, System.Func<bool> shouldResume, bool monsterVoiceOnly)
    {
        AudioClip pausedClip = null;
        bool pausedVoice = voiceSource != null
            && voiceSource.isPlaying
            && (!monsterVoiceOnly || voiceSourcePlayingMonsterVoice);
        if (pausedVoice)
        {
            pausedClip = voiceSource.clip;
            voiceSource.Pause();
        }

        FpsHorrorKit.InteractMessageScript.Instance?.PauseMessageForSeconds(seconds, shouldResume);

        if (seconds > 0f)
            yield return new WaitForSeconds(seconds);

        bool canResume = shouldResume == null || shouldResume();
        if (pausedVoice
            && voiceSource != null
            && voiceSource.clip == pausedClip
            && !voiceSource.isPlaying)
        {
            if (canResume)
                voiceSource.UnPause();
            else
                StopVoice();
        }

        dialoguePauseRoutine = null;
    }

    public void StopVoice()
    {
        if (voiceSource == null)
            return;

        voiceSource.Stop();
        voiceSource.clip = null;
        voiceSourcePlayingMonsterVoice = false;
        voiceSourceVolumeMultiplier = 1f;
    }

    public void StopMonsterVoice()
    {
        if (!voiceSourcePlayingMonsterVoice || voiceSource == null)
            return;

        voiceSource.Stop();
        voiceSource.clip = null;
        voiceSourcePlayingMonsterVoice = false;
        voiceSourceVolumeMultiplier = 1f;
    }

    private void PlayUi(AudioClip clip)
    {
        if (clip != null && uiSource != null)
            uiSource.PlayOneShot(clip);
    }

    public void RequestGameplayAmbienceMoment()
    {
        if (gameplayAmbiencePlayedOnce
            || gameplayAmbienceSuppressed
            || gameplayAmbienceDisabledByPiano
            || FpsHorrorKit.PhysicalPianoController.IsAnyActive)
            return;

        gameplayAmbienceMomentRequested = true;
    }

    public void PlayStairEncounterThreatOnce(float fadeDuration = 10f)
    {
        if (stairEncounterThreatPlayed || audioData == null)
            return;

        stairEncounterThreatPlayed = true;
        StopGameplayAmbienceSequence();

        if (stairEncounterThreatRoutine != null)
            StopCoroutine(stairEncounterThreatRoutine);

        stairEncounterThreatRoutine = StartCoroutine(PlayStairEncounterThreatOnceRoutine(Mathf.Max(0.1f, fadeDuration)));
    }

    public void BlockGameplayAmbience(float seconds)
    {
        gameplayAmbienceBlockedUntil = Mathf.Max(gameplayAmbienceBlockedUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        StopCurrentGameplayAmbienceClip();
    }

    private static void PlayLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null)
            return;

        if (source.clip == clip && source.isPlaying)
            return;

        source.clip = clip;
        source.loop = true;
        source.Play();
    }

    private void PlayGameplayAmbienceClipOnce(AudioClip clip)
    {
        if (ambienceSource == null || clip == null)
            return;

        gameplayAmbiencePlayedOnce = true;
        ambienceSource.Stop();
        ambienceSource.clip = clip;
        ambienceSource.loop = false;
        ambienceSource.Play();
    }

    private IEnumerator PlayGameplayAmbienceSequence(AudioClip[] clips, float silenceSeconds)
    {
        int index = 0;
        float nextPlaybackTime = Time.unscaledTime + Mathf.Max(0f, silenceSeconds);

        int playedCount = 0;
        int maxPlays = CountPlayableClips(clips);
        while (playedCount < maxPlays)
        {
            if (ambienceSource == null)
                yield break;

            if (!gameplayAmbienceMomentRequested && Time.unscaledTime < nextPlaybackTime)
            {
                yield return null;
                continue;
            }

            if (IsGameplayAmbienceBlocked())
            {
                nextPlaybackTime = Time.unscaledTime + GameplayAmbienceBlockedRetryDelay();
                yield return null;
                continue;
            }

            var clip = NextClip(clips, ref index);
            if (clip == null)
                yield break;

            gameplayAmbienceMomentRequested = false;
            playedCount++;
            ambienceSource.Stop();
            ambienceSource.clip = clip;
            ambienceSource.loop = false;
            ambienceSource.Play();
            gameplayAmbienceClipPlaying = true;

            while (ambienceSource != null && ambienceSource.clip == clip && ambienceSource.isPlaying)
            {
                if (IsGameplayAmbienceBlocked())
                {
                    StopCurrentGameplayAmbienceClip();
                    break;
                }

                yield return null;
            }

            if (ambienceSource != null && ambienceSource.clip == clip)
                ambienceSource.clip = null;
            gameplayAmbienceClipPlaying = false;

            nextPlaybackTime = Time.unscaledTime + GameplayAmbienceDelay();
        }

        gameplayAmbienceRoutine = null;
        gameplayAmbienceSequenceActive = false;
        gameplayAmbienceMomentRequested = false;
        gameplayAmbienceClipPlaying = false;
    }

    private void StopGameplayAmbienceSequence()
    {
        if (gameplayAmbienceRoutine != null)
        {
            StopCoroutine(gameplayAmbienceRoutine);
            gameplayAmbienceRoutine = null;
        }

        gameplayAmbienceSequenceActive = false;
        gameplayAmbienceMomentRequested = false;
        gameplayAmbienceClipPlaying = false;
    }

    private void StopCurrentGameplayAmbienceClip()
    {
        if (!gameplayAmbienceClipPlaying || ambienceSource == null)
            return;

        ambienceSource.Stop();
        ambienceSource.clip = null;
        gameplayAmbienceClipPlaying = false;
    }

    private bool IsGameplayAmbienceBlocked()
    {
        if (Time.unscaledTime < gameplayAmbienceBlockedUntil)
            return true;
        if (backgroundDuckMultiplier < 0.99f)
            return true;
        if (gameplayAmbienceSuppressed)
            return true;
        if (gameplayAmbienceDisabledByPiano)
            return true;
        if (FpsHorrorKit.PhysicalPianoController.IsAnyActive)
            return true;
        if (voiceSource != null && voiceSource.isPlaying)
            return true;
        if (musicSource != null && musicSource.isPlaying && musicSource.clip != audioData?.menuMusic)
            return true;
        if (FpsHorrorKit.PianoPuzzleUI.IsAnyOpen)
            return true;

        return false;
    }

    private float GameplayAmbienceDelay()
    {
        if (audioData == null)
            return 45f;

        float min = Mathf.Max(0f, audioData.gameplayAmbienceSilenceSeconds);
        float max = Mathf.Max(min, audioData.gameplayAmbienceMaxSilenceSeconds);
        return Random.Range(min, max);
    }

    private float GameplayAmbienceBlockedRetryDelay()
    {
        return audioData != null ? Mathf.Max(0.1f, audioData.gameplayAmbienceBlockedRetrySeconds) : 3f;
    }

    private float PlayBlockingSfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
            BlockGameplayAmbience(clip.length);

        PlaySfx(clip, volume);
        return clip != null ? clip.length : 0f;
    }

    private IEnumerator PlayStairEncounterThreatOnceRoutine(float fadeDuration)
    {
        AudioSource[] sources = { ambienceSource, musicSource };
        AudioClip[] clips = { audioData != null ? audioData.ghostAmbience : null, audioData != null ? audioData.chaseMusic : null };

        float baseMusicVolume = GameData.Instance != null ? GameData.Instance.Settings.MusicVolume / 100f : 1f;
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null || clips[i] == null)
                continue;

            sources[i].Stop();
            sources[i].clip = clips[i];
            sources[i].loop = false;
            sources[i].volume = baseMusicVolume;
            sources[i].Play();
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float volume = baseMusicVolume * (1f - Mathf.Clamp01(elapsed / fadeDuration));
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i].clip == clips[i])
                    sources[i].volume = volume;
            }

            yield return null;
        }

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i].clip == clips[i])
                StopLoop(sources[i]);
        }

        if (GameData.Instance != null)
            ApplySettings(GameData.Instance.Settings);

        stairEncounterThreatRoutine = null;
    }

    private static AudioClip NextClip(AudioClip[] clips, ref int index)
    {
        if (clips == null || clips.Length == 0)
            return null;

        for (int i = 0; i < clips.Length; i++)
        {
            int clipIndex = index % clips.Length;
            index = (index + 1) % clips.Length;

            if (clips[clipIndex] != null)
                return clips[clipIndex];
        }

        return null;
    }

    private static AudioClip FirstPlayableClip(AudioClip[] clips)
    {
        if (clips == null)
            return null;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return clips[i];
        }

        return null;
    }

    private static bool HasPlayableClip(AudioClip[] clips)
    {
        if (clips == null)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    private static int CountPlayableClips(AudioClip[] clips)
    {
        if (clips == null)
            return 0;

        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                count++;
        }

        return count;
    }

    private static void StopLoop(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
    }

    private void StopAmbienceSource()
    {
        StopLoop(ambienceSource);
        gameplayAmbienceClipPlaying = false;
    }

    private void InitializeAsSingleton(AudioData data)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameObject.name = ManagerName;
        DontDestroyOnLoad(gameObject);

        if (data != null)
            audioData = data;
        if (audioData == null)
            audioData = Resources.Load<AudioData>(ResourcesAudioDataPath);

        EnsureSources();
        if (GameData.Instance != null)
            ApplySettings(GameData.Instance.Settings);
        PlayMenuMusic();
    }

    private void EnsureSources()
    {
        musicSource = EnsureSource("MusicSource", musicSource);
        ambienceSource = EnsureSource("AmbienceSource", ambienceSource);
        sfxSource = EnsureSource("SfxSource", sfxSource);
        uiSource = EnsureSource("UiSource", uiSource);
        voiceSource = EnsureSource("VoiceSource", voiceSource);

        if (musicSource != null) musicSource.playOnAwake = false;
        if (ambienceSource != null) ambienceSource.playOnAwake = false;
        if (sfxSource != null) sfxSource.playOnAwake = false;
        if (uiSource != null) uiSource.playOnAwake = false;
        if (voiceSource != null) voiceSource.playOnAwake = false;
    }

    private AudioSource EnsureSource(string sourceName, AudioSource source)
    {
        if (source != null)
            return source;

        Transform child = transform.Find(sourceName);
        if (child == null)
        {
            var childObject = new GameObject(sourceName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        var audioSource = child.GetComponent<AudioSource>();
        if (audioSource != null)
            return audioSource;

        audioSource = child.gameObject.AddComponent<AudioSource>();
        if (audioSource != null)
            return audioSource;

        var fallbackObject = new GameObject(sourceName);
        fallbackObject.transform.SetParent(transform, false);
        return fallbackObject.AddComponent<AudioSource>();
    }
}
