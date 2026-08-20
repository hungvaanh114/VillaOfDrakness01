using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class PianoInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactText = "[E] Ch\u01a1i piano";
        [SerializeField] private string missingMusicSheetText = "T\u00ecm \u0111\u1ee7 n\u1ed1t nh\u1ea1c";

        public void Interact()
        {
            if (MusicSheetManager.Instance == null || !MusicSheetManager.Instance.MusicSheetCompleted)
            {
                AudioManager.Instance?.PlayDoorLocked();
                InteractMessageScript.Instance?.ShowMessage(GetMissingMusicSheetMessage());
                return;
            }

            AudioManager.Instance?.PlayButtonClick();
            PianoPuzzleUI.Instance?.Open();
        }

        public void Highlight()
        {
            PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }

        private string GetMissingMusicSheetMessage()
        {
            var manager = MusicSheetManager.Instance;
            if (manager == null)
                return missingMusicSheetText;

            return $"{missingMusicSheetText} ({manager.CollectedMusicSheetCount}/{manager.RequiredMusicSheetCount})";
        }
    }
}
