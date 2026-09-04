using FpsHorrorKit;
using UnityEngine;

namespace MainGame.P2
{
    [RequireComponent(typeof(Collider))]
    public sealed class P2DollPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData dollItem;
        [SerializeField] private AudioClip linhVoiceClip;
        [SerializeField, TextArea(2, 5)] private string linhSubtitle = "Cái người trong giếng... nó nói nó ở đây lâu lắm rồi. Nó muốn con xuống chơi với nó.";
        [SerializeField] private string interactText = "[E] Nhặt búp bê";
        [SerializeField, Min(0.1f)] private float fallbackSubtitleSeconds = 5f;

        public void Configure(ItemData item, AudioClip voiceClip)
        {
            dollItem = item;
            linhVoiceClip = voiceClip;
        }

        public void Interact()
        {
            if (InventoryManager.Instance == null || dollItem == null)
                return;

            if (!InventoryManager.Instance.AddItem(dollItem, 1))
            {
                InteractMessageScript.Instance?.ShowMessage("Hành trang đã đầy.");
                return;
            }

            AudioManager.Instance?.PlayItemPickup(dollItem);
            float voiceDuration = linhVoiceClip != null && AudioManager.Instance != null
                ? AudioManager.Instance.PlayVoice(linhVoiceClip)
                : 0f;

            if (!string.IsNullOrWhiteSpace(linhSubtitle))
            {
                float subtitleSeconds = voiceDuration > 0f ? voiceDuration : fallbackSubtitleSeconds;
                InteractMessageScript.Instance?.ShowMessage($"\"{linhSubtitle}\"", subtitleSeconds);
            }

            Destroy(gameObject);
        }

        public void Highlight()
        {
            PlayerInteract.Instance?.ChangeInteractText(interactText);
        }

        public void HoldInteract()
        {
        }

        public void UnHighlight()
        {
        }
    }
}
