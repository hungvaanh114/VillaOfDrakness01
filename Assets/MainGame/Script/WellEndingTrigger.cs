using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(SphereCollider))]
public sealed class WellEndingTrigger : MonoBehaviour
{
    private const string DefaultJumpscareAssetPath = "Assets/MainGame/UI/ma duoi gieng.png";
    private const string DefaultPlayerScreamAssetPath = "Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-DEATH-03.wav";
    private const string FallbackJumpscareResourcePath = "UI/EndingJumpscare";

    [Header("Testing")]
    [SerializeField] private bool testEnding;

    [Header("Trigger")]
    [SerializeField, Min(0.5f)] private float triggerRadius = 2.8f;
    [SerializeField] private bool armedOnStart;
    [SerializeField] private bool triggerCutsceneOnOwnCollider;

    [Header("Outtro CutScene")]
    [SerializeField] private CutSceneManager cutSceneManager;
    [SerializeField] private string outtroCutSceneId = "outtro";
    [SerializeField, Min(0f)] private float afterDoorDelay = 0.45f;

    [Header("Gramophone Drop")]
    [SerializeField] private Transform gramophoneDropPoint;
    [SerializeField] private Vector3 gramophoneDropEuler = new(0f, 35f, -8f);

    [Header("Well Light")]
    [SerializeField] private Light wellLight;
    [SerializeField] private Color wellFlashColor = new(0.35f, 0.95f, 1f, 1f);
    [SerializeField, Min(0f)] private float flashIntensity = 13000f;
    [SerializeField, Min(0f)] private float normalGlowIntensity = 4500f;
    [SerializeField, Min(0.1f)] private float flashDuration = 1.2f;

    [Header("Terrain Hole")]
    [SerializeField] private bool carveTerrainHoleOnAwake = true;
    [SerializeField] private Terrain terrainToCarve;
    [SerializeField, Min(0.1f)] private float terrainHoleRadius = 1.55f;
    [SerializeField] private Vector3 terrainHoleOffset;

    [Header("Jumpscare")]
    [SerializeField] private Texture2D jumpscareTexture;
    [SerializeField, Min(0.05f)] private float jumpscarePopDuration = 0.16f;
    [SerializeField, Min(0f)] private float jumpscareHoldDuration = 1.35f;
    [SerializeField, Min(0.05f)] private float jumpscareStartScale = 0.2f;
    [SerializeField, Min(0.05f)] private float jumpscareImpactScale = 1.08f;
    [SerializeField, Range(0f, 1f)] private float jumpscareOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float jumpscareDarkBackdropOpacity = 0.78f;
    [SerializeField] private AudioClip playerJumpscareScream;
    [SerializeField, TextArea(1, 3)] private string playerJumpscareSubtitle = "...Ai đó... giúp...";
    [SerializeField, Range(1, 3)] private int fallbackPlayerScreamVoiceIndex = 3;
    [SerializeField, Min(0f)] private float jumpscareCameraShakeDuration = 0.9f;
    [SerializeField, Range(0f, 0.25f)] private float jumpscareCameraShakePosition = 0.075f;
    [SerializeField, Range(0f, 8f)] private float jumpscareCameraShakeRotation = 2.8f;
    [SerializeField, Range(0f, 80f)] private float jumpscareScreenShakePixels = 26f;
    [SerializeField, Min(1f)] private float jumpscareShakeFrequency = 24f;

    [Header("Circular Backdrop")]
    [SerializeField, Range(0f, 1f)] private float circularBackdropBaseOpacity = 0.28f;
    [SerializeField, Range(0f, 1f)] private float circularBackdropEdgeOpacity = 0.92f;
    [SerializeField, Min(0.05f)] private float circularBackdropStartScale = 0.48f;
    [SerializeField, Min(0.05f)] private float circularBackdropEndScale = 1.18f;
    [SerializeField, Min(128)] private int circularBackdropTextureSize = 768;

