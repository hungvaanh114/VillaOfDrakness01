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

    private int lastGroundFootstepIndex = -1;
    private int lastWoodFootstepIndex = -1;
    private float backgroundDuckMultiplier = 1f;
    private bool maVuDaiPatrolPlayed;

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
            voiceSource.volume = settings.SfxVolume / 100f;
    }

    public void PlayMenuMusic()
    {
        PlayLoop(musicSource, audioData != null ? audioData.menuMusic : null);
    }

    public void PlayGameplayAmbience()
    {
        PlayLoop(ambienceSource, audioData != null ? audioData.gameplayAmbience : null);
    }

    public void PlayIntroAmbience()
    {
        StopMusic();
        PlayLoop(ambienceSource, audioData != null ? audioData.introAmbience : null);
    }

    public void PlayGhostAmbience()
    {
        PlayLoop(ambienceSource, audioData != null ? audioData.ghostAmbience : null);
    }

    public void PlayChaseMusic()
    {
        PlayLoop(musicSource, audioData != null ? audioData.chaseMusic : null);
    }

    public void PlayDeathMusic()
    {
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
        if (GameData.Instance != null)
            ApplySettings(GameData.Instance.Settings);
    }

    public void ClearBackgroundDuck()
    {
        backgroundDuckMultiplier = 1f;
        if (GameData.Instance != null)
            ApplySettings(GameData.Instance.Settings);
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
        PlaySfx(audioData != null ? audioData.musicBoxStartup : null);
    }

    public void PlayPianoWrong()
    {
        PlaySfx(audioData != null ? audioData.pianoWrong : null);
    }

    public void PlaySanityWarning()
    {
        PlaySfx(audioData != null ? audioData.sanityWarning : null);
    }

    public void PlayGhostJumpscare(float volume = 1f)
    {
        PlaySfx(audioData != null ? audioData.ghostJumpscare : null, volume);
    }

    public void PlayWellJumpscare()
    {
        PlaySfx(audioData != null ? audioData.wellJumpscare : null);
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

    public float PlayMaVuDaiPatrol()
    {
        if (maVuDaiPatrolPlayed)
            return 0f;

        maVuDaiPatrolPlayed = true;
        return PlayVoice(audioData != null ? audioData.maVuDaiPatrolFull : null);
    }

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
        return PlayVoice(index switch
        {
            1 => audioData != null ? audioData.diaryReaction01 : null,
            2 => audioData != null ? audioData.diaryReaction02 : null,
            3 => audioData != null ? audioData.diaryReaction03 : null,
            _ => null
        });
    }

    public float PlayHideVoice(int index)
    {
        return PlayVoice(index switch
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
        return PlayVoice(index switch
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
        if (voiceSource == null || clip == null)
            return 0f;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();
        return clip.length;
    }

    public void StopVoice()
    {
        if (voiceSource == null)
            return;

        voiceSource.Stop();
        voiceSource.clip = null;
    }

    private void PlayUi(AudioClip clip)
    {
        if (clip != null && uiSource != null)
            uiSource.PlayOneShot(clip);
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

    private static void StopLoop(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
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
