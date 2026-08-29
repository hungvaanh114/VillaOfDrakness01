using FpsHorrorKit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainGamePhysicalPianoSceneUpdater
{
    private const string ScenePath = "Assets/MainGame/Game.unity";
    private const string PhysicalPianoName = "Prop_Piano_FullKeys";
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

    [MenuItem("Tools/MainGame/Install Physical Piano")]
    [MenuItem("Assets/MainGame/Install Physical Piano")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var piano = FindSceneTransform(PhysicalPianoName);
        if (piano == null)
        {
            Debug.LogError($"Could not find {PhysicalPianoName} in {ScenePath}.");
            return;
        }

        var controller = piano.GetComponent<PhysicalPianoController>() ?? piano.gameObject.AddComponent<PhysicalPianoController>();
        var interactable = piano.GetComponent<PianoInteractable>() ?? piano.gameObject.AddComponent<PianoInteractable>();
        EnsureCollider(piano);
        ConfigureController(controller, piano);
        ConfigureInteractable(interactable, controller);
        EnsureKeyLabels(piano);
        ApplyWhiteMaterialToKeys(piano);

        EditorUtility.SetDirty(piano.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Physical piano installed on Prop_Piano_FullKeys.");
    }

    private static void ConfigureController(PhysicalPianoController controller, Transform piano)
    {
        var serialized = new SerializedObject(controller);
        var cameraTarget = serialized.FindProperty("cameraTarget");
        if (cameraTarget != null)
            cameraTarget.objectReferenceValue = FindChildTransform(piano, "camTaget") ?? FindChildTransform(piano, "camTarget");

        var focusCamera = serialized.FindProperty("focusCameraOnKeys");
        if (focusCamera != null)
            focusCamera.boolValue = true;

        Set(serialized, "playableKeyMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/MainGame/Mesh/Mat_PianoKey_Playable.mat"));
        Set(serialized, "selectedKeyMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/MainGame/Mesh/Mat_PianoKey_Selected.mat"));
        Set(serialized, "whiteKeyMaterial", AssetDatabase.LoadAssetAtPath<Material>("Assets/MainGame/Mesh/Mat_PianoKey_White.mat"));

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void ConfigureInteractable(PianoInteractable interactable, PhysicalPianoController controller)
    {
        var serialized = new SerializedObject(interactable);
        var physicalPiano = serialized.FindProperty("physicalPiano");
        if (physicalPiano != null)
            physicalPiano.objectReferenceValue = controller;

        if (TryReadExistingPianoTestFlag(out bool testFlag))
        {
            var testProperty = serialized.FindProperty("testTreatMusicSheetCompleted");
            if (testProperty != null)
                testProperty.boolValue = testFlag;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(interactable);
    }

    private static void EnsureKeyLabels(Transform piano)
    {
        foreach (var binding in NoteBindings)
        {
            var key = FindChildTransform(piano, binding.ObjectName);
            if (key == null)
                continue;

            var label = EnsureWorldSpaceLabel(key, binding.Note);
            label.gameObject.SetActive(false);
            EditorUtility.SetDirty(label.gameObject);
            EditorUtility.SetDirty(key.gameObject);
        }
    }

    private static RectTransform EnsureWorldSpaceLabel(Transform key, string note)
    {
        string labelName = $"PianoNoteLabel_{note}";
        var existing = key.Find(labelName);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            return existingRect;

        var labelObject = new GameObject(labelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasRenderer), typeof(Image));
        var labelTransform = labelObject.GetComponent<RectTransform>();
        labelTransform.SetParent(key, true);

        var canvas = labelObject.GetComponent<Canvas>() ?? labelObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120;

        var scaler = labelObject.GetComponent<CanvasScaler>() ?? labelObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 20f;
        scaler.referencePixelsPerUnit = 100f;

        var background = labelObject.GetComponent<Image>() ?? labelObject.AddComponent<Image>();
        background.color = new Color(0f, 0.006f, 0.012f, 0.68f);
        background.raycastTarget = false;

        labelTransform.sizeDelta = new Vector2(0.18f, 0.12f);
        if (TryGetWorldRendererBounds(key, out var bounds))
            labelTransform.position = bounds.center + Vector3.up * (bounds.extents.y + 0.12f) + new Vector3(0f, 0.08f, 0f);
        labelTransform.localScale = Vector3.one;

        var text = EnsureLabelText(labelTransform);
        text.text = note;
        text.fontSize = 42f;
        text.color = new Color(0.9f, 0.98f, 1f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        return labelTransform;
    }

    private static TextMeshProUGUI EnsureLabelText(RectTransform label)
    {
        var existing = label.Find("Text");
        if (existing != null && existing.GetComponent<RectTransform>() == null)
        {
            Object.DestroyImmediate(existing.gameObject);
            existing = null;
        }

        var textObject = existing != null
            ? existing.gameObject
            : new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(label, false);
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;

        return textObject.GetComponent<TextMeshProUGUI>();
    }

    private static void ApplyWhiteMaterialToKeys(Transform piano)
    {
        var whiteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/MainGame/Mesh/Mat_PianoKey_White.mat");
        if (whiteMaterial == null)
            return;

        foreach (var binding in NoteBindings)
        {
            var key = FindChildTransform(piano, binding.ObjectName);
            if (key == null)
                continue;

            foreach (var renderer in key.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = whiteMaterial;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static bool TryReadExistingPianoTestFlag(out bool value)
    {
        foreach (var interactable in Object.FindObjectsByType<PianoInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (interactable == null || interactable.gameObject.name == PhysicalPianoName)
                continue;

            if (!interactable.gameObject.name.Contains("Test_PianoPuzzle"))
                continue;

            var serialized = new SerializedObject(interactable);
            var property = serialized.FindProperty("testTreatMusicSheetCompleted");
            if (property != null)
            {
                value = property.boolValue;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static void EnsureCollider(Transform root)
    {
        if (root.GetComponent<Collider>() != null)
            return;

        var collider = root.gameObject.AddComponent<BoxCollider>();
        if (TryGetLocalRendererBounds(root, out var bounds))
        {
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        EditorUtility.SetDirty(collider);
    }

    private static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
    {
        bool hasBounds = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
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

    private static bool TryGetWorldRendererBounds(Transform root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = new Bounds(root.position, Vector3.one * 0.05f);
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform.name == objectName)
                return transform;
        }

        return null;
    }

    private static Transform FindChildTransform(Transform root, string objectName)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == objectName)
                return transform;
        }

        return null;
    }

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
}
