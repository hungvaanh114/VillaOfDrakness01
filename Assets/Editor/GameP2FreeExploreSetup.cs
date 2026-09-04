using FpsHorrorKit;
using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2FreeExploreSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string SilverMirrorAssetPath = "Assets/MainGame/Data/InventoryProgression/Item_P2_SilverMirror.asset";
    private const string SilverMaterialPath = "Assets/MainGame/Materials/InventoryProgression/P2_Block_SilverMirror.mat";

    [MenuItem("MainGame/P2/Apply P2 Free Explore Rules")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var silverMirror = EnsureSilverMirrorItem();
        var silverMaterial = EnsureMaterial(SilverMaterialPath, new Color(0.72f, 0.78f, 0.82f, 1f));
        CreateSilverMirrorPickup(silverMirror, silverMaterial);
        RemovePianoDoorLocksOnly();
        ConfigurePianoFreePlay();
        HideMusicSheetSystems();
        GameP2WindowGlassSetup.Apply();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("P2 free explore rules applied.");
    }

    private static ItemData EnsureSilverMirrorItem()
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(SilverMirrorAssetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, SilverMirrorAssetPath);
        }

        item.id = "Item_P2_SilverMirror";
        item.itemName = "Gương bạc";
        item.description = "Một mảnh gương bạc lạnh, nặng hơn vẻ ngoài của nó.";
        item.itemType = ItemType.QuestItem;
        item.canStack = false;
        item.maxStack = 1;
        item.canUse = false;
        item.keyID = string.Empty;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void CreateSilverMirrorPickup(ItemData item, Material material)
    {
        var root = EnsureRoot("P2_FreeExploreItems");
        var pickup = FindSceneTransform("P2_SilverMirror_Item");
        if (pickup == null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "P2_SilverMirror_Item";
            pickup = obj.transform;
            pickup.SetParent(root, true);
            pickup.position = new Vector3(1.15f, 4.05f, -4.6f);
            pickup.rotation = Quaternion.Euler(0f, 90f, 0f);
            pickup.localScale = new Vector3(0.08f, 0.7f, 0.45f);
        }

        var renderer = pickup.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        var collider = pickup.GetComponent<Collider>();
        if (collider == null)
            collider = pickup.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        EditorUtility.SetDirty(collider);

        var inventoryPickup = pickup.GetComponent<ItemPickup>();
        if (inventoryPickup != null)
            Object.DestroyImmediate(inventoryPickup, true);

        var heldPickup = pickup.GetComponent<P2HeldSilverMirrorPickup>();
        if (heldPickup == null)
            heldPickup = pickup.gameObject.AddComponent<P2HeldSilverMirrorPickup>();

        Set(heldPickup, "heldRoot", pickup);
        Set(heldPickup, "targetCamera", Camera.main);
        Set(heldPickup, "handRootName", "LeftHandProp");
        Set(heldPickup, "fallbackHandRootName", "LeftHand");
        Set(heldPickup, "pickupClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MainGame/Audio/SFX/SFX_KeyPickup.mp3"));
        Set(heldPickup, "interactText", "[E] Cầm gương bạc");
        Set(heldPickup, "heldLocalPosition", new Vector3(0.02f, 0.04f, 0.02f));
        Set(heldPickup, "heldLocalEulerAngles", new Vector3(0f, 90f, 0f));
        Set(heldPickup, "overrideHeldScale", false);
        Set(heldPickup, "breakHouseGlassWhenPickedUp", true);
        Set(heldPickup, "lockPlayerWhileHeld", false);
        Set(heldPickup, "allowPutDown", false);
        Set(heldPickup, "allowMouseRotation", false);
        Set(heldPickup, "matchHandLayerWhileHeld", true);
        EditorUtility.SetDirty(heldPickup);
        EditorUtility.SetDirty(pickup.gameObject);
    }

    private static void RemovePianoDoorLocksOnly()
    {
        foreach (var door in Object.FindObjectsByType<DoorSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (door == null || !door.gameObject.scene.IsValid() || !door.openWhenPianoCompleted)
                continue;

            var serialized = new SerializedObject(door);
            Set(serialized, "openWhenPianoCompleted", false);

            var requiredKey = serialized.FindProperty("requiredKeyID");
            if (requiredKey == null || string.IsNullOrWhiteSpace(requiredKey.stringValue))
            {
                Set(serialized, "isLocked", false);
                Set(serialized, "hasKey", true);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(door);
        }
    }

    private static void ConfigurePianoFreePlay()
    {
        foreach (var interactable in Object.FindObjectsByType<PianoInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (interactable == null || !interactable.gameObject.scene.IsValid())
                continue;

            Set(interactable, "requireMusicSheets", false);
            Set(interactable, "blockWhenCompleted", false);
            Set(interactable, "markCheckpointOnInteract", false);
            Set(interactable, "testTreatMusicSheetCompleted", true);
            EditorUtility.SetDirty(interactable);
        }

        foreach (var piano in Object.FindObjectsByType<PhysicalPianoController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (piano == null || !piano.gameObject.scene.IsValid())
                continue;

            Set(piano, "freePlayOnly", true);
            Set(piano, "disableInteractionAfterCompleted", false);
            EditorUtility.SetDirty(piano);
        }
    }

    private static void HideMusicSheetSystems()
    {
        foreach (var inventory in Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (inventory == null || !inventory.gameObject.scene.IsValid())
                continue;

            Set(inventory, "hideMusicSheetTab", true);
            EditorUtility.SetDirty(inventory);
        }

        SetSceneObjectActive("MusicSheetTab", false);
        SetSceneObjectActive("MusicPanel", false);
        SetSceneObjectActive("Tab_2", false);
        SetSceneObjectActive("MusicSheet", false);

        foreach (var pickup in Object.FindObjectsByType<MusicSheetPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pickup != null && pickup.gameObject.scene.IsValid())
            {
                pickup.gameObject.SetActive(false);
                EditorUtility.SetDirty(pickup.gameObject);
            }
        }

        foreach (var manager in Object.FindObjectsByType<MusicSheetManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (manager != null && manager.gameObject.scene.IsValid())
            {
                manager.enabled = false;
                EditorUtility.SetDirty(manager);
            }
        }
    }

    private static void SetSceneObjectActive(string objectName, bool active)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform == null || transform.name != objectName || !transform.gameObject.scene.IsValid())
                continue;

            transform.gameObject.SetActive(active);
            EditorUtility.SetDirty(transform.gameObject);
        }
    }

    private static Transform EnsureRoot(string name)
    {
        var root = FindSceneTransform(name);
        if (root != null)
            return root;

        return new GameObject(name).transform;
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

    private static Material EnsureMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Set(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        Set(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, string value)
    {
        var serialized = new SerializedObject(target);
        Set(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        Set(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, bool value)
    {
        var serialized = new SerializedObject(target);
        Set(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string propertyName, Vector3 value)
    {
        var serialized = new SerializedObject(target);
        Set(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, int value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, bool value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, Vector3 value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }
}
