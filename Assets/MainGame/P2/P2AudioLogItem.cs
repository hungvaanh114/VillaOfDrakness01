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
        [SerializeField] private bool lockPlayerDuringPlayback = true;
        [SerializeField] private bool hideGhostDuringPlayback = true;
        [SerializeField] private AudioSource fallbackSource;

        private bool hasPlayed;
        private bool isPlaying;
        private bool lockApplied;
        private bool changedGameState;
        private GameController.GameState previousGameState;
        private bool previousRaycastState = true;
        private bool changedRaycastState;
        private bool previousCutSceneState;
        private bool previousInteractingState;
        private FpsController lockedPlayerController;
        private P2GhostDoorApparitionDirector ghostDirector;
        private Coroutine playbackCompletedRoutine;
        private Coroutine messageRoutine;
        private static int activePlaybackLocks;
        private static bool blockSpaceJumpUntilRelease;

        public static bool IsSpaceSkipActive { get; private set; }
        public static bool IsAnyPlaybackLocked => activePlaybackLocks > 0;
        public static bool ShouldBlockSpaceJump
        {
            get
            {
                if (blockSpaceJumpUntilRelease && (Keyboard.current == null || !Keyboard.current.spaceKey.isPressed))
                    blockSpaceJumpUntilRelease = false;

                return IsSpaceSkipActive || blockSpaceJumpUntilRelease;
            }
        }

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
            if (!isPlaying)
                return;

            FpsAssetsInputs.Instance?.ClearGameplayInput();
            if (lockedPlayerController != null)
                lockedPlayerController.StopCutSceneMovement();

            if (skipKey == Key.None)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[skipKey].wasPressedThisFrame)
                CompletePlayback(true);
        }

        private void OnDisable()
        {
            StopMessageRoutine();
            EndPlaybackLock();
            if (IsSpaceSkipActive)
                IsSpaceSkipActive = false;
            blockSpaceJumpUntilRelease = false;
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
            BeginPlaybackLock();

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
            EndPlaybackLock();
            if (skipped)
            {
                if (skipKey == Key.Space)
                    blockSpaceJumpUntilRelease = true;
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

        private void BeginPlaybackLock()
        {
            if (lockApplied)
                return;

            lockApplied = true;
            activePlaybackLocks++;
            lockedPlayerController = ResolvePlayerController();

            if (lockPlayerDuringPlayback && lockedPlayerController != null)
            {
                previousCutSceneState = lockedPlayerController.isCutScene;
                previousInteractingState = lockedPlayerController.isInteracting;
            }

            var gameController = GameController.Instance;
            if (lockPlayerDuringPlayback && gameController != null)
            {
                previousGameState = gameController.currentGameState;
                changedGameState = true;
                if (previousGameState != GameController.GameState.Dead
                    && previousGameState != GameController.GameState.Ending)
                {
                    gameController.SetGameState(GameController.GameState.Dialogue);
                }
            }

            if (lockPlayerDuringPlayback && lockedPlayerController != null)
            {
                lockedPlayerController.isCutScene = true;
                lockedPlayerController.isInteracting = true;
                lockedPlayerController.StopCutSceneMovement();
            }

            if (PlayerInteract.Instance != null)
            {
                previousRaycastState = PlayerInteract.Instance.sendRaycast;
                PlayerInteract.Instance.sendRaycast = false;
                changedRaycastState = true;
            }

            if (lockPlayerDuringPlayback)
                P2GameController.Instance?.LockInput(true);

            if (hideGhostDuringPlayback)
                ResolveGhostDirector()?.SetAudioLogSuspended(true);

            FpsAssetsInputs.Instance?.ClearGameplayInput();
        }

        private void EndPlaybackLock()
        {
            if (!lockApplied)
                return;

            if (hideGhostDuringPlayback)
                ResolveGhostDirector()?.SetAudioLogSuspended(false);

            if (changedRaycastState && PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = previousRaycastState;

            if (lockPlayerDuringPlayback)
                P2GameController.Instance?.LockInput(false);

            var gameController = GameController.Instance;
            if (changedGameState
                && gameController != null
                && gameController.currentGameState == GameController.GameState.Dialogue)
            {
                gameController.SetGameState(previousGameState);
            }

            if (lockPlayerDuringPlayback && lockedPlayerController != null)
            {
                lockedPlayerController.isCutScene = previousCutSceneState;
                lockedPlayerController.isInteracting = previousInteractingState;
                lockedPlayerController.StopCutSceneMovement();
            }

            FpsAssetsInputs.Instance?.ClearGameplayInput();
            changedRaycastState = false;
            changedGameState = false;
            lockedPlayerController = null;
            lockApplied = false;
            activePlaybackLocks = Mathf.Max(0, activePlaybackLocks - 1);
        }

        private FpsController ResolvePlayerController()
        {
            if (GameController.Instance != null && GameController.Instance.playerController != null)
                return GameController.Instance.playerController;

            return FindFirstObjectByType<FpsController>(FindObjectsInactive.Include);
        }

        private P2GhostDoorApparitionDirector ResolveGhostDirector()
        {
            if (ghostDirector == null)
                ghostDirector = FindFirstObjectByType<P2GhostDoorApparitionDirector>(FindObjectsInactive.Include);

            return ghostDirector;
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
