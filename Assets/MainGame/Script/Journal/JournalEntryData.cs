using UnityEngine;

namespace FpsHorrorKit
{
    [CreateAssetMenu(fileName = "JournalEntry", menuName = "MainGame/Journal/Entry")]
    public sealed class JournalEntryData : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea(4, 12)] public string content;
    }
}
