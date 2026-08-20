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

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (audioSource != null && audioSource.isPlaying && recordTransform != null)
                recordTransform.Rotate(Vector3.up, recordRotationSpeed * Time.deltaTime, Space.Self);

            if (isPlayingTape && audioSource != null && !audioSource.isPlaying)
                FinishTape(true);
        }

        public bool IsPlayingTape => isPlayingTape && audioSource != null && audioSource.isPlaying;
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
            isPlayingTape = true;

            if (duckBackgroundDuringTape)
            {
                AudioManager.Instance?.SetBackgroundDuck(backgroundDuckMultiplier);
                duckedBackground = true;
            }
        }

        public void SkipTape()
        {
            ResolveReferences();

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

            FinishTape(isPlayingTape);
        }

        private void FinishTape(bool notifyFinished)
        {
            isPlayingTape = false;

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
