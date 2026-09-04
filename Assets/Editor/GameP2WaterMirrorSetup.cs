using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2WaterMirrorSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string RootName = "P2_WaterMirror_Setup";
    private const string WaterName = "P2_WaterMirror_Jumpscare";
    private const string MaterialPath = "Assets/MainGame/Materials/InventoryProgression/P2_WaterMirror.mat";
    private const string ShaderPath = "Assets/MainGame/Shaders/P2_BlackWaterMirror.shader";
    private const string ShaderName = "MainGame/P2/Black Water Mirror";
    private const string JumpscareTexturePath = "Assets/MainGame/UI/anhHuMa.png";

    [MenuItem("MainGame/P2/Apply P2 Water Mirror Jumpscare")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var material = EnsureWaterMaterial();
        if (ApplyMaterialOnlyToExistingWater(material))
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("P2 water mirror material updated only. Existing water mirror setup was kept unchanged.");
            return;
        }

        var water = EnsureWaterObject(material);
        ConfigureJumpscare(water);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("P2 water mirror jumpscare applied.");
    }

    private static Transform EnsureWaterObject(Material material)
    {
        var root = FindSceneTransform(RootName);
        if (root == null)
            root = new GameObject(RootName).transform;

        var water = FindSceneTransform(WaterName);
        if (water == null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = WaterName;
            water = obj.transform;
            water.position = FindDefaultPosition();
            water.rotation = Quaternion.identity;
            water.localScale = new Vector3(2.2f, 0.018f, 1.25f);
        }

        water.SetParent(root, true);
        var renderer = water.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        var collider = water.GetComponent<BoxCollider>();
        if (collider == null)
            collider = water.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = Vector3.one;
        collider.center = Vector3.zero;
        EditorUtility.SetDirty(collider);

        EditorUtility.SetDirty(water.gameObject);
        return water;
    }

    private static void ConfigureJumpscare(Transform water)
    {
        var trigger = water.GetComponent<P2WaterMirrorJumpscare>();
        if (trigger == null)
            trigger = water.gameObject.AddComponent<P2WaterMirrorJumpscare>();

        var serialized = new SerializedObject(trigger);
        Set(serialized, "waterSurface", water);
        Set(serialized, "triggerOnlyOnce", true);
        Set(serialized, "requiredLookSeconds", 3f);
        Set(serialized, "requiredPlayerDistance", 7f);
        Set(serialized, "requirePlayerInFront", false);
        Set(serialized, "acceptEitherWaterSide", true);
        Set(serialized, "requireRaycastHit", true);
        Set(serialized, "lookRaycastDistance", 7f);
        Set(serialized, "lookRaycastRadius", 0.08f);
        Set(serialized, "screenJumpscareTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(JumpscareTexturePath));
        Set(serialized, "popDuration", 0.2f);
        Set(serialized, "holdDuration", 2.35f);
        Set(serialized, "startScale", 0.18f);
        Set(serialized, "impactScale", 1.24f);
        Set(serialized, "imageOpacity", 1f);
        Set(serialized, "darkBackdropOpacity", 0.72f);
        Set(serialized, "respawnDelayPadding", 0.08f);
        Set(serialized, "playDeathVoiceImmediately", false);
        Set(serialized, "deathVoiceIndex", 3);
        Set(serialized, "drawDebugGizmos", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
    }

    private static Material EnsureWaterMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = Shader.Find(ShaderName) ?? AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (material == null)
        {
            material = new Material(shader != null ? shader : Shader.Find("HDRP/Unlit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        if (shader != null)
            material.shader = shader;

        SetColorIfPresent(material, "_DeepColor", new Color(0.003f, 0.006f, 0.009f, 1f));
        SetColorIfPresent(material, "_EdgeColor", new Color(0.045f, 0.105f, 0.13f, 1f));
        SetColorIfPresent(material, "_HighlightColor", new Color(0.18f, 0.32f, 0.36f, 1f));
        SetFloatIfPresent(material, "_Alpha", 0.96f);
        SetFloatIfPresent(material, "_Smoothness", 0.98f);
        SetFloatIfPresent(material, "_FresnelPower", 3.2f);
        SetFloatIfPresent(material, "_RippleStrength", 0.008f);
        SetFloatIfPresent(material, "_RippleScale", 4.5f);
        SetFloatIfPresent(material, "_RippleSpeed", 0.38f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool ApplyMaterialOnlyToExistingWater(Material material)
    {
        var water = FindSceneTransform(WaterName);
        if (water == null)
            return false;

        var renderer = water.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != material)
        {
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        return true;
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, value);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static Vector3 FindDefaultPosition()
    {
        var journal = FindSceneTransform("NhatKy");
        if (journal != null)
            return journal.position + new Vector3(0f, 0.055f, 1.35f);

        var player = Object.FindFirstObjectByType<FpsHorrorKit.FpsController>();
        if (player != null)
            return player.transform.position + player.transform.forward * 2.2f + Vector3.up * 0.02f;

        return new Vector3(0f, 0.02f, 0f);
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

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, bool value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, int value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }
}
