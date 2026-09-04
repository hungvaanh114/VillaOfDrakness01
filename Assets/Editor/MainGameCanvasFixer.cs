using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainGameCanvasFixer
{
    [MenuItem("Tools/MainGame/Fix Misplaced UI Canvas")]
    [MenuItem("Assets/MainGame/Fix Misplaced UI Canvas")]
    public static void FixActiveScene()
    {
        RestoreGateSignCanvases();
        var screenCanvas = MainGameEditorCanvasUtility.FindOrCreateScreenCanvas();
        if (screenCanvas == null)
            return;

        var moved = 0;
        moved += MoveUiRootsIfNeeded("InventoryUIRoot", screenCanvas.transform);
        moved += MoveUiRootsIfNeeded("PianoPuzzleUIRoot", screenCanvas.transform);

        if (moved > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"Moved {moved} UI root(s) away from world-space gate canvas.");
        }
    }

    private static int MoveUiRootsIfNeeded(string rootName, Transform targetParent)
    {
        var moved = 0;
        if (targetParent == null)
            return moved;

        foreach (var root in FindTransforms(rootName))
        {
            var parentCanvas = root.GetComponentInParent<Canvas>();
            var shouldMove = root.parent != targetParent || parentCanvas == null || parentCanvas.renderMode == RenderMode.WorldSpace || IsGateSignCanvas(parentCanvas);
            if (!shouldMove)
                continue;

            root.SetParent(targetParent, false);
            root.gameObject.layer = LayerMask.NameToLayer("UI");
            var rect = root.GetComponent<RectTransform>() ?? root.gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
            EditorUtility.SetDirty(root);
            moved++;
        }

        return moved;
    }

    private static void RestoreGateSignCanvases()
    {
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (!IsGateSignCanvas(canvas) || canvas.gameObject.scene != EditorSceneManager.GetActiveScene())
                continue;

            canvas.gameObject.name = "VillaGateSignCanvas";
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            canvas.worldCamera = null;

            var rect = canvas.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(1920f, 1080f);
                rect.localScale = Vector3.one * 0.004f;
                EditorUtility.SetDirty(rect);
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.dynamicPixelsPerUnit = 10f;
                EditorUtility.SetDirty(scaler);
            }

            EditorUtility.SetDirty(canvas);
        }
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

    private static System.Collections.Generic.IEnumerable<Transform> FindTransforms(string name)
    {
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == name && transform.gameObject.scene == EditorSceneManager.GetActiveScene())
                yield return transform;
        }
    }
}
