using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(CutSceneSequence))]
public sealed class OuttroCutSceneSequenceBinder : MonoBehaviour
{
    private const string DeathVoice01AssetPath = "Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-DEATH-01.wav";
    private const string DeathVoice02AssetPath = "Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-DEATH-02.wav";
    private const string DeathVoice01Subtitle = "\"Cái gì vậy... ánh sáng trong giếng? Không lẽ... có người bị kẹt dưới đó?\"";
    private const string DeathVoice02Subtitle = "\"Ơ... cái gì-\"";
    private const string ChapterTwoLine09Subtitle = "\"Gương bạc... bà nói soi vào trăng thì có thể... nhưng bà không nói phải làm gì tiếp theo. Con còn thiếu gì đó.\"";

    [SerializeField] private bool rebuildSequence;
    [SerializeField] private bool useChapterTwoEnding;
    [SerializeField] private Transform playerPoint01;
    [SerializeField] private Transform playerPoint02;
    [SerializeField] private Transform playerLookWellPoint;
    [SerializeField] private Transform cameraBackHighPoint;
    [SerializeField] private Transform cameraWellMouthPoint;
    [SerializeField] private Transform cameraLookAtPoint;
    [SerializeField] private AudioClip deathVoice01;
    [SerializeField] private AudioClip deathVoice02;
    [SerializeField] private AudioClip chapterTwoLine09;

    private CutSceneSequence sequence;

    private void Awake()
    {
        ResolveReferences();
        if (UseChapterTwoEnding() || rebuildSequence || IsSequenceEmpty())
        {
            rebuildSequence = false;
            Configure();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
#if UNITY_EDITOR
        deathVoice01 ??= UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DeathVoice01AssetPath);
        deathVoice02 ??= UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DeathVoice02AssetPath);
#endif
        if (!UseChapterTwoEnding() && !rebuildSequence && !IsSequenceEmpty())
            return;

