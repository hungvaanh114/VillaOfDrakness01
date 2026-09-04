using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MainGame.P2
{
    public sealed class P2OilLamp : MonoBehaviour
    {
        [Header("Lanterns")]
        [SerializeField] private Transform gameplayLanternRoot;
        [SerializeField] private Transform gameplayFlameSwayRoot;
        [SerializeField] private Light gameplayFlameLight;
        [SerializeField] private Renderer gameplayFlameRenderer;
        [SerializeField] private ParticleSystem[] gameplayFlameParticles = Array.Empty<ParticleSystem>();

        [Header("Danger Target")]
        [SerializeField] private P2GhostController ghost;
        [SerializeField] private Transform dangerTarget;

        [Header("UI")]
        [SerializeField] private Image oilFillImage;
        [SerializeField] private TMP_Text oilPercentText;
        [SerializeField] private Color oilFillColor = new Color(1f, 0.77f, 0.12f, 0.95f);

        [Header("Oil")]
        [SerializeField, Range(0f, 100f)] private float oilPercent = 100f;
        [SerializeField, Min(0f)] private float oilDrainPerSecond = 0.15f;
        [SerializeField] private Key toggleKey = Key.T;
        [SerializeField] private bool startLitOnGameplay = true;
        [SerializeField] private bool forceLitDuringCutscene = true;
        [SerializeField] private bool drainOilDuringCutscene;
        [SerializeField] private bool acceptExternalFlashlightToggle;

        [Header("Near Monster Color")]
        [SerializeField] private Color dangerFlameColor = new Color(0.35f, 0.85f, 1f);
        [SerializeField, Range(0f, 1f)] private float dangerColorBlend = 1f;

        [Header("Near Monster Effect")]
        [SerializeField, Min(0.1f)] private float nearGhostEffectStartDistance = 12f;
        [SerializeField, Min(0.1f)] private float nearGhostFullEffectDistance = 2f;
        [SerializeField, Min(0f)] private float dangerShakePosition = 0.045f;
        [SerializeField, Min(0f)] private float dangerShakeRotation = 5f;
        [SerializeField, Min(0f)] private float dangerFlameSwayPosition = 0.012f;
        [SerializeField, Min(0f)] private float dangerFlameSwayRotation = 4f;
        [SerializeField] private bool debugNearGhostEffectZone = true;
        [SerializeField] private float directAttackSeconds = 10f;
        [SerializeField] private float dangerDistance = 7f;

        public bool IsLit { get; private set; } = true;
        public bool ControlsGameplaySystems => true;

        private static P2OilLamp gameplayLamp;

        private readonly LampRuntime gameplayRuntime = new();
        private float unlitNearGhostTimer;
        private bool forcedLitForCurrentGameplay;
        private bool wasInCutsceneState;
        private bool litBeforeCutscene = true;

        private sealed class LampRuntime
        {
            public Transform Root;
            public Light Light;
            public Renderer FlameRenderer;
            public ParticleSystem[] Particles = Array.Empty<ParticleSystem>();
            public ParticleSystem.MinMaxGradient[] AuthoredParticleColors = Array.Empty<ParticleSystem.MinMaxGradient>();
            public Material FlameMaterial;
            public Vector3 BaseLocalPosition;
            public Quaternion BaseLocalRotation;
            public Transform FlameSwayRoot;
            public Vector3 BaseFlameSwayLocalPosition;
            public Quaternion BaseFlameSwayLocalRotation;
            public float AuthoredIntensity;
            public float AuthoredRange;
            public Color AuthoredColor = Color.white;
            public Color AuthoredMaterialColor = Color.white;
        }

        private void Awake()
        {
            gameplayLamp = this;
            ResolveReferences();
            CaptureRuntime(gameplayRuntime, gameplayLanternRoot, gameplayFlameSwayRoot, gameplayFlameLight, gameplayFlameRenderer, gameplayFlameParticles);
            ResolveDangerTarget();
            ResolveOilUi();
            UpdateOilUi();
            ApplyLitVisual(startLitOnGameplay || IsLit);
        }

        private void Start()
        {
            ForceLitForFreshGameplayIfNeeded();
        }

        private void Update()
        {
            HandleLampToggleInput();
            HandleCutsceneLampState();
            ForceLitForFreshGameplayIfNeeded();

            DrainOil();
            TrackUnlitDanger();
            UpdateOilUi();

            var danger = IsLit ? GetNearGhostEffect01() : 0f;
            ApplyNearMonsterColorEffect(gameplayRuntime, danger);
            ApplyNearGhostReaction(gameplayRuntime, danger);
        }

        public void SetLit(bool lit)
        {
            SetLitInternal(lit, false, false);
        }

        private void HandleLampToggleInput()
        {
            if (!WasTogglePressedThisFrame() || IsLampInputLocked())
                return;

            SetLitInternal(!IsLit, true, true);
        }

        private bool WasTogglePressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[toggleKey].wasPressedThisFrame;
        }

        private static bool IsLampInputLocked()
        {
            return global::GameController.IsGameplayInputLocked()
                || (P2GameController.Instance?.IsInputLocked ?? false);
        }

        private void ForceLitForFreshGameplayIfNeeded()
        {
            if (!startLitOnGameplay || forcedLitForCurrentGameplay)
                return;

            var controller = global::GameController.Instance;
            if (controller != null && controller.currentGameState != global::GameController.GameState.Gameplay)
                return;

            if (oilPercent <= 0f)
                oilPercent = 100f;

            ApplyLitVisual(true);
            UpdateOilUi();
            forcedLitForCurrentGameplay = true;
        }

        private void HandleCutsceneLampState()
        {
            bool isCutscene = IsCutsceneLampState();
            if (!forceLitDuringCutscene)
            {
                wasInCutsceneState = isCutscene;
                return;
            }

            if (isCutscene)
            {
                if (!wasInCutsceneState)
                    litBeforeCutscene = IsLit;

                if (!IsLit)
                    ApplyLitVisual(true);
            }
            else if (wasInCutsceneState)
            {
                ApplyLitVisual(oilPercent > 0f && litBeforeCutscene);
            }

            wasInCutsceneState = isCutscene;
        }

        private static bool IsCutsceneLampState()
        {
            var controller = global::GameController.Instance;
            return controller != null
                && (controller.currentGameState == global::GameController.GameState.Cutscene
                    || controller.currentGameState == global::GameController.GameState.Ending
                    || controller.currentGameState == global::GameController.GameState.Dead);
        }

        private void SetLitInternal(bool lit, bool showMessage, bool force)
        {
            if (!lit && !force && !acceptExternalFlashlightToggle)
            {
                ApplyLitVisual(IsLit);
                return;
            }

            if (lit && oilPercent <= 0f)
            {
                ApplyLitVisual(false);
                if (showMessage)
                    ShowLampMessage("Đèn dầu đã hết dầu.");
                UpdateOilUi();
                return;
            }

            ApplyLitVisual(lit);
            if (showMessage)
                ShowLampMessage(lit ? "Đèn dầu đã được bật lại." : "Bạn thổi tắt đèn dầu.");
        }

        private void ApplyLitVisual(bool lit)
        {
            IsLit = lit;
            SetLampLit(gameplayRuntime, lit);
        }

        private static void SetLampLit(LampRuntime lamp, bool lit)
        {
            if (lamp == null)
                return;

            if (lamp.Light != null)
            {
                lamp.Light.enabled = true;
                if (!lit)
                    lamp.Light.intensity = 0f;
                else
                    RestoreAuthoredLight(lamp);
            }
            if (lamp.FlameRenderer != null)
                lamp.FlameRenderer.enabled = lit;

            foreach (var particles in lamp.Particles)
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
            if (!IsLit || oilDrainPerSecond <= 0f || IsOilDrainPaused())
                return;

            oilPercent = Mathf.Max(0f, oilPercent - oilDrainPerSecond * Time.deltaTime);
            if (oilPercent <= 0f)
                SetLitInternal(false, true, true);
        }

        private bool IsOilDrainPaused()
        {
            return !drainOilDuringCutscene && IsCutsceneLampState();
        }

        private void UpdateOilUi()
        {
            ResolveOilUi();
            if (oilFillImage != null)
            {
                oilFillImage.color = oilFillColor;
                oilFillImage.fillAmount = Mathf.Clamp01(oilPercent / 100f);
            }

            if (oilPercentText != null)
                oilPercentText.text = $"{Mathf.CeilToInt(oilPercent)}%";
        }

        private void ResolveOilUi()
        {
            if (oilFillImage == null)
            {
                var fillObject = GameObject.Find("LanternFuelFill");
                if (fillObject != null)
                    oilFillImage = fillObject.GetComponent<Image>();
            }

            if (oilPercentText == null)
            {
                var textObject = GameObject.Find("BatteryPercentText");
                if (textObject != null)
                    oilPercentText = textObject.GetComponent<TMP_Text>();
            }
        }

        private void ApplyNearMonsterColorEffect(LampRuntime lamp, float danger)
        {
            if (!IsLit || lamp == null)
                return;

            if (danger <= 0.001f)
            {
                RestoreAuthoredColors(lamp);
                return;
            }

            if (lamp.Light != null)
                lamp.Light.color = Color.Lerp(lamp.AuthoredColor, dangerFlameColor, danger * dangerColorBlend);

            if (lamp.FlameMaterial != null)
                lamp.FlameMaterial.color = Color.Lerp(lamp.AuthoredMaterialColor, dangerFlameColor, danger * dangerColorBlend);

            ApplyParticleDangerColor(lamp, danger);
        }

        private static void RestoreAuthoredLight(LampRuntime lamp)
        {
            if (lamp == null || lamp.Light == null)
                return;

            lamp.Light.enabled = true;
            lamp.Light.intensity = lamp.AuthoredIntensity;
            lamp.Light.range = lamp.AuthoredRange;
            lamp.Light.color = lamp.AuthoredColor;

            if (lamp.FlameMaterial != null)
                lamp.FlameMaterial.color = lamp.AuthoredMaterialColor;

            RestoreAuthoredParticleColors(lamp);
        }

        private static void RestoreAuthoredColors(LampRuntime lamp)
        {
            if (lamp == null)
                return;

            if (lamp.Light != null)
                lamp.Light.color = lamp.AuthoredColor;

            if (lamp.FlameMaterial != null)
                lamp.FlameMaterial.color = lamp.AuthoredMaterialColor;

            RestoreAuthoredParticleColors(lamp);
        }

        private void ApplyNearGhostReaction(LampRuntime lamp, float danger)
        {
            if (lamp == null || lamp.Root == null)
                return;

            if (danger <= 0.001f)
            {
                lamp.Root.localPosition = Vector3.Lerp(lamp.Root.localPosition, lamp.BaseLocalPosition, Time.deltaTime * 12f);
                lamp.Root.localRotation = Quaternion.Slerp(lamp.Root.localRotation, lamp.BaseLocalRotation, Time.deltaTime * 12f);
                RestoreFlameSway(lamp);
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

            lamp.Root.localPosition = lamp.BaseLocalPosition + shakeOffset;
            lamp.Root.localRotation = lamp.BaseLocalRotation * shakeRotation;
            ApplyFlameSway(lamp, danger);
        }

        private void CaptureRuntime(
            LampRuntime runtime,
            Transform root,
            Transform flameSwayRoot,
            Light light,
            Renderer flameRenderer,
            ParticleSystem[] particles)
        {
            runtime.Root = root;
            runtime.Light = light;
            runtime.FlameRenderer = flameRenderer;
            runtime.Particles = particles ?? Array.Empty<ParticleSystem>();
            runtime.AuthoredParticleColors = CaptureParticleColors(runtime.Particles);
            runtime.FlameMaterial = flameRenderer != null ? flameRenderer.material : null;
            runtime.BaseLocalPosition = root != null ? root.localPosition : Vector3.zero;
            runtime.BaseLocalRotation = root != null ? root.localRotation : Quaternion.identity;
            runtime.FlameSwayRoot = flameSwayRoot;
            runtime.BaseFlameSwayLocalPosition = flameSwayRoot != null ? flameSwayRoot.localPosition : Vector3.zero;
            runtime.BaseFlameSwayLocalRotation = flameSwayRoot != null ? flameSwayRoot.localRotation : Quaternion.identity;
            runtime.AuthoredIntensity = light != null ? light.intensity : 0f;
            runtime.AuthoredRange = light != null ? light.range : 0f;
            runtime.AuthoredColor = light != null ? light.color : Color.white;
            runtime.AuthoredMaterialColor = runtime.FlameMaterial != null ? runtime.FlameMaterial.color : Color.white;

            if (light != null)
                light.enabled = true;
        }

        private void ApplyParticleDangerColor(LampRuntime lamp, float danger)
        {
            var particles = lamp.Particles;
            if (particles == null)
                return;

            var blueStart = new Color(0.5f, 0.95f, 1f, 0.98f);
            var blueEnd = new Color(0.08f, 0.42f, 1f, 0.8f);

            var particleDanger = new ParticleSystem.MinMaxGradient(blueStart, blueEnd);
            foreach (var particlesInstance in particles)
            {
                if (particlesInstance == null)
                    continue;

                var index = Array.IndexOf(particles, particlesInstance);
                var authoredColor = index >= 0 && index < lamp.AuthoredParticleColors.Length
                    ? lamp.AuthoredParticleColors[index]
                    : particlesInstance.main.startColor;
                var main = particlesInstance.main;
                main.startColor = LerpGradient(authoredColor, particleDanger, danger * dangerColorBlend);
            }
        }

        private void ApplyFlameSway(LampRuntime lamp, float danger)
        {
            if (lamp.FlameSwayRoot == null)
                return;

            var swaySpeed = Mathf.Lerp(10f, 22f, danger);
            var positionAmount = dangerFlameSwayPosition * danger;
            var rotationAmount = dangerFlameSwayRotation * danger;
            var swayOffset = new Vector3(
                Mathf.Sin(Time.time * swaySpeed) * positionAmount,
                Mathf.Sin(Time.time * swaySpeed * 1.37f) * positionAmount * 0.45f,
                Mathf.Cos(Time.time * swaySpeed * 0.83f) * positionAmount);
            var swayRotation = Quaternion.Euler(
                Mathf.Sin(Time.time * swaySpeed * 0.91f) * rotationAmount,
                0f,
                Mathf.Cos(Time.time * swaySpeed * 1.13f) * rotationAmount);

            lamp.FlameSwayRoot.localPosition = lamp.BaseFlameSwayLocalPosition + swayOffset;
            lamp.FlameSwayRoot.localRotation = lamp.BaseFlameSwayLocalRotation * swayRotation;
        }

        private static void RestoreFlameSway(LampRuntime lamp)
        {
            if (lamp.FlameSwayRoot == null)
                return;

            lamp.FlameSwayRoot.localPosition = Vector3.Lerp(lamp.FlameSwayRoot.localPosition, lamp.BaseFlameSwayLocalPosition, Time.deltaTime * 12f);
            lamp.FlameSwayRoot.localRotation = Quaternion.Slerp(lamp.FlameSwayRoot.localRotation, lamp.BaseFlameSwayLocalRotation, Time.deltaTime * 12f);
        }

        private static ParticleSystem.MinMaxGradient[] CaptureParticleColors(ParticleSystem[] particles)
        {
            if (particles == null || particles.Length == 0)
                return Array.Empty<ParticleSystem.MinMaxGradient>();

            var colors = new ParticleSystem.MinMaxGradient[particles.Length];
            for (var i = 0; i < particles.Length; i++)
                colors[i] = particles[i] != null ? particles[i].main.startColor : default;

            return colors;
        }

        private static void RestoreAuthoredParticleColors(LampRuntime lamp)
        {
            if (lamp == null || lamp.Particles == null)
                return;

            for (var i = 0; i < lamp.Particles.Length; i++)
            {
                var particles = lamp.Particles[i];
                if (particles == null || i >= lamp.AuthoredParticleColors.Length)
                    continue;

                var main = particles.main;
                main.startColor = lamp.AuthoredParticleColors[i];
            }
        }

        private static ParticleSystem.MinMaxGradient LerpGradient(
            ParticleSystem.MinMaxGradient from,
            ParticleSystem.MinMaxGradient to,
            float t)
        {
            t = Mathf.Clamp01(t);
            return from.mode switch
            {
                ParticleSystemGradientMode.Color => new ParticleSystem.MinMaxGradient(Color.Lerp(from.color, to.color, t)),
                ParticleSystemGradientMode.TwoColors => new ParticleSystem.MinMaxGradient(
                    Color.Lerp(from.colorMin, to.colorMin, t),
                    Color.Lerp(from.colorMax, to.colorMax, t)),
                _ => new ParticleSystem.MinMaxGradient(Color.Lerp(from.colorMax, to.colorMax, t))
            };
        }

        private float GetNearGhostEffect01()
        {
            ResolveDangerTarget();
            if (dangerTarget == null)
                return 0f;

            var distance = DistanceToDangerTarget();
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

            if (DistanceToDangerTarget() > dangerDistance)
            {
                unlitNearGhostTimer = 0f;
                return;
            }

            unlitNearGhostTimer += Time.deltaTime;
            if (unlitNearGhostTimer >= directAttackSeconds)
            {
                unlitNearGhostTimer = -999f;
                ghost.ForceChase();
                ShowLampMessage("Bóng tối không che được lâu nữa.");
            }
        }

        private void ResolveReferences()
        {
            if (gameplayLanternRoot == null)
                gameplayLanternRoot = FindLantern("Lantern_01_2k", "FollowCamera");

            if (gameplayFlameSwayRoot == null && gameplayLanternRoot != null)
            {
                var fireFx = gameplayLanternRoot.Find("P2_Lantern_FireFX");
                gameplayFlameSwayRoot = fireFx != null
                    ? fireFx
                    : gameplayFlameParticles != null && gameplayFlameParticles.Length > 0 && gameplayFlameParticles[0] != null
                        ? gameplayFlameParticles[0].transform
                        : null;
            }

            if (gameplayFlameLight == null)
                gameplayFlameLight = FindFlameLight(gameplayLanternRoot);

            if (gameplayFlameParticles == null || gameplayFlameParticles.Length == 0)
                gameplayFlameParticles = gameplayLanternRoot != null ? gameplayLanternRoot.GetComponentsInChildren<ParticleSystem>(true) : Array.Empty<ParticleSystem>();
        }

        private void ResolveDangerTarget()
        {
            if (dangerTarget != null)
                return;

            if (ghost != null)
            {
                dangerTarget = ghost.transform;
                return;
            }

            var p2Ghost = FindFirstObjectByType<P2GhostController>(FindObjectsInactive.Include);
            if (p2Ghost != null)
            {
                ghost = p2Ghost;
                dangerTarget = p2Ghost.transform;
                return;
            }

            var p1Monster = FindFirstObjectByType<global::MonsterAI>(FindObjectsInactive.Include);
            if (p1Monster != null)
                dangerTarget = p1Monster.transform;
        }

        private float DistanceToDangerTarget()
        {
            ResolveDangerTarget();
            if (dangerTarget == null)
                return 999f;

            var controller = P2GameController.Instance;
            if (controller != null)
                return controller.DistanceToPlayer(dangerTarget.position);

            var player = FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
            var playerPosition = player != null ? player.transform.position : transform.position;
            return Vector3.Distance(playerPosition, dangerTarget.position);
        }

        private void ShowLampMessage(string message)
        {
            if (P2GameController.Instance != null)
            {
                P2GameController.Instance.ShowPrompt(message);
                return;
            }

            FpsHorrorKit.InteractMessageScript.Instance?.ShowMessage(message);
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

            var target = dangerTarget != null ? dangerTarget : ghost != null ? ghost.transform : null;
            if (target != null)
            {
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.25f);
                Gizmos.DrawLine(origin, target.position);
            }
        }

        private static Transform FindLantern(string lanternName, string requiredParentName)
        {
            foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.name != lanternName)
                    continue;

                if (string.IsNullOrWhiteSpace(requiredParentName))
                    return transform;

                var parent = transform.parent;
                while (parent != null)
                {
                    if (parent.name == requiredParentName)
                        return transform;

                    parent = parent.parent;
                }
            }

            return null;
        }

        private static Light FindFlameLight(Transform lantern)
        {
            if (lantern == null)
                return null;

            var flame = lantern.Find("P2_Lantern_FireFX/P2_Lantern_FlameLight");
            return flame != null ? flame.GetComponent<Light>() : lantern.GetComponentInChildren<Light>(true);
        }
    }
}
