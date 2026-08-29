using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class PhysicalPianoController : MonoBehaviour
    {
        private const string ScenePianoName = "Prop_Piano_FullKeys";
        private const float MissingRendererLabelSize = 0.05f;

        private static readonly NoteBinding[] NoteBindings =
        {
            new("C", "Key_Do"),
            new("D", "Key_Re"),
            new("E", "Key_Mi"),
            new("F", "Key_Fa"),
            new("G", "Key_Sol"),
            new("A", "Key_La"),
            new("B", "Key_Si")
        };

        [Header("Input")]
        [SerializeField] private Key previousKey = Key.A;
        [SerializeField] private Key nextKey = Key.D;
        [SerializeField] private Key playKey = Key.E;
        [SerializeField] private Key exitKey = Key.Escape;

        [Header("Visuals")]
        [SerializeField] private Material playableKeyMaterial;
        [SerializeField] private Material selectedKeyMaterial;
        [SerializeField] private Material whiteKeyMaterial;

        [Header("Key Press")]
        [SerializeField] private Vector3 pressLocalOffset = new(0f, -0.018f, 0f);
        [SerializeField] private float pressDownTime = 0.045f;
        [SerializeField] private float pressReturnTime = 0.085f;

        [Header("Interaction")]
        [SerializeField] private bool installRuntimeInteractable = true;
        [SerializeField] private bool disableInteractionAfterCompleted = true;
        [SerializeField] private string startMessage = "A/D \u0111\u1ed5i ph\u00edm, E \u0111\u00e1nh, ESC tho\u00e1t.";
        [SerializeField] private string missingKeyMessage = "Kh\u00f4ng t\u00ecm th\u1ea5y \u0111\u1ee7 ph\u00edm piano.";

        [Header("Camera Focus")]
        [SerializeField] private bool focusCameraOnKeys = true;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private string cameraTargetName = "camTaget";
        [SerializeField] private float cameraDistance = 0.82f;
        [SerializeField] private float cameraHeight = 0.38f;
        [SerializeField] private float cameraLookHeight = 0.015f;
        [SerializeField] private float cameraMoveSpeed = 8.5f;
        [SerializeField] private float cameraFocusFov = 42f;
        [SerializeField] private float cameraArriveDistance = 0.025f;
        [SerializeField] private float cameraArriveAngle = 1.2f;

        [Header("Guide UI")]
        [SerializeField] private bool showGuideText = true;
        [SerializeField] private string guideText = "A / D - \u0110\u1ed5i ph\u00edm    |    E - \u0110\u00e1nh n\u1ed1t    |    ESC - Tho\u00e1t";
        [SerializeField] private int guideFontSize = 30;
        [SerializeField] private Color guideTextColor = new(0.86f, 0.96f, 1f, 1f);
        [SerializeField] private Color guideBackgroundColor = new(0f, 0.006f, 0.012f, 0.72f);

        [Header("Fallback Audio")]
        [SerializeField] private AudioClip noteClip;
        [SerializeField] private AudioClip noteClipC;
        [SerializeField] private AudioClip noteClipD;
        [SerializeField] private AudioClip noteClipE;
        [SerializeField] private AudioClip noteClipF;
        [SerializeField] private AudioClip noteClipG;
        [SerializeField] private AudioClip noteClipA;
        [SerializeField] private AudioClip noteClipB;

        private readonly List<PianoKeyState> keys = new();
        private AudioSource audioSource;
        private bool active;
        private bool completed;
        private bool subscribedToPuzzle;
        private bool cameraReachedTarget;
        private int selectedIndex;
        private int activatedFrame = -1;
        private Camera focusCamera;
        private Transform focusCameraTransform;
        private Transform originalCameraParent;
        private Vector3 originalCameraLocalPosition;
        private Quaternion originalCameraLocalRotation;
        private float originalCameraFov;
        private Behaviour cameraBrain;
        private bool cameraBrainWasEnabled;
        private bool cameraFocusActive;
        private GameObject guideRoot;
        private Material runtimeSelectedKeyMaterial;

        public static bool IsAnyActive => ActiveInstance != null && ActiveInstance.active;
        private static PhysicalPianoController ActiveInstance { get; set; }
        public bool IsActive => active;
        public bool IsCompleted => completed || IsProgressCompleted();

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            ConfigureAudioSource();
            EnsureRuntimeInteractable();
#if UNITY_EDITOR
            AutoAssignMissingAssetsInEditor();
#endif
            BindKeys();
            ApplyWhiteMaterialToAllKeys();
        }

        private void OnEnable()
        {
            SubscribeToPuzzle();
        }

        private void Start()
        {
            SubscribeToPuzzle();
            if (IsProgressCompleted())
            {
                completed = true;
                DisableCompletedInteractionTargets();
            }

            if (!active)
                ApplyWhiteMaterialToAllKeys();
        }

        private void OnDisable()
        {
            if (subscribedToPuzzle && PianoPuzzle.Instance != null)
            {
                PianoPuzzle.Instance.OnPianoCompleted -= HandlePianoCompleted;
                PianoPuzzle.Instance.OnPianoFailed -= HandlePianoFailed;
            }
            subscribedToPuzzle = false;

            if (active)
                Deactivate(false);
        }

        private void OnDestroy()
        {
            if (runtimeSelectedKeyMaterial != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(runtimeSelectedKeyMaterial);
                else
#endif
                Destroy(runtimeSelectedKeyMaterial);
            }
        }

        private void Update()
        {
            UpdateCameraFocus(false);

            if (!active)
                return;

            if (GameController.IsCutsceneOrEndInputLocked())
            {
                Deactivate(true);
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || Time.frameCount == activatedFrame)
                return;

            if (WasPressed(keyboard, exitKey))
            {
                Deactivate(true);
                return;
            }

            if (WasPressed(keyboard, previousKey))
                SelectKey(selectedIndex - 1);

            if (WasPressed(keyboard, nextKey))
                SelectKey(selectedIndex + 1);

            if (WasPressed(keyboard, playKey))
                PlaySelectedKey();
        }

        public bool ActivateFromPianoInteract()
        {
            if (IsCompleted)
            {
                completed = true;
                DisableCompletedInteractionTargets();
                return true;
            }

            BindKeys();
            if (keys.Count < NoteBindings.Length)
            {
                InteractMessageScript.Instance?.ShowMessage(missingKeyMessage);
                return false;
            }

            active = true;
            ActiveInstance = this;
            SubscribeToPuzzle();
            AudioManager.Instance?.DisableGameplayAmbienceAfterPiano();
            activatedFrame = Time.frameCount;
            cameraReachedTarget = false;
            selectedIndex = 0;
            PianoPuzzle.Instance?.ResetPuzzle();
            SetLabelsVisible(false);
            SelectKey(selectedIndex);
            BlockGameplayForPiano(true);
            BeginCameraFocus();
            SetGuideVisible(true);

            if (!showGuideText && !string.IsNullOrWhiteSpace(startMessage))
                InteractMessageScript.Instance?.ShowMessage(startMessage);

            return true;
        }

        public static bool CloseActivePiano()
        {
            if (ActiveInstance == null || !ActiveInstance.active)
                return false;

            ActiveInstance.Deactivate(true);
            return true;
        }

        private void SubscribeToPuzzle()
        {
            if (subscribedToPuzzle || PianoPuzzle.Instance == null)
                return;

            PianoPuzzle.Instance.OnPianoCompleted += HandlePianoCompleted;
            PianoPuzzle.Instance.OnPianoFailed += HandlePianoFailed;
            subscribedToPuzzle = true;
        }

        public static PhysicalPianoController FindScenePiano()
        {
            foreach (var controller in FindObjectsByType<PhysicalPianoController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (controller != null && controller.gameObject.scene.IsValid())
                    return controller;
            }

            var piano = FindSceneTransform(ScenePianoName);
            if (piano == null)
                return null;

            return piano.GetComponent<PhysicalPianoController>() ?? piano.gameObject.AddComponent<PhysicalPianoController>();
        }

        private void PlaySelectedKey()
        {
            if (!cameraReachedTarget || selectedIndex < 0 || selectedIndex >= keys.Count)
                return;

            AudioManager.Instance?.SetGameplayAmbienceSuppressed(true);
            AudioManager.Instance?.DisableGameplayAmbienceAfterPiano();
            AudioManager.Instance?.BlockGameplayAmbience(3f);
            var key = keys[selectedIndex];
            StartKeyPress(key);
            PlayNoteAudio(key.Note);
            PianoPuzzle.Instance?.PlayNote(key.Note);
        }

        private void SelectKey(int index)
        {
            if (keys.Count == 0)
                return;

            selectedIndex = Mathf.Clamp(index, 0, keys.Count - 1);
            RefreshKeyMaterials();
        }

        private void BindKeys()
        {
            keys.Clear();

            foreach (var binding in NoteBindings)
            {
                var keyTransform = FindChildTransform(transform, binding.ObjectName);
                if (keyTransform == null)
                    continue;

                var state = new PianoKeyState(binding.Note, keyTransform);
                EnsureLabel(state);
                keys.Add(state);
            }

            SetLabelsVisible(active && cameraReachedTarget);
        }

        private void EnsureLabel(PianoKeyState key)
        {
            if (key == null || key.Transform == null)
                return;

            string labelName = $"PianoNoteLabel_{key.Note}";
            var existing = key.Transform.Find(labelName);
            if (existing == null)
                return;

            key.LabelCanvas = existing.GetComponent<Canvas>() ?? existing.GetComponentInChildren<Canvas>(true);
            key.Label = existing.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void StartKeyPress(PianoKeyState key)
        {
            if (key.PressRoutine != null)
                StopCoroutine(key.PressRoutine);

            key.Transform.localPosition = key.OriginalLocalPosition;
            key.PressRoutine = StartCoroutine(PressKeyRoutine(key));
        }

        private IEnumerator PressKeyRoutine(PianoKeyState key)
        {
            Vector3 from = key.OriginalLocalPosition;
            Vector3 down = from + pressLocalOffset;
            yield return MoveKey(key.Transform, from, down, pressDownTime);
            yield return MoveKey(key.Transform, down, from, pressReturnTime);
            key.Transform.localPosition = from;
            key.PressRoutine = null;
        }

        private static IEnumerator MoveKey(Transform keyTransform, Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                keyTransform.localPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                keyTransform.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            keyTransform.localPosition = to;
        }

        private void PlayNoteAudio(string note)
        {
            if (PianoPuzzleUI.Instance != null)
            {
                PianoPuzzleUI.Instance.PlayPhysicalNoteAudio(note);
                return;
            }

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            ConfigureAudioSource();
            var clip = ClipForNote(note);
            if (clip == null)
                return;

            bool fallback = clip == noteClip && ClipForNoteWithoutFallback(note) == null;
            audioSource.pitch = fallback ? PianoPuzzleUI.FallbackPitchForNote(note) : 1f;
            audioSource.PlayOneShot(clip);
        }

        private AudioClip ClipForNote(string note)
        {
            return ClipForNoteWithoutFallback(note) ?? noteClip;
        }

        private AudioClip ClipForNoteWithoutFallback(string note)
        {
            return note switch
            {
                "C" => noteClipC,
                "D" => noteClipD,
                "E" => noteClipE,
                "F" => noteClipF,
                "G" => noteClipG,
                "A" => noteClipA,
                "B" => noteClipB,
                _ => null
            };
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
                return;

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
        }

        private void HandlePianoCompleted()
        {
            completed = true;
            if (active)
                Deactivate(false);

            DisableCompletedInteractionTargets();
        }

        private void HandlePianoFailed()
        {
            if (active)
                Deactivate(false);
        }

        private void Deactivate(bool resetPuzzle)
        {
            active = false;
            if (ActiveInstance == this)
                ActiveInstance = null;

            if (resetPuzzle)
                PianoPuzzle.Instance?.ResetPuzzle();

            foreach (var key in keys)
            {
                if (key.PressRoutine != null)
                {
                    StopCoroutine(key.PressRoutine);
                    key.PressRoutine = null;
                }

                if (key.Transform != null)
                    key.Transform.localPosition = key.OriginalLocalPosition;
            }

            ApplyWhiteMaterialToAllKeys();
            SetLabelsVisible(false);
            EndCameraFocus();
            SetGuideVisible(false);
            AudioManager.Instance?.DisableGameplayAmbienceAfterPiano();
            BlockGameplayForPiano(false);
        }

        private void BeginCameraFocus()
        {
            if (!focusCameraOnKeys)
            {
                cameraReachedTarget = true;
                RefreshKeyMaterials();
                SetLabelsVisible(true);
                return;
            }

            focusCamera = Camera.main;
            if (focusCamera == null)
            {
                cameraReachedTarget = true;
                RefreshKeyMaterials();
                SetLabelsVisible(true);
                return;
            }

            focusCameraTransform = focusCamera.transform;
            if (focusCameraTransform == null)
            {
                cameraReachedTarget = true;
                RefreshKeyMaterials();
                SetLabelsVisible(true);
                return;
            }

            ResolveCameraTarget();

            originalCameraParent = focusCameraTransform.parent;
            originalCameraLocalPosition = focusCameraTransform.localPosition;
            originalCameraLocalRotation = focusCameraTransform.localRotation;
            originalCameraFov = focusCamera.fieldOfView;

            cameraBrain = focusCamera.GetComponent("CinemachineBrain") as Behaviour;
            cameraBrainWasEnabled = cameraBrain != null && cameraBrain.enabled;
            if (cameraBrain != null)
                cameraBrain.enabled = false;

            focusCameraTransform.SetParent(null, true);
            cameraFocusActive = true;
            UpdateCameraFocus(false);
        }

        private void EndCameraFocus()
        {
            if (!cameraFocusActive)
                return;

            if (focusCameraTransform != null)
            {
                focusCameraTransform.SetParent(originalCameraParent, false);
                focusCameraTransform.localPosition = originalCameraLocalPosition;
                focusCameraTransform.localRotation = originalCameraLocalRotation;
            }

            if (focusCamera != null)
                focusCamera.fieldOfView = originalCameraFov;

            if (cameraBrain != null)
                cameraBrain.enabled = cameraBrainWasEnabled;

            focusCamera = null;
            focusCameraTransform = null;
            originalCameraParent = null;
            cameraBrain = null;
            cameraFocusActive = false;
        }

        private void UpdateCameraFocus(bool instant)
        {
            if (!cameraFocusActive || focusCameraTransform == null || focusCamera == null)
                return;

            if (!TryGetSelectedKeyCameraPose(out var targetPosition, out var targetRotation))
                return;

            float t = instant ? 1f : 1f - Mathf.Exp(-cameraMoveSpeed * Time.deltaTime);
            focusCameraTransform.position = Vector3.Lerp(focusCameraTransform.position, targetPosition, t);
            focusCameraTransform.rotation = Quaternion.Slerp(focusCameraTransform.rotation, targetRotation, t);
            focusCamera.fieldOfView = Mathf.Lerp(focusCamera.fieldOfView, cameraFocusFov, t);

            bool reached = Vector3.Distance(focusCameraTransform.position, targetPosition) <= cameraArriveDistance
                && Quaternion.Angle(focusCameraTransform.rotation, targetRotation) <= cameraArriveAngle;
            if (cameraReachedTarget != reached)
            {
                cameraReachedTarget = reached;
                RefreshKeyMaterials();
                SetLabelsVisible(reached);
            }
        }

        private bool TryGetSelectedKeyCameraPose(out Vector3 targetPosition, out Quaternion targetRotation)
        {
            targetPosition = default;
            targetRotation = default;

            ResolveCameraTarget();
            if (cameraTarget != null)
            {
                targetPosition = cameraTarget.position;
                targetRotation = cameraTarget.rotation;
                return true;
            }

            if (selectedIndex < 0 || selectedIndex >= keys.Count || keys[selectedIndex].Transform == null)
                return false;

            var key = keys[selectedIndex];
            var bounds = GetWorldBounds(key.Renderers, key.Transform.position);
            Vector3 lookAt = bounds.center + Vector3.up * cameraLookHeight;
            Vector3 viewDirection = focusCameraTransform.position - lookAt;

            if (viewDirection.sqrMagnitude < 0.01f)
                viewDirection = -transform.forward;

            viewDirection.y = 0f;
            if (viewDirection.sqrMagnitude < 0.01f)
                viewDirection = -transform.forward;

            targetPosition = lookAt + viewDirection.normalized * cameraDistance + Vector3.up * cameraHeight;
            Vector3 lookDirection = lookAt - targetPosition;
            if (lookDirection.sqrMagnitude < 0.0001f)
                return false;

            targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            return true;
        }

        private void ResolveCameraTarget()
        {
            if (cameraTarget != null)
                return;

            if (!string.IsNullOrWhiteSpace(cameraTargetName))
                cameraTarget = FindChildTransform(transform, cameraTargetName);

            if (cameraTarget == null)
                cameraTarget = FindChildTransform(transform, "camTarget");
        }

        private void BlockGameplayForPiano(bool blocked)
        {
            if (blocked)
                AudioManager.Instance?.SetGameplayAmbienceSuppressed(true);
            if (blocked)
                AudioManager.Instance?.SetBackgroundDuck(0f);
            else
                AudioManager.Instance?.ClearBackgroundDuck();

            if (PlayerInteract.Instance != null)
                PlayerInteract.Instance.sendRaycast = !blocked;

            if (GameController.Instance == null)
                return;

            if (blocked)
            {
                GameController.Instance.SetGameState(GameController.GameState.Puzzle);
            }
            else if (GameController.Instance.currentGameState == GameController.GameState.Puzzle
                && !(InventoryUI.Instance != null && InventoryUI.Instance.IsOpen))
            {
                GameController.Instance.SetGameState(GameController.GameState.Gameplay);
            }
        }

        private void SetLabelsVisible(bool visible)
        {
            foreach (var key in keys)
            {
                if (key.LabelCanvas != null)
                    key.LabelCanvas.gameObject.SetActive(visible);
            }
        }

        private void SetGuideVisible(bool visible)
        {
            if (visible)
                EnsureGuideUI();

            if (guideRoot != null)
                guideRoot.SetActive(visible && showGuideText);
        }

        private void EnsureGuideUI()
        {
            if (!showGuideText || guideRoot != null)
                return;

            var canvas = FindPianoGuideCanvas();
            if (canvas == null)
                return;

            guideRoot = new GameObject("PhysicalPianoGuide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = guideRoot.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = new Vector2(0.24f, 0.035f);
            rect.anchorMax = new Vector2(0.76f, 0.105f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = guideRoot.GetComponent<Image>();
            background.color = guideBackgroundColor;
            background.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(guideRoot.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 2f);
            textRect.offsetMax = new Vector2(-14f, -2f);

            var guideLabel = textObject.GetComponent<TextMeshProUGUI>();
            guideLabel.text = guideText;
            guideLabel.fontSize = guideFontSize;
            guideLabel.color = guideTextColor;
            guideLabel.alignment = TextAlignmentOptions.Center;
            guideLabel.textWrappingMode = TextWrappingModes.NoWrap;
            guideLabel.raycastTarget = false;
        }

        private static Canvas FindPianoGuideCanvas()
        {
            var gameUi = GameController.Instance != null ? GameController.Instance.gameUI : null;
            if (gameUi != null)
            {
                var uiCanvas = gameUi.GetComponentInParent<Canvas>() ?? gameUi.GetComponentInChildren<Canvas>(true);
                if (uiCanvas != null)
                    return uiCanvas;
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                return canvas;

            var canvasObject = new GameObject("PhysicalPianoGuideCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32050;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private void RefreshKeyMaterials()
        {
            for (int i = 0; i < keys.Count; i++)
            {
                var material = whiteKeyMaterial;
                if (active && cameraReachedTarget)
                    material = i == selectedIndex ? GetSelectedKeyMaterial() : playableKeyMaterial;

                ApplyKeyMaterial(keys[i], material);
            }
        }

        private void ApplyWhiteMaterialToAllKeys()
        {
            foreach (var key in keys)
                ApplyKeyMaterial(key, whiteKeyMaterial);
        }

        private void ApplyKeyMaterial(PianoKeyState key, Material material)
        {
            if (key == null || key.Renderers == null || material == null)
                return;

            foreach (var renderer in key.Renderers)
            {
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private Material GetSelectedKeyMaterial()
        {
            if (selectedKeyMaterial != null)
                return selectedKeyMaterial;

            if (runtimeSelectedKeyMaterial != null)
                return runtimeSelectedKeyMaterial;

            var source = playableKeyMaterial != null ? playableKeyMaterial : whiteKeyMaterial;
            if (source == null)
                return null;

            runtimeSelectedKeyMaterial = new Material(source)
            {
                name = "Mat_PianoKey_Selected_Runtime"
            };
            ApplyHighlightColors(runtimeSelectedKeyMaterial, new Color(1f, 0.82f, 0.16f, 1f), new Color(1f, 0.55f, 0.03f, 1f));
            return runtimeSelectedKeyMaterial;
        }

        private static void ApplyHighlightColors(Material material, Color baseColor, Color emissiveColor)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);
            if (material.HasProperty("_EmissiveColor"))
                material.SetColor("_EmissiveColor", emissiveColor);
            if (material.HasProperty("_EmissiveColorLDR"))
                material.SetColor("_EmissiveColorLDR", emissiveColor);
            if (material.HasProperty("_UseEmissiveIntensity"))
                material.SetFloat("_UseEmissiveIntensity", 1f);
            if (material.HasProperty("_EmissiveIntensity"))
                material.SetFloat("_EmissiveIntensity", 1.15f);
        }

        private void EnsureRuntimeInteractable()
        {
            if (!installRuntimeInteractable)
                return;

            if (GetComponent<PianoInteractable>() == null)
                gameObject.AddComponent<PianoInteractable>();

            EnsurePlayableCollider(gameObject);
        }

        private static void EnsurePlayableCollider(GameObject pianoObject)
        {
            if (pianoObject.GetComponent<Collider>() != null)
                return;

            var collider = pianoObject.AddComponent<BoxCollider>();
            if (TryGetLocalRendererBounds(pianoObject.transform, out var bounds))
            {
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
        }

        private void DisableCompletedInteractionTargets()
        {
            if (!disableInteractionAfterCompleted)
                return;

            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider != null)
                    collider.enabled = false;
            }
        }

        private static bool IsProgressCompleted()
        {
            return GameProgressManager.Instance != null
                && GameProgressManager.Instance.CurrentProgress >= GameProgress.PianoCompleted;
        }

        private static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
        {
            bool hasBounds = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                var worldBounds = renderer.bounds;
                Vector3[] corners =
                {
                    new(worldBounds.min.x, worldBounds.min.y, worldBounds.min.z),
                    new(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z),
                    new(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z),
                    new(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z),
                    new(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z),
                    new(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z),
                    new(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z),
                    new(worldBounds.max.x, worldBounds.max.y, worldBounds.max.z)
                };

                foreach (var corner in corners)
                {
                    var local = root.InverseTransformPoint(corner);
                    if (!hasBounds)
                    {
                        min = local;
                        max = local;
                        hasBounds = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }
            }

            if (!hasBounds)
            {
                bounds = default;
                return false;
            }

            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static Bounds GetWorldBounds(Renderer[] renderers)
        {
            return GetWorldBounds(renderers, Vector3.zero);
        }

        private static Bounds GetWorldBounds(Renderer[] renderers, Vector3 fallbackCenter)
        {
            if (renderers == null || renderers.Length == 0)
                return new Bounds(fallbackCenter, Vector3.one * MissingRendererLabelSize);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            return keyboard[key].wasPressedThisFrame;
        }

        private static Transform FindChildTransform(Transform root, string objectName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child;
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.name == objectName && transform.gameObject.scene.IsValid())
                    return transform;
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneInstaller()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureScenePianoInstalled();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureScenePianoInstalled();
        }

        private static void EnsureScenePianoInstalled()
        {
            var piano = FindSceneTransform(ScenePianoName);
            if (piano == null)
                return;

            if (piano.GetComponent<PhysicalPianoController>() == null)
                piano.gameObject.AddComponent<PhysicalPianoController>();
            if (piano.GetComponent<PianoInteractable>() == null)
                piano.gameObject.AddComponent<PianoInteractable>();

            EnsurePlayableCollider(piano.gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoAssignMissingAssetsInEditor();
        }

        private void AutoAssignMissingAssetsInEditor()
        {
            playableKeyMaterial ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/MainGame/Mesh/Mat_PianoKey_Playable.mat");
            selectedKeyMaterial ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/MainGame/Mesh/Mat_PianoKey_Selected.mat");
            whiteKeyMaterial ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/MainGame/Mesh/Mat_PianoKey_White.mat");
            noteClipC ??= LoadEditorClip("c1");
            noteClipD ??= LoadEditorClip("d1");
            noteClipE ??= LoadEditorClip("e1");
            noteClipF ??= LoadEditorClip("f1");
            noteClipG ??= LoadEditorClip("g1");
            noteClipA ??= LoadEditorClip("a1");
            noteClipB ??= LoadEditorClip("b1");
            noteClip ??= noteClipC;
        }

        private static AudioClip LoadEditorClip(string fileName)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/MainGame/Audio/wav/{fileName}.wav");
        }
#endif

        private readonly struct NoteBinding
        {
            public readonly string Note;
            public readonly string ObjectName;

            public NoteBinding(string note, string objectName)
            {
                Note = note;
                ObjectName = objectName;
            }
        }

        private sealed class PianoKeyState
        {
            public readonly string Note;
            public readonly Transform Transform;
            public readonly Vector3 OriginalLocalPosition;
            public readonly Renderer[] Renderers;
            public Canvas LabelCanvas;
            public TextMeshProUGUI Label;
            public Coroutine PressRoutine;

            public PianoKeyState(string note, Transform transform)
            {
                Note = note;
                Transform = transform;
                OriginalLocalPosition = transform.localPosition;
                Renderers = transform.GetComponentsInChildren<Renderer>(true);
            }
        }
    }
}