        rebuildSequence = false;
        Configure();
    }

    public void Configure()
    {
        ResolveReferences();
        if (sequence == null || playerPoint01 == null)
            return;

        if (UseChapterTwoEnding())
        {
            ConfigureChapterTwo();
            return;
        }

        if (playerPoint02 == null || playerLookWellPoint == null)
            return;

        var points = new List<CutScenePoint>
        {
            new()
            {
                name = "Chạy ra sân sau",
                point = playerPoint01,
                moveToPoint = true,
                moveSpeedOverride = 2.1f,
                turnSpeedOverride = 720f,
                overrideText = "\"Khoa tìm đường ra sân sau, hộp nhạc vẫn ôm chặt trong tay.\"",
                overrideAudioClip = deathVoice01,
                overrideFallbackDuration = 2.2f,
                waitAfter = 0.1f,
                cameraShot = CutSceneCameraShot.BehindShoulder,
                cameraPositionOverride = cameraBackHighPoint,
                cameraLookAtPlayer = true,
                cameraLookHeight = 1.25f,
                cameraSmoothTimeOverride = 0.18f
            },
            new()
            {
                name = "Áp sát giếng",
                point = playerPoint02,
                moveToPoint = true,
                moveSpeedOverride = 2.1f,
                turnSpeedOverride = 720f,
                overrideText = string.Empty,
                overrideAudioClip = deathVoice02,
                overrideFallbackDuration = 1.1f,
                waitAfter = 0.05f,
                cameraShot = CutSceneCameraShot.DescendBehind,
                cameraPositionOverride = cameraBackHighPoint,
                cameraLookAtPlayer = true,
                cameraLookHeight = 1.25f,
                cameraSmoothTimeOverride = 0.16f
            },
            new()
            {
                name = "Nhìn xuống giếng",
                point = playerLookWellPoint,
                moveToPoint = true,
                moveSpeedOverride = 4.4f,
                turnSpeedOverride = 720f,
                overrideText = "\"Cái gì vậy... ánh sáng trong giếng?\"",
                overrideFallbackDuration = 1.15f,
                waitAfter = 0.175f,
                cameraShot = CutSceneCameraShot.WindowInspect,
                cameraPositionOverride = cameraWellMouthPoint,
                cameraLookAtOverride = cameraLookAtPoint,
                cameraLookAtPlayer = false,
                cameraLookHeight = 0f,
                cameraSmoothTimeOverride = 0.2f
            },
            new()
            {
                name = "Mặt giếng phản chiếu",
                point = playerLookWellPoint,
                moveToPoint = false,
                overrideText = "\"Ơ... cái gì-\"",
                overrideFallbackDuration = 0.425f,
                waitAfter = 0.375f,
                cameraShot = CutSceneCameraShot.WindowInspect,
                cameraPositionOverride = cameraWellMouthPoint,
                cameraLookAtOverride = cameraLookAtPoint,
                cameraLookAtPlayer = false,
                cameraLookHeight = 0f,
                cameraSmoothTimeOverride = 0.12f
            }
        };

        points[0].overrideText = DeathVoice01Subtitle;
        points[1].overrideText = DeathVoice02Subtitle;
        points[2].overrideText = string.Empty;
        points[3].overrideText = string.Empty;

        sequence.Configure("outtro", false, null, false, points);
    }

    private void ConfigureChapterTwo()
    {
        var points = new List<CutScenePoint>
        {
            new()
            {
                name = "Chạy ra sân sau",
                point = playerPoint01,
                moveToPoint = true,
                moveSpeedOverride = 1.5f,
                turnSpeedOverride = 720f,
                overrideText = string.Empty,
                overrideFallbackDuration = 2f,
                waitAfter = 0f,
                cameraShot = CutSceneCameraShot.BehindShoulder,
                cameraPositionOverride = cameraBackHighPoint,
                cameraLookAtPlayer = true,
                cameraLookHeight = 1.25f,
                cameraSmoothTimeOverride = 0.18f
            },
            new()
            {
                name = "Ngọc nói trước sân",
                point = playerPoint01,
                moveToPoint = false,
                dialogueId = "p2_ngoc_09",
                overrideText = ChapterTwoLine09Subtitle,
                overrideAudioClip = chapterTwoLine09,
                overrideFallbackDuration = 7f,
                waitAfter = 0.05f,
                cameraShot = CutSceneCameraShot.BehindShoulder,
                cameraPositionOverride = cameraBackHighPoint,
                cameraLookAtPlayer = true,
                cameraLookHeight = 1.25f,
                cameraSmoothTimeOverride = 0.16f
            }
        };

        sequence.Configure("outtro", false, null, false, points);
    }

    private void ResolveReferences()
    {
        if (sequence == null)
            sequence = GetComponent<CutSceneSequence>();

        if (deathVoice01 == null || deathVoice02 == null)
        {
            var audioData = Resources.Load<AudioData>("Audio/AudioData");
            if (audioData != null)
            {
                deathVoice01 ??= audioData.mkDeath01;
                deathVoice02 ??= audioData.mkDeath02;
            }
        }

        if (chapterTwoLine09 == null)
        {
            var audioData = Resources.Load<AudioData>("Audio/AudioData");
            if (audioData != null)
                chapterTwoLine09 ??= audioData.p2Ngoc09;
        }

        playerPoint01 ??= FindChild("OuttroPlayerPoint_01");
        playerPoint02 ??= FindChild("OuttroPlayerPoint_02");
        playerLookWellPoint ??= FindChild("OuttroPlayerLookWell");
        cameraBackHighPoint ??= FindChild("OuttroCameraBackHigh");
        cameraWellMouthPoint ??= FindChild("OuttroCameraWellMouth");
        cameraLookAtPoint ??= FindChild("OuttroCameraLookAt");
    }

    private bool UseChapterTwoEnding()
    {
        return useChapterTwoEnding || gameObject.scene.name == "GameP2";
    }

    private bool IsSequenceEmpty()
    {
        if (sequence == null)
            sequence = GetComponent<CutSceneSequence>();

        return sequence == null || sequence.Points == null || sequence.Points.Count == 0;
    }

    private Transform FindChild(string childName)
    {
        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && child.name == childName)
                return child;
        }

        return null;
    }
}
