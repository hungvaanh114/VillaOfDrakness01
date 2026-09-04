using System.Collections;
using FpsHorrorKit;
using UnityEngine;

namespace MainGame.P2
{
    [RequireComponent(typeof(P2AudioLogItem))]
    public sealed class P2AudioLogGhostPassSequence : MonoBehaviour
    {
        [Header("Ghost Pass")]
        [SerializeField] private P2GhostDoorApparitionDirector ghostDirector;
        [SerializeField] private Transform ghostPassStart;
        [SerializeField] private Transform ghostPassEnd;
        [SerializeField, Min(0f)] private float delayAfterAudio = 0.25f;
        [SerializeField, Min(0f)] private float delayAfterGhostPass = 0.35f;
        [SerializeField] private bool triggerOnce = true;

        [Header("Optional Door")]
        [SerializeField] private DoorSystem doorToOpenBeforePass;
        [SerializeField] private bool openDoorBeforePass;

        [Header("Ngoc Reaction")]
        [SerializeField] private AudioClip ngocReactionClip;
        [SerializeField, TextArea(1, 3)] private string ngocReactionSubtitle = "Bà dặn đừng nhìn vào mặt nước. Đừng nhìn vào.";
        [SerializeField, Min(0.1f)] private float fallbackSubtitleSeconds = 3f;

        private P2AudioLogItem audioLog;
        private Coroutine sequenceRoutine;
        private bool hasTriggered;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (audioLog != null)
                audioLog.PlaybackCompleted += HandlePlaybackCompleted;
        }

        private void OnDisable()
        {
            if (audioLog != null)
                audioLog.PlaybackCompleted -= HandlePlaybackCompleted;
        }

        private void HandlePlaybackCompleted(P2AudioLogItem item)
        {
            if (triggerOnce && hasTriggered)
                return;

            hasTriggered = true;
            if (sequenceRoutine != null)
                StopCoroutine(sequenceRoutine);
            sequenceRoutine = StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            if (delayAfterAudio > 0f)
                yield return new WaitForSeconds(delayAfterAudio);

            if (openDoorBeforePass && doorToOpenBeforePass != null)
                doorToOpenBeforePass.TryOpenForMonster();

            bool waitingForGhost = false;
            ResolveReferences();
            if (ghostDirector != null && ghostPassStart != null && ghostPassEnd != null)
            {
                waitingForGhost = true;
                ghostDirector.PlayScriptedPass(ghostPassStart, ghostPassEnd, () => waitingForGhost = false);
            }

            while (waitingForGhost)
                yield return null;

            if (delayAfterGhostPass > 0f)
                yield return new WaitForSeconds(delayAfterGhostPass);

            PlayNgocReaction();
            sequenceRoutine = null;
        }

        private void PlayNgocReaction()
        {
            var clip = ngocReactionClip != null
                ? ngocReactionClip
                : Resources.Load<AudioData>("Audio/AudioData")?.p2Ngoc05;

            float duration = AudioManager.Instance != null
                ? AudioManager.Instance.PlayPlayerVoice(clip)
                : 0f;

            if (!string.IsNullOrWhiteSpace(ngocReactionSubtitle))
                InteractMessageScript.Instance?.ShowMessage($"\"{ngocReactionSubtitle}\"", duration > 0f ? duration : fallbackSubtitleSeconds);
        }

        private void ResolveReferences()
        {
            if (audioLog == null)
                audioLog = GetComponent<P2AudioLogItem>();
            if (ghostDirector == null)
                ghostDirector = FindFirstObjectByType<P2GhostDoorApparitionDirector>(FindObjectsInactive.Include);
        }
    }
}
