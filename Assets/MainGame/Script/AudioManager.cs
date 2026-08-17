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
            musicSource.volume = settings.MusicVolume / 100f;
        if (ambienceSource != null)
            ambienceSource.volume = settings.MusicVolume / 100f;
        if (sfxSource != null)
            sfxSource.volume = settings.SfxVolume / 100f;
        if (uiSource != null)
            uiSource.volume = settings.SfxVolume / 100f;
    }

    public void PlayMenuMusic()
    {
        PlayLoop(musicSource, audioData != null ? audioData.menuMusic : null);
    }

    public void PlayGameplayAmbience()
    {
        PlayLoop(ambienceSource, audioData != null ? audioData.gameplayAmbience : null);
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

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
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

        musicSource.playOnAwake = false;
        ambienceSource.playOnAwake = false;
        sfxSource.playOnAwake = false;
        uiSource.playOnAwake = false;
    }

    private AudioSource EnsureSource(string sourceName, AudioSource source)
    {
        if (source != null)
            return source;

        var child = transform.Find(sourceName);
        if (child == null)
        {
            var childObject = new GameObject(sourceName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        return child.GetComponent<AudioSource>() ?? child.gameObject.AddComponent<AudioSource>();
    }
}
