using System;
using System.Collections;
using FpsHorrorKit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MainGame.P2
{
    [DisallowMultipleComponent]
    public sealed class P2KnockPlankPuzzle : MonoBehaviour
    {
        [Header("Planks")]
        [SerializeField] private P2KnockPlank[] planks = Array.Empty<P2KnockPlank>();
        [SerializeField] private bool requireSequentialClicks = true;
        [SerializeField] private int hollowPlankIndex = 5;

        [Header("Audio")]
        [SerializeField] private AudioClip[] noteClips = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip hollowThudClip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Min(0.1f)] private float audioMinDistance = 2f;
        [SerializeField, Min(1f)] private float audioMaxDistance = 18f;

        [Header("Reveal")]
        [SerializeField] private GameObject hiddenCavity;
        [SerializeField] private Vector3 hollowPopLocalOffset = new(0.42f, -0.42f, 0.08f);
        [SerializeField] private Vector3 hollowPopLocalEuler = new(0f, 0f, -18f);
        [SerializeField, Min(0.05f)] private float hollowPopSeconds = 0.55f;
        [SerializeField, Min(0f)] private float exitDelayAfterReveal = 0.25f;

        [Header("Click")]
        [SerializeField, Min(0.5f)] private float clickDistance = 12f;
        [SerializeField] private LayerMask clickMask = ~0;

        private Camera activeCamera;
        private P2KnockPlank focusedPlank;
        private Action completedCallback;
        private int nextExpectedIndex;
        private bool isActive;
        private bool resolving;
        private bool completed;

        public bool IsActive => isActive;

        public void Configure(
            P2KnockPlank[] newPlanks,
            AudioClip[] newNoteClips,
            AudioClip newHollowThudClip,
            GameObject newHiddenCavity)
        {
            planks = newPlanks ?? Array.Empty<P2KnockPlank>();
            noteClips = newNoteClips ?? Array.Empty<AudioClip>();
            hollowThudClip = newHollowThudClip;
            hiddenCavity = newHiddenCavity;
            hollowPlankIndex = Mathf.Max(0, planks.Length - 1);
        }

        public void BeginZoomInteraction(Camera camera, Action onCompleted)
        {
            if (completed)
            {
                onCompleted?.Invoke();
                return;
            }

            activeCamera = camera != null ? camera : Camera.main;
            completedCallback = onCompleted;
            nextExpectedIndex = 0;
            resolving = false;
            isActive = true;
            SetAllPlankColliders(true);
            InteractMessageScript.Instance?.ShowMessage("Bấm lần lượt từng tấm ván.");
        }

        private void Update()
        {
            if (!isActive || resolving)
                return;

            UpdateFocusedPlank();

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && focusedPlank != null)
                HandlePlankClick(focusedPlank);
        }

        private void OnDisable()
        {
            ClearFocus();
            isActive = false;
            resolving = false;
        }

        private void HandlePlankClick(P2KnockPlank plank)
        {
            if (plank == null)
                return;

            if (requireSequentialClicks && plank.PlankIndex != nextExpectedIndex)
            {
                InteractMessageScript.Instance?.ShowMessage("Gõ lần lượt từ trái sang phải.", 1.1f);
                return;
            }

            PlayPlankSound(plank);

            if (plank.IsHollow || plank.PlankIndex == hollowPlankIndex)
            {
                StartCoroutine(RevealRoutine(plank));
                return;
            }

            nextExpectedIndex = Mathf.Clamp(nextExpectedIndex + 1, 0, planks.Length - 1);
        }

        private IEnumerator RevealRoutine(P2KnockPlank hollowPlank)
        {
            resolving = true;
            completed = true;
            ClearFocus();

            if (hollowPlank != null)
                yield return hollowPlank.PopOff(hollowPopLocalOffset, hollowPopLocalEuler, hollowPopSeconds);

            if (hiddenCavity != null)
                hiddenCavity.SetActive(true);

            P2GameController.Instance?.RegisterWallOpened(hiddenCavity);

            if (exitDelayAfterReveal > 0f)
                yield return new WaitForSeconds(exitDelayAfterReveal);

            isActive = false;
            resolving = false;
            completedCallback?.Invoke();
            completedCallback = null;
        }

        private void PlayPlankSound(P2KnockPlank plank)
        {
            var clip = GetClipForPlank(plank);
            if (clip == null)
            {
                P2GameController.Instance?.PlayKnock(plank != null && plank.IsHollow);
                return;
            }

            var source = plank.GetComponent<AudioSource>();
            if (source == null)
                source = plank.gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = Mathf.Max(0.1f, audioMinDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 1f, audioMaxDistance);
            source.PlayOneShot(clip, volume);
        }

        private AudioClip GetClipForPlank(P2KnockPlank plank)
        {
            if (plank == null)
                return null;

            if (plank.IsHollow || plank.PlankIndex == hollowPlankIndex)
                return hollowThudClip;

            var index = plank.PlankIndex;
            return noteClips != null && index >= 0 && index < noteClips.Length ? noteClips[index] : null;
        }

        private void UpdateFocusedPlank()
        {
            var nextFocus = FindMousePlank();
            if (nextFocus == focusedPlank)
                return;

            ClearFocus();
            focusedPlank = nextFocus;
            focusedPlank?.SetFocused(true);
        }

        private P2KnockPlank FindMousePlank()
        {
            if (activeCamera == null)
                activeCamera = Camera.main;
            if (activeCamera == null || Mouse.current == null)
                return null;

            var ray = activeCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return Physics.Raycast(ray, out var hit, clickDistance, clickMask, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<P2KnockPlank>()
                : null;
        }

        private void ClearFocus()
        {
            if (focusedPlank != null)
                focusedPlank.SetFocused(false);
            focusedPlank = null;
        }

        private void SetAllPlankColliders(bool enabled)
        {
            if (planks == null)
                return;

            foreach (var plank in planks)
            {
                if (plank != null)
                    plank.SetColliderEnabled(enabled);
            }
        }
    }
}
