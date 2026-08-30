using UnityEngine;

namespace FpsHorrorKit
{
    using System;

    public sealed class GramophoneTapePlayer : MonoBehaviour
    {
        public event Action TapeFinished;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip tapeClip;
        [SerializeField] private Transform recordTransform;
        [SerializeField] private float recordRotationSpeed = 240f;
        [SerializeField] private bool playOnce = true;
        [Header("Mix")]
        [SerializeField, Range(0f, 2f)] private float tapeVolume = 1.65f;
        [SerializeField, Range(0f, 1f)] private float tapeSpatialBlend = 0.25f;
        [SerializeField, Min(0.1f)] private float tapeMinDistance = 6f;
        [SerializeField, Min(1f)] private float tapeMaxDistance = 60f;
        [SerializeField] private bool duckBackgroundDuringTape = true;
        [SerializeField, Range(0f, 1f)] private float backgroundDuckMultiplier = 0.28f;
        [SerializeField] private GameObject cuasoObject;
        [SerializeField] private string cuasoObjectName = "cuaso";
        [SerializeField] private bool activateCuasoOnPlay = true;

        private bool hasPlayed;
        private bool isPlayingTape;
        private bool duckedBackground;
        private bool pausedByFocusLoss;
        private float lastKnownTapeTime;
        private float suppressStoppedCheckUntil;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                lastKnownTapeTime = audioSource.time;
            }

            if (audioSource != null && audioSource.isPlaying && recordTransform != null)
                recordTransform.Rotate(Vector3.up, recordRotationSpeed * Time.deltaTime, Space.Self);

            if (isPlayingTape && audioSource != null && !audioSource.isPlaying)
            {
                if (!Application.isFocused || Time.unscaledTime < suppressStoppedCheckUntil)
                    return;

                if (HasTapeReachedEnd())
                    FinishTape(true);
                else
                    ResumeTapeFromLastKnownTime();
            }
        }

        public bool IsPlayingTape => isPlayingTape;
        public float TapeLength => tapeClip != null ? tapeClip.length : 0f;

        public void PlayTape()
        {
            if (playOnce && hasPlayed)
                return;

            ResolveReferences();
            if (audioSource == null || tapeClip == null)
                return;

            hasPlayed = true;
            if (activateCuasoOnPlay)
                ResolveCuasoObject()?.SetActive(true);

            DoorSystem.LockDoorsMarkedForGramophoneTape();

            audioSource.Stop();
            audioSource.clip = tapeClip;
            audioSource.loop = false;
            audioSource.volume = tapeVolume;
            audioSource.spatialBlend = tapeSpatialBlend;
            audioSource.minDistance = tapeMinDistance;
            audioSource.maxDistance = tapeMaxDistance;
            audioSource.Play();
            AudioManager.Instance?.BlockGameplayAmbience(tapeClip.length);
            isPlayingTape = true;
            pausedByFocusLoss = false;
            lastKnownTapeTime = 0f;
            suppressStoppedCheckUntil = Time.unscaledTime + 0.25f;

            if (duckBackgroundDuringTape)
            {
                AudioManager.Instance?.SetBackgroundDuck(backgroundDuckMultiplier);
                duckedBackground = true;
            }
        }

        public void SkipTape()
        {
            if (!isPlayingTape)
                return;

            ResolveReferences();

            if (audioSource != null)
            {
                lastKnownTapeTime = 0f;
                audioSource.Stop();
                audioSource.clip = null;
            }

            FinishTape(isPlayingTape);
        }

        private void FinishTape(bool notifyFinished)
        {
            isPlayingTape = false;
            pausedByFocusLoss = false;
            lastKnownTapeTime = 0f;
            suppressStoppedCheckUntil = 0f;

            if (duckedBackground)
            {
                AudioManager.Instance?.ClearBackgroundDuck();
                duckedBackground = false;
            }

            if (notifyFinished)
                TapeFinished?.Invoke();
        }

        private void OnDisable()
        {
            if (duckedBackground)
                AudioManager.Instance?.ClearBackgroundDuck();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!isPlayingTape || audioSource == null)
                return;

            suppressStoppedCheckUntil = Time.unscaledTime + 0.5f;

            if (!hasFocus)
            {
                lastKnownTapeTime = audioSource.time;
                if (audioSource.isPlaying)
                {
                    audioSource.Pause();
                    pausedByFocusLoss = true;
                }

                return;
            }

            if (pausedByFocusLoss || !audioSource.isPlaying)
                ResumeTapeFromLastKnownTime();
        }

        private bool HasTapeReachedEnd()
        {
            if (tapeClip == null)
                return true;

            float endTime = Mathf.Max(0.05f, tapeClip.length - 0.12f);
            return lastKnownTapeTime >= endTime || audioSource.time >= endTime;
        }

        private void ResumeTapeFromLastKnownTime()
        {
            if (!isPlayingTape || audioSource == null || tapeClip == null)
                return;

            audioSource.clip = tapeClip;
            audioSource.loop = false;
            audioSource.volume = tapeVolume;
            audioSource.spatialBlend = tapeSpatialBlend;
            audioSource.minDistance = tapeMinDistance;
            audioSource.maxDistance = tapeMaxDistance;
            audioSource.time = Mathf.Clamp(lastKnownTapeTime, 0f, Mathf.Max(0f, tapeClip.length - 0.05f));
            audioSource.Play();
            AudioManager.Instance?.BlockGameplayAmbience(tapeClip.length - audioSource.time);
            pausedByFocusLoss = false;
            suppressStoppedCheckUntil = Time.unscaledTime + 0.5f;
        }

        private void ResolveReferences()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

            if (recordTransform == null)
                recordTransform = FindChildByName(transform, "record");
        }

        private GameObject ResolveCuasoObject()
        {
            if (cuasoObject != null)
                return cuasoObject;

            if (string.IsNullOrWhiteSpace(cuasoObjectName))
                return null;

            cuasoObject = GameObject.Find(cuasoObjectName);
            return cuasoObject;
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                var found = FindChildByName(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
