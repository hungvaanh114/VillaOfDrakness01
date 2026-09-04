using FpsHorrorKit;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MainGame.P2
{
    [RequireComponent(typeof(Collider))]
    public sealed class P2AudioLogItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private string interactText = "[E] Nghe hộp ghi âm";
        [SerializeField] private string playbackMessage = "Hộp ghi âm... nhấn SPACE để bỏ qua.";
        [SerializeField] private Key skipKey = Key.Space;
        [SerializeField, Min(0.1f)] private float messageRefreshSeconds = 1.1f;
        [SerializeField] private bool canReplay = true;
        [SerializeField] private AudioSource fallbackSource;

        private bool hasPlayed;
        private bool isPlaying;
        private Coroutine playbackCompletedRoutine;
        private Coroutine messageRoutine;

        public static bool IsSpaceSkipActive { get; private set; }

        public event Action<P2AudioLogItem, float> PlaybackStarted;
        public event Action<P2AudioLogItem> PlaybackCompleted;

        public void Configure(AudioClip newClip, string newSubtitle, string newInteractText, bool allowReplay)
        {
            clip = newClip;
            interactText = newInteractText;
            canReplay = allowReplay;
        }

        private void Update()
        {
            if (!isPlaying || skipKey == Key.None)
                return;

            if (FpsAssetsInputs.Instance != null)
                FpsAssetsInputs.Instance.jump = false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[skipKey].wasPressedThisFrame)
                CompletePlayback(true);
        }

        private void OnDisable()
        {
            StopMessageRoutine();
            if (IsSpaceSkipActive)
                IsSpaceSkipActive = false;
            if (playbackCompletedRoutine != null)
            {
                StopCoroutine(playbackCompletedRoutine);
                playbackCompletedRoutine = null;
            }
        }

        public void Interact()
        {
            if (hasPlayed && !canReplay)
                return;

            hasPlayed = true;
            float duration = PlayClip();
            if (duration <= 0f)
                duration = clip != null ? Mathf.Max(0.1f, clip.length) : 3f;

            isPlaying = duration > 0f;
            IsSpaceSkipActive = isPlaying && skipKey == Key.Space;
            StartMessageRoutine();

            PlaybackStarted?.Invoke(this, duration);
            if (playbackCompletedRoutine != null)
                StopCoroutine(playbackCompletedRoutine);
            playbackCompletedRoutine = StartCoroutine(PlaybackCompletedAfter(duration));
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

        private IEnumerator PlaybackCompletedAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, seconds));
            playbackCompletedRoutine = null;
            CompletePlayback(false);
        }

        private void CompletePlayback(bool skipped)
        {
            bool wasPlaying = isPlaying;
            isPlaying = false;
            if (IsSpaceSkipActive)
                IsSpaceSkipActive = false;

            if (playbackCompletedRoutine != null)
            {
                StopCoroutine(playbackCompletedRoutine);
                playbackCompletedRoutine = null;
            }

            StopMessageRoutine();
            if (skipped)
            {
                AudioManager.Instance?.StopVoice();
                if (fallbackSource != null)
                    fallbackSource.Stop();
                if (FpsAssetsInputs.Instance != null)
                    FpsAssetsInputs.Instance.jump = false;
                InteractMessageScript.Instance?.ClearMessage();
            }

            if (wasPlaying)
                PlaybackCompleted?.Invoke(this);
        }

        private void StartMessageRoutine()
        {
            StopMessageRoutine();
            if (!string.IsNullOrWhiteSpace(playbackMessage))
                messageRoutine = StartCoroutine(MessageRoutine());
        }

        private void StopMessageRoutine()
        {
            if (messageRoutine == null)
                return;

            StopCoroutine(messageRoutine);
            messageRoutine = null;
        }

        private IEnumerator MessageRoutine()
        {
            while (isPlaying)
            {
                InteractMessageScript.Instance?.ShowMessage(playbackMessage, messageRefreshSeconds + 0.2f);
                yield return new WaitForSecondsRealtime(messageRefreshSeconds);
            }

            messageRoutine = null;
        }
    }
}
