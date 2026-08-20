using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class MusicSheetPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private MusicSheetData musicSheetData;
        [SerializeField] private string interactText = "[E] Nhặt mảnh nhạc";

        public void Interact()
        {
            if (MusicSheetManager.Instance == null || musicSheetData == null)
                return;

            if (MusicSheetManager.Instance.Collect(musicSheetData))
            {
                AudioManager.Instance?.PlayNotePickup();
                InteractMessageScript.Instance?.ShowMessage($"Đã nhặt mảnh nhạc {musicSheetData.index}.");
                Destroy(gameObject);
            }
        }

        public void Highlight()
        {
            PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }
    }
}
