using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CutSceneSequence : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string cutSceneId = "intro";
    [SerializeField] private bool playOnce = true;

    [Header("Start")]
    [SerializeField] private bool teleportToStartPoint = true;
    [SerializeField] private bool teleportCameraToFirstShot = true;
    [SerializeField] private Transform startPoint;

    [Header("Route")]
    [SerializeField] private List<CutScenePoint> points = new();

    public string CutSceneId => cutSceneId;
    public bool PlayOnce => playOnce;
    public bool TeleportToStartPoint => teleportToStartPoint;
    public bool TeleportCameraToFirstShot => teleportCameraToFirstShot;
    public Transform StartPoint => startPoint;
    public IReadOnlyList<CutScenePoint> Points => points;

    public void Configure(
        string id,
        bool sequencePlaysOnce,
        Transform start,
        bool shouldTeleportToStart,
        IEnumerable<CutScenePoint> route)
    {
        cutSceneId = id;
        playOnce = sequencePlaysOnce;
        startPoint = start;
        teleportToStartPoint = shouldTeleportToStart;
        teleportCameraToFirstShot = true;
        points.Clear();
        if (route != null)
            points.AddRange(route);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(cutSceneId))
            cutSceneId = gameObject.name;
    }
}

[Serializable]
public sealed class CutScenePoint
{
    [Header("Point")]
    public string name;
    public Transform point;
    public bool moveToPoint = true;
    [Tooltip("<= 0 means use CutSceneManager default speed.")]
    public float moveSpeedOverride = -1f;
    [Tooltip("<= 0 means use CutSceneManager default turn speed.")]
    public float turnSpeedOverride = -1f;

    [Header("Dialogue")]
    [HideInInspector]
    public string dialogueId;
    [Tooltip("Edit dialogue directly here. Only text inside double quotes is shown as spoken subtitle.")]
    [InspectorName("Dialogue Text")]
    [TextArea(2, 8)] public string overrideText;
    [InspectorName("Voice Clip")]
    public AudioClip overrideAudioClip;
    [InspectorName("Fallback Duration")]
    [Min(0.5f)] public float overrideFallbackDuration = 4f;
    [Min(0f)] public float waitAfter = 0.15f;

    [Header("Camera")]
    public CutSceneCameraShot cameraShot = CutSceneCameraShot.BehindShoulder;
    public Transform cameraPositionOverride;
    public Transform cameraLookAtOverride;
    public bool cameraLookAtPlayer = true;
    [Min(0f)] public float cameraLookHeight = 1.35f;
    [Tooltip("<= 0 means use CutSceneManager default smooth time.")]
    public float cameraSmoothTimeOverride = -1f;
    public bool useCustomCameraOffset;
    public Vector3 customCameraOffset = new Vector3(0.45f, 2.1f, -3.2f);

    public CutScenePoint()
    {
    }

    public CutScenePoint(string name, Transform point, string dialogueId, CutSceneCameraShot cameraShot)
    {
        this.name = name;
        this.point = point;
        this.dialogueId = dialogueId;
        this.cameraShot = cameraShot;
    }
}

public enum CutSceneCameraShot
{
    OverheadFollow,
    DescendBehind,
    SignClose,
    BehindShoulder,
    WindowInspect,
    InteriorSettle
}
