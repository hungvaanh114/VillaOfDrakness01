using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MainGame/Cut Scene/Dialogue Data", fileName = "CutSceneDialogueData")]
public sealed class CutSceneDialogueData : ScriptableObject
{
    [SerializeField] private List<CutSceneDialogueLine> lines = new();

    public IReadOnlyList<CutSceneDialogueLine> Lines => lines;

    public bool TryGetLine(string id, out CutSceneDialogueLine line)
    {
        line = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        foreach (var item in lines)
        {
            if (item != null && string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase))
            {
                line = item;
                return true;
            }
        }

        return false;
    }

    public void ReplaceLines(IEnumerable<CutSceneDialogueLine> newLines)
    {
        lines.Clear();
        if (newLines == null)
            return;

        lines.AddRange(newLines);
    }
}

[Serializable]
public sealed class CutSceneDialogueLine
{
    public string id;
    [TextArea(2, 6)] public string text;
    public AudioClip audioClip;
    [Min(0.5f)] public float fallbackDuration = 4f;

    public CutSceneDialogueLine()
    {
    }

    public CutSceneDialogueLine(string id, string text, float fallbackDuration, AudioClip audioClip = null)
    {
        this.id = id;
        this.text = text;
        this.fallbackDuration = fallbackDuration;
        this.audioClip = audioClip;
    }
}
