using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MainGame.P2
{
    public sealed class P2OilLamp : MonoBehaviour
    {
        [SerializeField] private Light flameLight;
        [SerializeField] private Renderer flameRenderer;
        [SerializeField] private ParticleSystem[] flameParticles = Array.Empty<ParticleSystem>();
        [SerializeField] private P2GhostController ghost;
        [SerializeField] private Transform shakeRoot;
        [SerializeField] private bool controlsGameplaySystems = true;
        [SerializeField] private Image oilFillImage;
        [SerializeField, Range(0f, 100f)] private float oilPercent = 100f;
        [SerializeField, Min(0f)] private float oilDrainPerSecond = 0.15f;
        [SerializeField, Min(0f)] private float flameBaseIntensity = 1.15f;
        [SerializeField, Min(0f)] private float flamePulseIntensity = 0.45f;
        [SerializeField, Min(0f)] private float flameBaseRange = 2.4f;
        [SerializeField, Min(0f)] private float flameDangerRange = 1.8f;
        [SerializeField, Min(0.1f)] private float nearGhostEffectStartDistance = 12f;
        [SerializeField, Min(0.1f)] private float nearGhostFullEffectDistance = 2f;
        [SerializeField, Min(0f)] private float dangerShakePosition = 0.045f;
        [SerializeField, Min(0f)] private float dangerShakeRotation = 5f;
        [SerializeField] private Color normalFlameColor = new Color(1f, 0.48f, 0.16f);
        [SerializeField] private Color dangerFlameColor = new Color(0.35f, 0.85f, 1f);
        [SerializeField] private bool debugNearGhostEffectZone = true;
        [SerializeField] private float directAttackSeconds = 10f;
        [SerializeField] private float dangerDistance = 7f;

        public bool IsLit { get; private set; } = true;
        public bool ControlsGameplaySystems => controlsGameplaySystems;

        private static P2OilLamp gameplayLamp;

        private float unlitNearGhostTimer;
        private Material flameMaterial;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;

        private void OnValidate()
        {
            if (shakeRoot == null)
                shakeRoot = transform;
            if (!controlsGameplaySystems)
                oilFillImage = null;
        }

        private void Awake()
        {
            if (controlsGameplaySystems)
                gameplayLamp = this;

            if (shakeRoot == null)
                shakeRoot = transform;
            if (flameLight == null)
                flameLight = GetComponentInChildren<Light>();
            if (flameRenderer != null)
                flameMaterial = flameRenderer.material;
            baseLocalPosition = shakeRoot.localPosition;
            baseLocalRotation = shakeRoot.localRotation;
            ResolveOilFillImage();
            UpdateOilUi();
            SetFlameParticles(IsLit);
        }

        private void Update()
        {
            if (controlsGameplaySystems)
            {
                if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && !(P2GameController.Instance?.IsInputLocked ?? false))
                    SetLit(!IsLit);

                DrainOil();
                TrackUnlitDanger();
                UpdateOilUi();
            }
            else
            {
                MirrorGameplayLampState();
            }

            ApplyFlicker();
            ApplyNearGhostReaction();
        }

        public void SetLit(bool lit)
        {
            if (lit && oilPercent <= 0f)
            {
                ApplyLitVisual(false);
                if (controlsGameplaySystems)
                    P2GameController.Instance?.ShowPrompt("Den dau da het dau.");
                UpdateOilUi();
                return;
            }

            ApplyLitVisual(lit);
            if (controlsGameplaySystems)
                P2GameController.Instance?.ShowPrompt(lit ? "Den dau da chay lai." : "Den dau tat. Khong the doc chu.");
        }

        private void ApplyLitVisual(bool lit)
        {
            IsLit = lit;
            if (flameLight != null)
                flameLight.enabled = lit;
            if (flameRenderer != null)
                flameRenderer.enabled = lit;
            SetFlameParticles(lit);
        }

        private void MirrorGameplayLampState()
        {
            if (gameplayLamp == null || gameplayLamp == this)
                return;

            if (IsLit != gameplayLamp.IsLit)
                ApplyLitVisual(gameplayLamp.IsLit);
        }

        private void SetFlameParticles(bool lit)
        {
            if (flameParticles == null)
                return;

            foreach (var particles in flameParticles)
            {
                if (particles == null)
                    continue;

                if (lit)
                {
                    if (!particles.isPlaying)
                        particles.Play(true);
                }
                else
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void DrainOil()
        {
            if (!IsLit || oilDrainPerSecond <= 0f)
                return;

            oilPercent = Mathf.Max(0f, oilPercent - oilDrainPerSecond * Time.deltaTime);
            if (oilPercent <= 0f)
                SetLit(false);
        }

        private void UpdateOilUi()
        {
            ResolveOilFillImage();
            if (oilFillImage != null)
                oilFillImage.fillAmount = Mathf.Clamp01(oilPercent / 100f);
        }

        private void ResolveOilFillImage()
        {
            if (oilFillImage != null)
                return;

            var fillObject = GameObject.Find("LanternFuelFill");
            if (fillObject != null)
                oilFillImage = fillObject.GetComponent<Image>();
        }

        private void ApplyFlicker()
        {
            if (!IsLit || flameLight == null)
                return;

            var danger = GetNearGhostEffect01();
            flameLight.intensity = flameBaseIntensity + Mathf.Sin(Time.time * Mathf.Lerp(5f, 18f, danger)) * Mathf.Lerp(0.06f, flamePulseIntensity, danger);
            flameLight.range = Mathf.Lerp(flameBaseRange, flameDangerRange, danger);
            flameLight.color = Color.Lerp(normalFlameColor, dangerFlameColor, danger);

            if (flameMaterial != null)
                flameMaterial.color = Color.Lerp(new Color(1f, 0.55f, 0.18f), dangerFlameColor, danger);

            ApplyParticleDangerColor(danger);
        }

        private void ApplyNearGhostReaction()
        {
            if (shakeRoot == null)
                return;

            var danger = IsLit ? GetNearGhostEffect01() : 0f;
            if (danger <= 0.001f)
            {
                shakeRoot.localPosition = Vector3.Lerp(shakeRoot.localPosition, baseLocalPosition, Time.deltaTime * 12f);
                shakeRoot.localRotation = Quaternion.Slerp(shakeRoot.localRotation, baseLocalRotation, Time.deltaTime * 12f);
                return;
            }

            var shakeSpeed = Mathf.Lerp(16f, 42f, danger);
            var positionAmount = dangerShakePosition * danger;
            var rotationAmount = dangerShakeRotation * danger;
            var shakeOffset = new Vector3(
                Mathf.PerlinNoise(Time.time * shakeSpeed, 0.13f) - 0.5f,
                Mathf.PerlinNoise(1.71f, Time.time * shakeSpeed) - 0.5f,
                Mathf.PerlinNoise(Time.time * shakeSpeed, 3.29f) - 0.5f) * positionAmount;

            var shakeRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(5.17f, Time.time * shakeSpeed) - 0.5f) * rotationAmount,
                (Mathf.PerlinNoise(Time.time * shakeSpeed, 8.33f) - 0.5f) * rotationAmount,
                (Mathf.PerlinNoise(11.9f, Time.time * shakeSpeed) - 0.5f) * rotationAmount);

            shakeRoot.localPosition = baseLocalPosition + shakeOffset;
            shakeRoot.localRotation = baseLocalRotation * shakeRotation;
        }

        private void ApplyParticleDangerColor(float danger)
        {
            if (flameParticles == null)
                return;

            var warmStart = new Color(1f, 0.86f, 0.25f, 0.95f);
            var warmEnd = new Color(1f, 0.28f, 0.04f, 0.75f);
            var blueStart = new Color(0.5f, 0.95f, 1f, 0.98f);
            var blueEnd = new Color(0.08f, 0.42f, 1f, 0.8f);

            foreach (var particles in flameParticles)
            {
                if (particles == null)
                    continue;

                var main = particles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    Color.Lerp(warmStart, blueStart, danger),
                    Color.Lerp(warmEnd, blueEnd, danger));

                var emission = particles.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                    Mathf.Lerp(18f, 36f, danger),
                    Mathf.Lerp(28f, 52f, danger));

                var noise = particles.noise;
                noise.strength = new ParticleSystem.MinMaxCurve(
                    Mathf.Lerp(0.04f, 0.16f, danger),
                    Mathf.Lerp(0.1f, 0.32f, danger));
                noise.frequency = Mathf.Lerp(7f, 16f, danger);
            }
        }

        private float GetNearGhostEffect01()
        {
            if (ghost == null)
                return 0f;

            var controller = P2GameController.Instance;
            var distance = controller != null
                ? controller.DistanceToPlayer(ghost.transform.position)
                : Vector3.Distance(transform.position, ghost.transform.position);
            var startDistance = Mathf.Max(nearGhostEffectStartDistance, nearGhostFullEffectDistance + 0.01f);
            return Mathf.InverseLerp(startDistance, nearGhostFullEffectDistance, distance);
        }

        private void TrackUnlitDanger()
        {
            if (IsLit || ghost == null || !ghost.IsAwakened)
            {
                unlitNearGhostTimer = 0f;
                return;
            }

            if (P2GameController.Instance.DistanceToPlayer(ghost.transform.position) > dangerDistance)
            {
                unlitNearGhostTimer = 0f;
                return;
            }

            unlitNearGhostTimer += Time.deltaTime;
            if (unlitNearGhostTimer >= directAttackSeconds)
            {
                unlitNearGhostTimer = -999f;
                ghost.ForceChase();
                P2GameController.Instance.ShowPrompt("Bong toi khong che duoc lau nua.");
            }
        }

        private void OnDrawGizmos()
        {
            if (!debugNearGhostEffectZone)
                return;

            var origin = transform.position;
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.55f);
            Gizmos.DrawWireSphere(origin, nearGhostEffectStartDistance);
            Gizmos.color = new Color(0.05f, 0.35f, 1f, 0.8f);
            Gizmos.DrawWireSphere(origin, nearGhostFullEffectDistance);
            if (ghost != null)
            {
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.25f);
                Gizmos.DrawLine(origin, ghost.transform.position);
            }
        }
    }
}
