using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class EndingP2TransitionController : MonoBehaviour
{
    [SerializeField] private string creditsSceneName = "Credits";
    [SerializeField, Min(0f)] private float holdSeconds = 4f;
    [SerializeField] private bool allowSkipAfterOneSecond = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && !Application.isPlaying)
                EnsureLayout();
        };
    }
#endif

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        EnsureLayout();

        float elapsed = 0f;
        while (elapsed < holdSeconds)
        {
            elapsed += Time.deltaTime;
            if (allowSkipAfterOneSecond && elapsed >= 1f && WasSkipPressed())
                break;

            yield return null;
        }

        if (!string.IsNullOrWhiteSpace(creditsSceneName))
            SceneManager.LoadScene(creditsSceneName);
    }

    private static bool WasSkipPressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.spaceKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame
                || keyboard.escapeKey.wasPressedThisFrame);
    }

    private void EnsureLayout()
    {
        if (transform.Find("EndingP2Canvas") != null)
            return;

        var canvasObject = new GameObject("EndingP2Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        AddText(canvasObject.transform, "PartTitle", "PHẦN 2", 72f, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.76f), FontStyles.Bold);
        AddText(canvasObject.transform, "P2CharacterName", "NHÂN VẬT CHÍNH P2", 46f, new Vector2(0.1f, 0.49f), new Vector2(0.9f, 0.6f), FontStyles.Bold);
        AddText(canvasObject.transform, "ToBeContinued", "To be continued", 38f, new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.48f), FontStyles.Italic);
    }

    private static void AddText(Transform parent, string name, string text, float size, Vector2 anchorMin, Vector2 anchorMax, FontStyles style)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.86f, 0.92f, 1f, 1f);
        label.fontStyle = style;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
    }
}
