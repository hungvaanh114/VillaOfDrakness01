using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainGameEditorCanvasUtility
{
    public static Canvas FindOrCreateScreenCanvas()
    {
        var canvas = FindExistingScreenCanvas();
        if (canvas != null)
        {
            SetupScreenCanvas(canvas);
            return canvas;
        }

        var gameObject = new GameObject("MainGameUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        gameObject.layer = LayerMask.NameToLayer("UI");
        canvas = gameObject.GetComponent<Canvas>();
        SetupScreenCanvas(canvas);
        EditorUtility.SetDirty(gameObject);
        return canvas;
    }

    private static Canvas FindExistingScreenCanvas()
    {
        var settingUI = FindTransform("SettingUI");
        var settingCanvas = settingUI != null ? settingUI.GetComponentInParent<Canvas>() : null;
        if (IsScreenCanvas(settingCanvas))
            return settingCanvas;

        var gameUI = FindTransform("GameUI");
        var gameCanvas = gameUI != null ? gameUI.GetComponentInParent<Canvas>() : null;
        if (IsScreenCanvas(gameCanvas))
            return gameCanvas;

        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas != null && canvas.gameObject.scene == EditorSceneManager.GetActiveScene() && IsScreenCanvas(canvas))
                return canvas;
        }

        return null;
    }

    private static bool IsScreenCanvas(Canvas canvas)
    {
        return canvas != null && canvas.renderMode != RenderMode.WorldSpace && !IsGateSignCanvas(canvas);
    }

    private static bool IsGateSignCanvas(Canvas canvas)
    {
        if (canvas == null)
            return false;

        foreach (var image in canvas.GetComponentsInChildren<Image>(true))
        {
            if (image != null && image.sprite != null && image.sprite.name.ToLowerInvariant().Contains("bien nha"))
                return true;
        }

        var parentName = canvas.transform.parent != null ? canvas.transform.parent.name.ToLowerInvariant() : string.Empty;
        return parentName.Contains("gate") || parentName.Contains("fence");
    }

    private static Transform FindTransform(string name)
    {
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == name && transform.gameObject.scene == EditorSceneManager.GetActiveScene())
                return transform;
        }

        return null;
    }

    private static void SetupScreenCanvas(Canvas canvas)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (!IsGateSignCanvas(canvas))
            canvas.gameObject.name = "MainGameUICanvas";
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 0);

        var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(scaler);
    }
}
