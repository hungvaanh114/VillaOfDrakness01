using System;
using UnityEngine;

[CreateAssetMenu(menuName = "MainGame/Cut Scene/Intro Data", fileName = "IntroCutSceneData")]
public sealed class IntroCutSceneData : ScriptableObject
{
    public IntroCutSceneCue[] cues = Array.Empty<IntroCutSceneCue>();
}

[Serializable]
public sealed class IntroCutSceneCue
{
    [TextArea(2, 6)] public string text;
    public AudioClip audioClip;
    [Min(0.5f)] public float fallbackDuration = 4f;
    [Min(0f)] public float waitAfter = 0.15f;
    public IntroPlayerTarget playerTarget = IntroPlayerTarget.None;
    public IntroCameraShot cameraShot = IntroCameraShot.BehindShoulder;
}

public enum IntroPlayerTarget
{
    None,
    ForestStart,
    GateApproach,
    FrontDoor,
    FenceWalk,
    DiningWindow,
    DiningRoom
}

public enum IntroCameraShot
{
    OverheadFollow,
    DescendBehind,
    SignClose,
    BehindShoulder,
    WindowInspect,
    InteriorSettle
}