    [Header("Ending Text")]
    [SerializeField, Min(1f)] private float creditsDuration = 36f;
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 1.45f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 0.8f;
    [SerializeField, Min(0f)] private float menuReturnDelay = 1.25f;
    [SerializeField] private float creditsStartY = -920f;
    [SerializeField] private float creditsEndY = 1480f;
    [TextArea(6, 18)]
    [SerializeField] private string endingLine =
        "VILLA OF DARKNESS\n\n" +
        "Nguyễn Minh Khoa dừng lại tại giếng đá.\n" +
        "Chiếc hộp nhạc đồng nằm lại trên mặt đất,\n" +
        "chờ người tiếp theo bước vào khu vườn này.\n\n" +
        "THÀNH VIÊN\n\n" +
        "Nguyễn Trường Vũ\n" +
        "Thiết kế UI Panels - Thiết kế Ambient\n\n" +
        "Võ Văn Thuận\n" +
        "Lập trình Gameplay - Hệ thống nhặt Item\n" +
        "Cơ chế Piano - Cơ chế Inventory - Hệ thống UI\n\n" +
        "Lê Phú Tuấn Anh\n" +
        "Level Design - Dialogue System\n\n" +
        "Nguyễn Hữu Phúc\n" +
        "Cơ chế sự kiện - Triggers - Hệ thống AI\n" +
        "Cơ chế Mirror - Thiết lập Animation\n\n" +
        "Nguyễn Bùi Phúc Thái\n" +
        "Biên kịch - Cốt truyện - PM\n" +
        "Lập trình Gameplay - Game Design Document\n" +
        "Hệ thống Player - Scene Setup\n\n" +
        "Bùi Thanh Tân\n" +
        "Hệ thống Audio - Hệ thống Sanity - Lập trình Gameplay\n\n" +
        "Cảm ơn bạn đã chơi.";

    private SphereCollider triggerCollider;
    private bool isArmed;
    private bool hasTriggered;
    private Texture2D circularBackdropTexture;
    private bool terrainHoleCarved;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (jumpscareTexture == null || jumpscareTexture.name == "EndingJumpscare")
            jumpscareTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultJumpscareAssetPath);
        if (playerJumpscareScream == null)
            playerJumpscareScream = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultPlayerScreamAssetPath);
        ResolveReferences();
    }
