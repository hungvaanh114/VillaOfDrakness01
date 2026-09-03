using FpsHorrorKit;
using UnityEngine;

namespace MainGame.P2
{
    [RequireComponent(typeof(Collider))]
    public sealed class P2AudioLogItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, TextArea(3, 8)] private string subtitle;
        [SerializeField] private string interactText = "[E] Nghe hộp ghi âm";
        [SerializeField] private bool canReplay = true;
        [SerializeField] private AudioSource fallbackSource;

        private bool hasPlayed;

        public void Configure(AudioClip newClip, string newSubtitle, string newInteractText, bool allowReplay)
        {
            clip = newClip;
            subtitle = newSubtitle;
            interactText = newInteractText;
            canReplay = allowReplay;
        }

        public void Interact()
        {
            if (hasPlayed && !canReplay)
                return;

            hasPlayed = true;
            float duration = PlayClip();
            if (duration <= 0f)
                duration = Mathf.Clamp(string.IsNullOrWhiteSpace(subtitle) ? 3f : subtitle.Length * 0.055f, 3f, 14f);

            if (!string.IsNullOrWhiteSpace(subtitle))
                InteractMessageScript.Instance?.ShowMessage($"\"{subtitle}\"", duration);
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

        private float PlayClip()
        {
            if (clip == null)
            {
                AudioManager.Instance?.PlayGenericInteract();
                return 0f;
            }

            if (AudioManager.Instance != null)
                return AudioManager.Instance.PlayVoice(clip);

            if (fallbackSource == null)
                fallbackSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

            fallbackSource.Stop();
            fallbackSource.clip = clip;
            fallbackSource.loop = false;
            fallbackSource.Play();
            return clip.length;
        }
    }
}
