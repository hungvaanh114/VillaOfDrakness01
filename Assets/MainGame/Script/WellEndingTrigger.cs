using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(SphereCollider))]
public sealed class WellEndingTrigger : MonoBehaviour
{
    private const string DefaultJumpscareAssetPath = "Assets/MainGame/UI/ma duoi gieng.png";
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

    [Header("Jumpscare")]
    [SerializeField] private Texture2D jumpscareTexture;
    [SerializeField, Min(0.05f)] private float jumpscarePopDuration = 0.16f;
    [SerializeField, Min(0f)] private float jumpscareHoldDuration = 1.35f;
    [SerializeField, Min(0.05f)] private float jumpscareStartScale = 0.2f;
    [SerializeField, Min(0.05f)] private float jumpscareImpactScale = 1.08f;
    [SerializeField, Range(0f, 1f)] private float jumpscareOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float jumpscareDarkBackdropOpacity = 0.78f;

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (jumpscareTexture == null || jumpscareTexture.name == "EndingJumpscare")
            jumpscareTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultJumpscareAssetPath);
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
        BeginExitDoorEnding(ResolveCarriedGramophone());
    }

    public void ActivateEndingSetup()
    {
        ResolveReferences();
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

        carriedGramophone ??= ResolveCarriedGramophone();
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
        StartCoroutine(ExitDoorEndingRoutine(ResolveCarriedGramophone()));
    }

    private IEnumerator ExitDoorEndingRoutine(Transform carriedGramophone)
    {
        ResolveReferences();
        PlayWellFxBurst();

        if (afterDoorDelay > 0f)
            yield return new WaitForSeconds(afterDoorDelay);

        var controller = GameController.Instance;
        if (controller != null)
        {
            controller.SetChapterPhase(GameController.ChapterPhase.Ending);
            if (controller.gameUI != null)
                controller.gameUI.SetActive(false);
        }

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

        BuildEndingUi(out CanvasGroup blackGroup, out RawImage jumpscareImage, out RectTransform jumpscareRect, out TextMeshProUGUI creditLabel);

        AudioManager.Instance?.PlayWellJumpscare();
        yield return PlayJumpscareImage(blackGroup, jumpscareImage, jumpscareRect);

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

    private IEnumerator PlayJumpscareImage(CanvasGroup blackGroup, RawImage jumpscareImage, RectTransform jumpscareRect)
    {
        if (jumpscareImage == null || jumpscareRect == null)
            yield break;

        if (blackGroup != null)
            blackGroup.alpha = jumpscareDarkBackdropOpacity;

        jumpscareImage.gameObject.SetActive(true);
        jumpscareImage.color = new Color(1f, 1f, 1f, 0f);
        jumpscareRect.localScale = Vector3.one * jumpscareStartScale;

        float elapsed = 0f;
        while (elapsed < jumpscarePopDuration)
        {
            float t = Mathf.Clamp01(elapsed / jumpscarePopDuration);
            float impact = 1f - Mathf.Pow(1f - t, 3f);
            jumpscareImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, jumpscareOpacity, impact));
            jumpscareRect.localScale = Vector3.one * Mathf.Lerp(jumpscareStartScale, jumpscareImpactScale, impact);
            elapsed += Time.deltaTime;
            yield return null;
        }

        jumpscareImage.color = new Color(1f, 1f, 1f, jumpscareOpacity);
        jumpscareRect.localScale = Vector3.one;

        if (jumpscareHoldDuration > 0f)
            yield return new WaitForSeconds(jumpscareHoldDuration);
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

    private void BuildEndingUi(out CanvasGroup blackGroup, out RawImage jumpscareImage, out RectTransform jumpscareRect, out TextMeshProUGUI creditLabel)
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
