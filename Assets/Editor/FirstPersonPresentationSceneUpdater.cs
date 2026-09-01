using FpsHorrorKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FirstPersonPresentationSceneUpdater
{
    private const string GameScenePath = "Assets/MainGame/Game.unity";
    private const string FlashlightPrefabPath = "Assets/Electric Torch/Prefab/Electric Torch LowPoly.prefab";
    private const string FlashlightAnchorName = "Electric torch";
    private const string FlashlightViewModelName = "FirstPersonFlashlightViewModel";

    [MenuItem("Tools/MainGame/Configure First Person Presentation")]
    public static void ConfigureActiveScene()
    {
        ConfigureScene(true);
    }

    public static void ConfigureGameScenePresentation()
    {
        EditorSceneManager.OpenScene(GameScenePath);
        ConfigureScene(true);
    }

    private static void ConfigureScene(bool saveScene)
    {
        var playerController = Object.FindFirstObjectByType<FpsController>(FindObjectsInactive.Include);
        if (playerController == null)
        {
            Debug.LogError("Cannot configure first-person presentation. FpsController was not found.");
            return;
        }

        var presentation = playerController.GetComponent<FirstPersonPresentationController>();
        if (presentation == null)
            presentation = Undo.AddComponent<FirstPersonPresentationController>(playerController.gameObject);

        var playerModelRoot = FindChild(playerController.transform, "PlayerNew");
        var flashlightViewModel = EnsureFlashlightViewModel(playerController);

        var serialized = new SerializedObject(presentation);
        serialized.FindProperty("playerModelRoot").objectReferenceValue = playerModelRoot != null ? playerModelRoot.gameObject : null;
        serialized.FindProperty("firstPersonFlashlightViewModel").objectReferenceValue = flashlightViewModel;
        serialized.FindProperty("hidePlayerRenderersInFirstPerson").boolValue = true;
        serialized.FindProperty("showFlashlightViewModelInFirstPerson").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(presentation);
        EditorSceneManager.MarkSceneDirty(playerController.gameObject.scene);
        if (saveScene)
            EditorSceneManager.SaveScene(playerController.gameObject.scene);

        Debug.Log("First-person presentation configured: player body renders only in cutscenes, flashlight viewmodel renders only outside cutscenes.");
    }

    private static GameObject EnsureFlashlightViewModel(FpsController playerController)
    {
        var pivot = playerController.flashlightPivot != null
            ? playerController.flashlightPivot
            : FindChild(playerController.transform, "FlashlightPivot");
        if (pivot == null)
        {
            var pivotObject = new GameObject("FlashlightPivot");
            Undo.RegisterCreatedObjectUndo(pivotObject, "Create Flashlight Pivot");
            pivot = pivotObject.transform;
            pivot.SetParent(playerController.transform, false);
            pivot.localPosition = new Vector3(0.298f, 0.279f, 0.759f);
        }

        var anchor = pivot.Find(FlashlightAnchorName);
        if (anchor == null)
        {
            var anchorObject = new GameObject(FlashlightAnchorName);
            Undo.RegisterCreatedObjectUndo(anchorObject, "Create First Person Flashlight Anchor");
            anchor = anchorObject.transform;
            anchor.SetParent(pivot, false);
            anchor.localPosition = new Vector3(-0.26f, -0.081f, -0.171f);
            anchor.localEulerAngles = new Vector3(127.852f, -10.62f, 76.161f);
        }

        var existing = anchor.Find(FlashlightViewModelName);
        if (existing != null)
            return existing.gameObject;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefabPath);
        GameObject viewModel;
        if (prefab != null)
        {
            viewModel = PrefabUtility.InstantiatePrefab(prefab, anchor) as GameObject;
            Undo.RegisterCreatedObjectUndo(viewModel, "Create First Person Flashlight Viewmodel");
        }
        else
        {
            viewModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(viewModel, "Create First Person Flashlight Viewmodel");
            viewModel.transform.SetParent(anchor, false);
        }

        viewModel.name = FlashlightViewModelName;
        viewModel.transform.localPosition = Vector3.zero;
        viewModel.transform.localRotation = Quaternion.identity;
        viewModel.transform.localScale = Vector3.one;
        viewModel.SetActive(true);

        foreach (var collider in viewModel.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var light in viewModel.GetComponentsInChildren<Light>(true))
            light.enabled = false;

        return viewModel;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}

[InitializeOnLoad]
public static class FirstPersonPresentationSceneUpdaterAutoRun
{
    static FirstPersonPresentationSceneUpdaterAutoRun()
    {
        EditorApplication.delayCall += ConfigureIfGameSceneIsOpen;
    }

    private static void ConfigureIfGameSceneIsOpen()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/MainGame/Game.unity")
            return;

        FirstPersonPresentationSceneUpdater.ConfigureActiveScene();
    }
}