#endif

    private void Awake()
    {
        ResolveReferences();
        triggerCollider = GetComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = triggerRadius;

        if (jumpscareTexture == null)
            jumpscareTexture = Resources.Load<Texture2D>(FallbackJumpscareResourcePath);

        EnsureTerrainHole();

        if (armedOnStart)
            ActivateEndingSetup();
        else
            SetWellLightActive(false, normalGlowIntensity);
    }

    private void Update()
    {
        if (!testEnding)
            return;

        testEnding = false;
        hasTriggered = false;
        ActivateEndingSetup();
        BeginExitDoorEnding(null);
    }

    public void ActivateEndingSetup()
    {
        ResolveReferences();
        EnsureTerrainHole();
        isArmed = true;
        EnsureWellGlow();
        SetWellLightActive(true, normalGlowIntensity);
    }

    public void PlayWellFxBurst()
    {
        ActivateEndingSetup();
        StartCoroutine(FlashWellLight());
    }

    public void BeginExitDoorEnding(Transform carriedGramophone)
    {
        if (hasTriggered)
            return;

        var player = GameController.Instance != null && GameController.Instance.playerController != null
            ? GameController.Instance.playerController
            : FindFirstObjectByType<FpsHorrorKit.FpsController>();
        if (player == null)
            return;

        hasTriggered = true;
        StartCoroutine(ExitDoorEndingRoutine(carriedGramophone));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerCutsceneOnOwnCollider || !isArmed || hasTriggered)
            return;

        var player = other.GetComponentInParent<FpsHorrorKit.FpsController>();
        if (player == null)
            return;

        hasTriggered = true;
        StartCoroutine(ExitDoorEndingRoutine(null));
    }

    private IEnumerator ExitDoorEndingRoutine(Transform carriedGramophone)
    {
        ResolveReferences();
        PlayWellFxBurst();

        if (afterDoorDelay > 0f)
            yield return new WaitForSeconds(afterDoorDelay);

        var controller = GameController.Instance;
        if (controller != null)
            controller.SetGameState(GameController.GameState.Cutscene);

        if (FpsHorrorKit.PlayerInteract.Instance != null)
            FpsHorrorKit.PlayerInteract.Instance.sendRaycast = false;

        yield return PlayOuttroCutScene(controller);

        DropGramophone(carriedGramophone);
        yield return RunPostOuttroEnding();
    }

    private IEnumerator PlayOuttroCutScene(GameController controller)
    {
        if (cutSceneManager == null)
            ResolveReferences();

        if (cutSceneManager == null || string.IsNullOrWhiteSpace(outtroCutSceneId))
            yield break;

        cutSceneManager.Play(outtroCutSceneId, controller);
        yield return null;

        while (cutSceneManager != null && cutSceneManager.IsPlaying)
            yield return null;
    }

    private IEnumerator RunPostOuttroEnding()
    {
        var controller = GameController.Instance;
        if (controller != null)
        {
            controller.SetChapterPhase(GameController.ChapterPhase.Ending);
            controller.SetGameState(GameController.GameState.Ending);
            if (controller.gameUI != null)
                controller.gameUI.SetActive(false);
        }

        BuildEndingUi(
            out CanvasGroup blackGroup,
            out CanvasGroup vignetteGroup,
            out RectTransform vignetteRect,
            out RawImage jumpscareImage,
            out RectTransform jumpscareRect,
            out TextMeshProUGUI jumpscareSubtitle,
            out TextMeshProUGUI creditLabel);

        PlayWellJumpscareAudio();
        yield return PlayJumpscareImage(blackGroup, vignetteGroup, vignetteRect, jumpscareImage, jumpscareRect, jumpscareSubtitle);

        if (jumpscareImage != null)
            jumpscareImage.gameObject.SetActive(false);

        float blackAlpha = blackGroup != null ? blackGroup.alpha : 0f;
        yield return FadeBlack(blackGroup, blackAlpha, 1f, fadeToBlackDuration);

        AudioManager.Instance?.PlayDeathMusic();
        if (blackHoldDuration > 0f)
            yield return new WaitForSeconds(blackHoldDuration);

        yield return RunEndingCredits(creditLabel);

        if (menuReturnDelay > 0f)
            yield return new WaitForSeconds(menuReturnDelay);

        if (GameController.Instance != null)
            GameController.Instance.LoadMainMenu();
        else
            SceneManager.LoadScene("Menu");
    }

    private void PlayWellJumpscareAudio()
    {
        var audio = AudioManager.Instance;
        if (audio == null)
            return;

        audio.PlayWellJumpscare();
        if (playerJumpscareScream != null)
            audio.PlayVoice(playerJumpscareScream);
        else
            audio.PlayDeathVoice(fallbackPlayerScreamVoiceIndex);
    }

    private IEnumerator PlayJumpscareImage(
        CanvasGroup blackGroup,
        CanvasGroup vignetteGroup,
        RectTransform vignetteRect,
        RawImage jumpscareImage,
        RectTransform jumpscareRect,
        TextMeshProUGUI jumpscareSubtitle)
    {
        if (jumpscareImage == null || jumpscareRect == null)
            yield break;

        if (blackGroup != null)
            blackGroup.alpha = circularBackdropBaseOpacity;
        if (vignetteGroup != null)
            vignetteGroup.alpha = jumpscareDarkBackdropOpacity;
        if (vignetteRect != null)
            vignetteRect.localScale = Vector3.one * circularBackdropStartScale;

        Camera shakeCamera = Camera.main;
        Transform shakeCameraTransform = shakeCamera != null ? shakeCamera.transform : null;
        Vector3 cameraBasePosition = shakeCameraTransform != null ? shakeCameraTransform.localPosition : Vector3.zero;
        Quaternion cameraBaseRotation = shakeCameraTransform != null ? shakeCameraTransform.localRotation : Quaternion.identity;
        Behaviour cameraBrain = shakeCamera != null ? shakeCamera.GetComponent("CinemachineBrain") as Behaviour : null;
        bool cameraBrainWasEnabled = cameraBrain != null && cameraBrain.enabled;
        if (cameraBrain != null)
            cameraBrain.enabled = false;

        Vector2 jumpscareBasePosition = jumpscareRect.anchoredPosition;
        Quaternion jumpscareBaseRotation = jumpscareRect.localRotation;

        jumpscareImage.gameObject.SetActive(true);
        jumpscareImage.color = new Color(1f, 1f, 1f, 0f);
        jumpscareRect.localScale = Vector3.one * jumpscareStartScale;
        ShowJumpscareSubtitle(jumpscareSubtitle, true);

        float elapsed = 0f;
        while (elapsed < jumpscarePopDuration)
        {
            float t = Mathf.Clamp01(elapsed / jumpscarePopDuration);
            float impact = 1f - Mathf.Pow(1f - t, 3f);
            jumpscareImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, jumpscareOpacity, impact));
            jumpscareRect.localScale = Vector3.one * Mathf.Lerp(jumpscareStartScale, jumpscareImpactScale, impact);
            if (vignetteRect != null)
                vignetteRect.localScale = Vector3.one * Mathf.Lerp(circularBackdropStartScale, circularBackdropEndScale, impact);
            ApplyJumpscareShake(shakeCameraTransform, cameraBasePosition, cameraBaseRotation, jumpscareRect, jumpscareBasePosition, elapsed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        jumpscareImage.color = new Color(1f, 1f, 1f, jumpscareOpacity);
        jumpscareRect.localScale = Vector3.one;
        if (vignetteRect != null)
            vignetteRect.localScale = Vector3.one * circularBackdropEndScale;

        if (jumpscareHoldDuration > 0f)
        {
            float holdElapsed = 0f;
            while (holdElapsed < jumpscareHoldDuration)
            {
                ApplyJumpscareShake(
                    shakeCameraTransform,
                    cameraBasePosition,
                    cameraBaseRotation,
                    jumpscareRect,
                    jumpscareBasePosition,
                    elapsed + holdElapsed);
                holdElapsed += Time.deltaTime;
                yield return null;
            }
        }

        RestoreJumpscareShake(shakeCameraTransform, cameraBasePosition, cameraBaseRotation, jumpscareRect, jumpscareBasePosition, jumpscareBaseRotation);
        if (cameraBrain != null)
            cameraBrain.enabled = cameraBrainWasEnabled;
        ShowJumpscareSubtitle(jumpscareSubtitle, false);
    }

    private void ShowJumpscareSubtitle(TextMeshProUGUI subtitle, bool visible)
    {
        if (subtitle == null)
            return;

        subtitle.text = visible ? playerJumpscareSubtitle : string.Empty;
        subtitle.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(playerJumpscareSubtitle));
    }

    private void ApplyJumpscareShake(
        Transform cameraTransform,
        Vector3 cameraBasePosition,
        Quaternion cameraBaseRotation,
        RectTransform imageRect,
        Vector2 imageBasePosition,
        float elapsed)
    {
        if (jumpscareCameraShakeDuration <= 0f || elapsed >= jumpscareCameraShakeDuration)
        {
            RestoreJumpscareShake(cameraTransform, cameraBasePosition, cameraBaseRotation, imageRect, imageBasePosition, Quaternion.identity);
            return;
        }

        float life = 1f - Mathf.Clamp01(elapsed / jumpscareCameraShakeDuration);
        float phase = elapsed * jumpscareShakeFrequency;
        float x = (Mathf.PerlinNoise(phase, 8.13f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(14.71f, phase) - 0.5f) * 2f;
        float roll = Mathf.Sin(phase * Mathf.PI * 2f) * life;

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = cameraBasePosition + new Vector3(x, y, 0f) * jumpscareCameraShakePosition * life;
            cameraTransform.localRotation = cameraBaseRotation * Quaternion.Euler(
                y * jumpscareCameraShakeRotation * life,
                x * jumpscareCameraShakeRotation * life,
                roll * jumpscareCameraShakeRotation * 0.65f);
        }

        if (imageRect != null)
        {
            imageRect.anchoredPosition = imageBasePosition + new Vector2(x, y) * jumpscareScreenShakePixels * life;
            imageRect.localRotation = Quaternion.Euler(0f, 0f, roll * jumpscareCameraShakeRotation * 1.4f);
        }
    }

    private static void RestoreJumpscareShake(
        Transform cameraTransform,
        Vector3 cameraBasePosition,
        Quaternion cameraBaseRotation,
        RectTransform imageRect,
        Vector2 imageBasePosition,
        Quaternion imageBaseRotation)
    {
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = cameraBasePosition;
            cameraTransform.localRotation = cameraBaseRotation;
        }

        if (imageRect != null)
        {
            imageRect.anchoredPosition = imageBasePosition;
            imageRect.localRotation = imageBaseRotation;
        }
    }

    private static IEnumerator FadeBlack(CanvasGroup blackGroup, float from, float to, float duration)
    {
        if (blackGroup == null)
            yield break;

        if (duration <= 0f)
        {
            blackGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            blackGroup.alpha = Mathf.Lerp(from, to, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        blackGroup.alpha = to;
    }

    private IEnumerator RunEndingCredits(TextMeshProUGUI creditLabel)
    {
        if (creditLabel == null)
            yield break;

        creditLabel.gameObject.SetActive(true);
        RectTransform rect = creditLabel.rectTransform;
        rect.anchoredPosition = new Vector2(0f, creditsStartY);

        float elapsed = 0f;
        while (elapsed < creditsDuration)
        {
            float t = Mathf.Clamp01(elapsed / creditsDuration);
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(creditsStartY, creditsEndY, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        rect.anchoredPosition = new Vector2(0f, creditsEndY);
    }

    private void BuildEndingUi(
        out CanvasGroup blackGroup,
        out CanvasGroup vignetteGroup,
        out RectTransform vignetteRect,
        out RawImage jumpscareImage,
        out RectTransform jumpscareRect,
        out TextMeshProUGUI jumpscareSubtitle,
        out TextMeshProUGUI creditLabel)
    {
        var canvasObject = new GameObject("EndingRuntimeUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var black = CreateImage("EndingBlack", canvasObject.transform, Color.black);
        Stretch(black.rectTransform);
        blackGroup = black.gameObject.AddComponent<CanvasGroup>();
        blackGroup.alpha = 0f;

        var vignette = new GameObject("EndingCircularVignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter), typeof(CanvasGroup)).GetComponent<RawImage>();
        vignette.transform.SetParent(canvasObject.transform, false);
        vignette.texture = GetCircularBackdropTexture();
        vignette.color = Color.white;
        vignette.raycastTarget = false;
        vignetteRect = vignette.rectTransform;
        Stretch(vignetteRect);
        vignetteRect.localScale = Vector3.one * circularBackdropStartScale;
        var vignetteFitter = vignette.GetComponent<AspectRatioFitter>();
        vignetteFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        vignetteFitter.aspectRatio = 1f;
        vignetteGroup = vignette.GetComponent<CanvasGroup>();
        vignetteGroup.alpha = 0f;

        jumpscareImage = new GameObject("EndingJumpscareImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
        jumpscareImage.transform.SetParent(canvasObject.transform, false);
        var texture = jumpscareTexture != null ? jumpscareTexture : Resources.Load<Texture2D>(FallbackJumpscareResourcePath);
        jumpscareImage.texture = texture;
        jumpscareImage.color = new Color(1f, 1f, 1f, 0f);
        jumpscareImage.raycastTarget = false;
        jumpscareRect = jumpscareImage.rectTransform;
        Stretch(jumpscareRect);
        var aspectFitter = jumpscareImage.GetComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectFitter.aspectRatio = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 1f;
        jumpscareImage.gameObject.SetActive(false);

        jumpscareSubtitle = new GameObject("EndingJumpscareSubtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        jumpscareSubtitle.transform.SetParent(canvasObject.transform, false);
        RectTransform subtitleRect = jumpscareSubtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 0f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0f);
        subtitleRect.pivot = new Vector2(0.5f, 0f);
        subtitleRect.sizeDelta = new Vector2(1480f, 160f);
        subtitleRect.anchoredPosition = new Vector2(0f, 92f);
        jumpscareSubtitle.text = string.Empty;
        jumpscareSubtitle.fontSize = 38f;
        jumpscareSubtitle.lineSpacing = 6f;
        jumpscareSubtitle.color = Color.white;
        jumpscareSubtitle.fontStyle = FontStyles.Bold;
        jumpscareSubtitle.alignment = TextAlignmentOptions.Center;
        jumpscareSubtitle.textWrappingMode = TextWrappingModes.Normal;
        jumpscareSubtitle.overflowMode = TextOverflowModes.Overflow;
        jumpscareSubtitle.outlineColor = Color.black;
        jumpscareSubtitle.outlineWidth = 0.18f;
        jumpscareSubtitle.raycastTarget = false;
        jumpscareSubtitle.gameObject.SetActive(false);

        creditLabel = new GameObject("EndingMovieCredits", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        creditLabel.transform.SetParent(canvasObject.transform, false);
        RectTransform labelRect = creditLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(1500f, 1800f);
        labelRect.anchoredPosition = new Vector2(0f, creditsStartY);
        creditLabel.text = endingLine;
        creditLabel.fontSize = 32f;
        creditLabel.lineSpacing = 12f;
        creditLabel.color = Color.white;
        creditLabel.fontStyle = FontStyles.Bold;
        creditLabel.alignment = TextAlignmentOptions.Center;
        creditLabel.textWrappingMode = TextWrappingModes.Normal;
        creditLabel.overflowMode = TextOverflowModes.Overflow;
        creditLabel.raycastTarget = false;
        creditLabel.gameObject.SetActive(false);
    }

    private Texture2D GetCircularBackdropTexture()
    {
        if (circularBackdropTexture != null)
            return circularBackdropTexture;

        int size = Mathf.Max(128, circularBackdropTextureSize);
        var pixels = new Color32[size * size];
        float center = (size - 1f) * 0.5f;
        float innerRadius = center * 0.34f;
        float outerRadius = center * 0.98f;
        byte maxAlpha = (byte)Mathf.RoundToInt(circularBackdropEdgeOpacity * 255f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float fade = Mathf.InverseLerp(innerRadius, outerRadius, distance);
                fade = Mathf.SmoothStep(0f, 1f, fade);
                byte alpha = (byte)Mathf.RoundToInt(maxAlpha * fade);
                pixels[y * size + x] = new Color32(0, 0, 0, alpha);
            }
        }

        circularBackdropTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "EndingGeneratedCircularBackdrop",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        circularBackdropTexture.SetPixels32(pixels);
        circularBackdropTexture.Apply(false, true);
        return circularBackdropTexture;
    }

    private void DropGramophone(Transform carriedGramophone)
    {
        if (carriedGramophone == null)
            return;

        carriedGramophone.SetParent(null, true);
        Vector3 dropPosition = gramophoneDropPoint != null
            ? gramophoneDropPoint.position
            : transform.position + transform.right * 1.15f + transform.forward * 0.35f + Vector3.up * 0.08f;

        carriedGramophone.SetPositionAndRotation(dropPosition, Quaternion.Euler(gramophoneDropEuler));
        foreach (var renderer in carriedGramophone.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.enabled = true;
        }
    }

    private IEnumerator FlashWellLight()
    {
        EnsureWellGlow();
        if (wellLight == null)
            yield break;

        SetWellLightActive(true, flashIntensity);
        wellLight.color = wellFlashColor;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            float t = Mathf.Clamp01(elapsed / flashDuration);
            wellLight.intensity = Mathf.Lerp(flashIntensity, normalGlowIntensity, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetWellLightActive(true, normalGlowIntensity);
    }

    private void EnsureWellGlow()
    {
        if (wellLight != null)
            return;

        var lightObject = new GameObject("WellGlowLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * 0.25f;
        wellLight = lightObject.AddComponent<Light>();
        wellLight.type = LightType.Point;
        wellLight.color = wellFlashColor;
        wellLight.range = 8f;
        wellLight.intensity = normalGlowIntensity;
    }

    private void SetWellLightActive(bool active, float intensity)
    {
        if (wellLight == null)
            return;

        wellLight.gameObject.SetActive(active);
        wellLight.enabled = active;
        wellLight.color = wellFlashColor;
        wellLight.intensity = intensity;
    }

    private void EnsureTerrainHole()
    {
        if (!carveTerrainHoleOnAwake || terrainHoleCarved)
            return;

        Vector3 worldCenter = transform.position + terrainHoleOffset;
        Terrain terrain = ResolveTerrainForHole(worldCenter);
        if (terrain == null || terrain.terrainData == null)
            return;

        TerrainData data = terrain.terrainData;
        int resolution = data.holesResolution;
        if (resolution <= 0 || data.size.x <= 0f || data.size.z <= 0f)
            return;

        Vector3 localCenter = worldCenter - terrain.transform.position;
        float normalizedX = localCenter.x / data.size.x;
        float normalizedZ = localCenter.z / data.size.z;
        int centerX = Mathf.RoundToInt(normalizedX * (resolution - 1));
        int centerY = Mathf.RoundToInt(normalizedZ * (resolution - 1));
        if (centerX < 0 || centerX >= resolution || centerY < 0 || centerY >= resolution)
            return;

        int radiusX = Mathf.Max(1, Mathf.CeilToInt((terrainHoleRadius / data.size.x) * (resolution - 1)));
        int radiusY = Mathf.Max(1, Mathf.CeilToInt((terrainHoleRadius / data.size.z) * (resolution - 1)));
        int xMin = Mathf.Clamp(centerX - radiusX, 0, resolution - 1);
        int yMin = Mathf.Clamp(centerY - radiusY, 0, resolution - 1);
        int xMax = Mathf.Clamp(centerX + radiusX, 0, resolution - 1);
        int yMax = Mathf.Clamp(centerY + radiusY, 0, resolution - 1);
        int width = xMax - xMin + 1;
        int height = yMax - yMin + 1;
        if (width <= 0 || height <= 0)
            return;

        bool[,] holes = data.GetHoles(xMin, yMin, width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (xMin + x - centerX) / (float)radiusX;
                float dy = (yMin + y - centerY) / (float)radiusY;
                if (dx * dx + dy * dy <= 1f)
                    holes[y, x] = false;
            }
        }

        data.SetHoles(xMin, yMin, holes);
        terrainHoleCarved = true;
    }

    private Terrain ResolveTerrainForHole(Vector3 worldCenter)
    {
        if (terrainToCarve != null)
            return terrainToCarve;

        foreach (var terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 position = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            bool containsX = worldCenter.x >= position.x && worldCenter.x <= position.x + size.x;
            bool containsZ = worldCenter.z >= position.z && worldCenter.z <= position.z + size.z;
            if (containsX && containsZ)
                return terrain;
        }

        return Terrain.activeTerrain ?? FindFirstObjectByType<Terrain>(FindObjectsInactive.Exclude);
    }

    private void ResolveReferences()
    {
        if (cutSceneManager == null)
            cutSceneManager = FindFirstObjectByType<CutSceneManager>(FindObjectsInactive.Include);

        if (gramophoneDropPoint == null)
        {
            var dropPoint = FindSceneTransform("CutSceneManager/Outtro/OuttroGramophoneDropPoint")
                ?? FindSceneTransform("OuttroGramophoneDropPoint")
                ?? FindSceneTransform("GramophoneDropPoint");
            if (dropPoint != null)
                gramophoneDropPoint = dropPoint;
        }
    }

    private static Transform ResolveCarriedGramophone()
    {
        var tapePlayer = FindFirstObjectByType<FpsHorrorKit.GramophoneTapePlayer>(FindObjectsInactive.Include);
        return tapePlayer != null ? tapePlayer.transform : null;
    }

    private static Transform FindSceneTransform(string transformPathOrName)
    {
        if (string.IsNullOrWhiteSpace(transformPathOrName))
            return null;

        var direct = GameObject.Find(transformPathOrName);
        if (direct != null)
            return direct.transform;

        foreach (var sceneTransform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!sceneTransform.gameObject.scene.IsValid())
                continue;

            if (sceneTransform.name == transformPathOrName)
                return sceneTransform;
        }

        return null;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var rect = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = rect.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
