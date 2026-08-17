using UnityEngine;

[CreateAssetMenu(menuName = "MainGame/Audio Data", fileName = "AudioData")]
public sealed class AudioData : ScriptableObject
{
    [Header("Music")]
    public AudioClip menuMusic;
    public AudioClip gameplayAmbience;

    [Header("UI")]
    public AudioClip buttonHover;
    public AudioClip buttonClick;
    public AudioClip applySettings;
    public AudioClip back;

    [Header("Gameplay SFX")]
    public AudioClip flashlightToggle;
    public AudioClip cameraShot;
    public AudioClip keyPickup;
    public AudioClip notePickup;
}
