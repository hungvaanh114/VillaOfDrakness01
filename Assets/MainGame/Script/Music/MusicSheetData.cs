using UnityEngine;

namespace FpsHorrorKit
{
    [CreateAssetMenu(fileName = "MusicSheetData", menuName = "MainGame/Music/Music Sheet Data")]
    public sealed class MusicSheetData : ScriptableObject
    {
        public string musicSheetID;
        public int index;
        public Sprite icon;
        [TextArea(3, 8)] public string description;
    }
}
