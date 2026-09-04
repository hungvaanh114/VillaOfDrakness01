using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class PianoInteractable : MonoBehaviour, IInteractable
    {
        [Header("Testing")]
        [SerializeField] private bool testTreatMusicSheetCompleted;
        [SerializeField] private bool requireMusicSheets = true;
        [SerializeField] private bool blockWhenCompleted = true;
        [SerializeField] private bool markCheckpointOnInteract = true;

        [SerializeField] private PhysicalPianoController physicalPiano;
        [SerializeField] private string interactText = "[E] Ch\u01a1i piano";
        [SerializeField] private string missingMusicSheetText = "T\u00ecm \u0111\u1ee7 n\u1ed1t nh\u1ea1c";

        public void Interact()
        {
            if (blockWhenCompleted && IsCompleted())
                return;

            if (requireMusicSheets && !HasRequiredMusicSheets())
            {
                AudioManager.Instance?.PlayDoorLocked();
                InteractMessageScript.Instance?.ShowMessage(GetMissingMusicSheetMessage());
                return;
            }

            AudioManager.Instance?.PlayButtonClick();
            if (markCheckpointOnInteract)
                ChapterOneCheckpointManager.Instance?.MarkPianoCheckpoint();
            if (GetPhysicalPiano() != null)
            {
                physicalPiano.ActivateFromPianoInteract();
                return;
            }

            PianoPuzzleUI.Instance?.Open();
        }

        public void Highlight()
        {
            if (blockWhenCompleted && IsCompleted())
                return;

            PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract() { }
        public void UnHighlight() { }

        private bool HasRequiredMusicSheets()
        {
            return testTreatMusicSheetCompleted
                || (MusicSheetManager.Instance != null && MusicSheetManager.Instance.MusicSheetCompleted);
        }

        private PhysicalPianoController GetPhysicalPiano()
        {
            if (physicalPiano != null)
                return physicalPiano;

            physicalPiano = GetComponent<PhysicalPianoController>()
                ?? GetComponentInParent<PhysicalPianoController>()
                ?? GetComponentInChildren<PhysicalPianoController>(true)
                ?? PhysicalPianoController.FindScenePiano();

            return physicalPiano;
        }

        private bool IsCompleted()
        {
            return GetPhysicalPiano() != null && physicalPiano.IsCompleted;
        }

        private string GetMissingMusicSheetMessage()
        {
            var manager = MusicSheetManager.Instance;
            if (manager == null)
                return missingMusicSheetText;

            return $"{missingMusicSheetText} ({manager.CollectedMusicSheetCount}/{manager.RequiredMusicSheetCount})";
        }
    }
}
