using System.Collections.Generic;
using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2WindowGlassSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string WindowGlassMaterialPath = "Assets/Flooded_Grounds/Content/Materials/BLD_WindowGlass1.mat";
    private const string BreakClipPath = "Assets/MainGame/Audio/SFX/SFX_WindowGlass_Shatter.wav";
    private const string ShardMaterialPath = "Assets/MainGame/P2/Materials/P2_WindowGlassShard.mat";

    [MenuItem("MainGame/P2/Apply P2 House Window Glass Breakables")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var windowGlassMaterial = AssetDatabase.LoadAssetAtPath<Material>(WindowGlassMaterialPath);
        if (windowGlassMaterial == null)
        {
            Debug.LogError($"Missing window glass material: {WindowGlassMaterialPath}");
            return;
        }

        var breakClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakClipPath);
        var shardMaterial = EnsureShardMaterial();
        int configured = ConfigureWindowGlass(windowGlassMaterial, breakClip, shardMaterial, true);
        if (configured == 0)
            configured = ConfigureWindowGlass(windowGlassMaterial, breakClip, shardMaterial, false);

        EnsureBreakController();
        ConfigureP2GameControllerBreakClip(breakClip);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"P2 house window glass breakables applied. Configured {configured} glass renderers.");
    }

    private static int ConfigureWindowGlass(Material windowGlassMaterial, AudioClip breakClip, Material shardMaterial, bool houseOnly)
    {
        var scopeRoots = houseOnly ? FindHouseScopeRoots() : new List<Transform>();
        int configured = 0;

        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer == null || !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene.path != ScenePath)
                continue;
            if (houseOnly && !IsUnderAnyRoot(renderer.transform, scopeRoots))
                continue;
            if (!UsesMaterial(renderer, windowGlassMaterial))
                continue;

            var breakable = renderer.GetComponent<P2BreakableWindowGlass>();
            if (breakable == null)
                breakable = renderer.gameObject.AddComponent<P2BreakableWindowGlass>();

            breakable.Configure(renderer, breakClip, shardMaterial);
            Set(breakable, "breakOnCollision", false);
            Set(breakable, "breakOnTriggerEnter", false);
            Set(breakable, "disableCollidersAfterBreak", true);
            Set(breakable, "shardCount", 18);
            Set(breakable, "shardLifetime", 6f);
            Set(breakable, "shardImpulse", 2.4f);
            Set(breakable, "upwardImpulse", 0.65f);
            Set(breakable, "shardTorque", 7f);

            EditorUtility.SetDirty(breakable);
            EditorUtility.SetDirty(renderer.gameObject);
            configured++;
        }

        return configured;
    }

    private static List<Transform> FindHouseScopeRoots()
    {
        var roots = new List<Transform>();
        AddRootIfFound(roots, "House");
        AddRootIfFound(roots, "Villa");
        return roots;
    }

    private static void AddRootIfFound(List<Transform> roots, string name)
    {
        var root = FindSceneTransform(name);
        if (root != null && !roots.Contains(root))
            roots.Add(root);
    }

    private static bool IsUnderAnyRoot(Transform transform, List<Transform> roots)
    {
        if (roots == null || roots.Count == 0)
            return false;

        for (int i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            if (root != null && (transform == root || transform.IsChildOf(root)))
                return true;
        }

        return false;
    }

    private static bool UsesMaterial(Renderer renderer, Material targetMaterial)
    {
        var materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == targetMaterial)
                return true;
        }

        return false;
    }

    private static void EnsureBreakController()
    {
        var systems = FindSceneTransform("MainGameSystems");
        var controllerTransform = FindSceneTransform("P2_HouseWindowGlassBreakController");
        if (controllerTransform == null)
        {
            var obj = new GameObject("P2_HouseWindowGlassBreakController");
            controllerTransform = obj.transform;
        }

        if (systems != null)
            controllerTransform.SetParent(systems, false);

        if (controllerTransform.GetComponent<P2HouseWindowGlassBreakController>() == null)
            controllerTransform.gameObject.AddComponent<P2HouseWindowGlassBreakController>();

        EditorUtility.SetDirty(controllerTransform.gameObject);
    }

    private static void ConfigureP2GameControllerBreakClip(AudioClip breakClip)
    {
        if (breakClip == null)
            return;

        var controller = Object.FindFirstObjectByType<P2GameController>(FindObjectsInactive.Include);
        if (controller == null)
            return;

        var serialized = new SerializedObject(controller);
        var property = serialized.FindProperty("glassBreakClip");
        if (property != null)
            property.objectReferenceValue = breakClip;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static Material EnsureShardMaterial()
    {
        EnsureFolder("Assets/MainGame/P2", "Materials");
        var material = AssetDatabase.LoadAssetAtPath<Material>(ShardMaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ShardMaterialPath);
        }

        material.color = new Color(0.64f, 0.86f, 1f, 0.45f);
        material.SetColor("_BaseColor", new Color(0.64f, 0.86f, 1f, 0.45f));
        material.SetColor("_Color", new Color(0.64f, 0.86f, 1f, 0.45f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName && transform.gameObject.scene.IsValid())
                return transform;
        }

        return null;
    }

    private static void Set(Object target, string propertyName, bool value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, float value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
