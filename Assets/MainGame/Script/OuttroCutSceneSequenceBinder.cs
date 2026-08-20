using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(CutSceneSequence))]
public sealed class OuttroCutSceneSequenceBinder : MonoBehaviour
{
    [SerializeField] private bool rebuildSequence;
    [SerializeField] private Transform playerPoint01;
    [SerializeField] private Transform playerPoint02;
    [SerializeField] private Transform playerLookWellPoint;
    [SerializeField] private Transform cameraBackHighPoint;
    [SerializeField] private Transform cameraWellMouthPoint;
    [SerializeField] private Transform cameraLookAtPoint;

    private CutSceneSequence sequence;

    private void Awake()
    {
        ResolveReferences();
        if (rebuildSequence || IsSequenceEmpty())
        {
            rebuildSequence = false;
            Configure();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        if (!rebuildSequence && !IsSequenceEmpty())
            return;

        rebuildSequence = false;
        Configure();
    }

    public void Configure()
    {
        ResolveReferences();
        if (sequence == null || playerPoint01 == null || playerPoint02 == null || playerLookWellPoint == null)
            return;

        var points = new List<CutScenePoint>
        {
            new()
            {
                name = "Chạy ra sân sau",
                point = playerPoint01,
                moveToPoint = true,
                moveSpeedOverride = 5.8f,
                turnSpeedOverride = 720f,
                overrideText = "\"Khoa tìm đường ra sân sau, hộp nhạc vẫn ôm chặt trong tay.\"",
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
                moveSpeedOverride = 5.8f,
                turnSpeedOverride = 720f,
                overrideText = string.Empty,
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
                overrideFallbackDuration = 2.3f,
                waitAfter = 0.35f,
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
                overrideFallbackDuration = 0.85f,
                waitAfter = 0.75f,
                cameraShot = CutSceneCameraShot.WindowInspect,
                cameraPositionOverride = cameraWellMouthPoint,
                cameraLookAtOverride = cameraLookAtPoint,
                cameraLookAtPlayer = false,
                cameraLookHeight = 0f,
                cameraSmoothTimeOverride = 0.12f
            }
        };

        sequence.Configure("outtro", false, null, false, points);
    }

    private void ResolveReferences()
    {
        if (sequence == null)
            sequence = GetComponent<CutSceneSequence>();

        playerPoint01 ??= FindChild("OuttroPlayerPoint_01");
        playerPoint02 ??= FindChild("OuttroPlayerPoint_02");
        playerLookWellPoint ??= FindChild("OuttroPlayerLookWell");
        cameraBackHighPoint ??= FindChild("OuttroCameraBackHigh");
        cameraWellMouthPoint ??= FindChild("OuttroCameraWellMouth");
        cameraLookAtPoint ??= FindChild("OuttroCameraLookAt");
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
