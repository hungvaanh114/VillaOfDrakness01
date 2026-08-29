using UnityEngine;

[CreateAssetMenu(menuName = "MainGame/Audio Data", fileName = "AudioData")]
public sealed class AudioData : ScriptableObject
{
    [Header("Music")]
    public AudioClip menuMusic;
    public AudioClip introAmbience;
    public AudioClip gameplayAmbience;
    public AudioClip[] gameplayAmbiences;
    [Min(0f)] public float gameplayAmbienceSilenceSeconds = 45f;
    [Min(0f)] public float gameplayAmbienceMaxSilenceSeconds = 90f;
    [Min(0.1f)] public float gameplayAmbienceBlockedRetrySeconds = 3f;
    public AudioClip ghostAmbience;
    public AudioClip chaseMusic;
    public AudioClip deathMusic;

    [Header("UI")]
    public AudioClip buttonHover;
    public AudioClip buttonClick;
    public AudioClip applySettings;
    public AudioClip back;

    [Header("Gameplay SFX")]
    public AudioClip flashlightToggle;
    public AudioClip flashlightBatteryUse;
    public AudioClip cameraShot;
    public AudioClip keyPickup;
    public AudioClip notePickup;
    public AudioClip paperPickup;
    public AudioClip diaryPageFlip;
    public AudioClip genericInteract;
    public AudioClip doorLocked;
    public AudioClip doorOpenSlow;
    public AudioClip doorUnlock;
    public AudioClip itemUnlock;
    public AudioClip musicBoxStartup;
    public AudioClip pianoWrong;
    public AudioClip sanityWarning;
    public AudioClip ghostJumpscare;
    public AudioClip wellJumpscare;
    public AudioClip clothTearOff;
    public AudioClip dialogueAdvance;
    public AudioClip dialogueTextBlip;
    public AudioClip[] footstepGround;
    public AudioClip[] footstepWood;

    [Header("Chapter 1 Voice")]
    public AudioClip intro01;
    public AudioClip intro02;
    public AudioClip intro03;
    public AudioClip intro04;
    public AudioClip intro05;
    public AudioClip baLanTapeFull;
    public AudioClip maVuDaiPatrolFull;
    public AudioClip diaryReaction01;
    public AudioClip diaryReaction02;
    public AudioClip diaryReaction03;
    public AudioClip mkHide01;
    public AudioClip mkHide02;
    public AudioClip mkHide03;
    public AudioClip mkHide04;
    public AudioClip mkHide05;
    public AudioClip mkHide06;
    public AudioClip mkDeath01;
    public AudioClip mkDeath02;
    public AudioClip mkDeath03;
}
