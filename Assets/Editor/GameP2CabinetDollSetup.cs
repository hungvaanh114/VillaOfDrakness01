using FpsHorrorKit;
using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2CabinetDollSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string QuestRootName = "P2_CabinetDollQuest";
    private const string KeyAssetPath = "Assets/MainGame/Data/InventoryProgression/Item_P2_CabinetKey.asset";
    private const string DollAssetPath = "Assets/MainGame/Data/InventoryProgression/Item_P2_LinhDoll.asset";
    private const string AudioDataPath = "Assets/MainGame/Resources/Audio/AudioData.asset";

    [MenuItem("MainGame/P2/Apply P2 Cabinet Doll Quest")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var keyItem = EnsureItemData(
            KeyAssetPath,
            "Item_P2_CabinetKey",
            "Chìa khóa tủ",
            "Một chìa khóa nhỏ dùng để mở chiếc tủ cũ trên tầng hai.",
            ItemType.Key,
            "P2_CABINET_KEY",
            true);

        var dollItem = EnsureItemData(
            DollAssetPath,
            "Item_P2_LinhDoll",
            "Búp bê của bé Linh",
            "Một con búp bê cũ được cất trong tủ.",
            ItemType.QuestItem,
            string.Empty,
            false);

        var root = EnsureRoot(QuestRootName);
        var materials = EnsureMaterials();
        var audioData = AssetDatabase.LoadAssetAtPath<AudioData>(AudioDataPath);

        CreateCabinetQuest(root, keyItem, dollItem, audioData != null ? audioData.p2Linh02 : null, materials);
        ConfigureUpperFloorLinhTrigger(audioData != null ? audioData.p2Linh01 : null);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("P2 cabinet, doll, key, and upper-floor Linh trigger applied.");
    }

    private static void CreateCabinetQuest(Transform root, ItemData keyItem, ItemData dollItem, AudioClip linh02, Materials materials)
    {
        var key = EnsurePrimitive(root, "P2_Cabinet_Key", PrimitiveType.Cube);
        key.transform.position = new Vector3(2.45f, 3.9f, -4.55f);
        key.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
        key.transform.localScale = new Vector3(0.28f, 0.05f, 0.1f);
        SetMaterial(key, materials.Brass);

        var keyPickup = key.GetComponent<ItemPickup>();
        if (keyPickup == null)
            keyPickup = key.AddComponent<ItemPickup>();
        SetPrivate(keyPickup, "itemData", keyItem);
        SetPrivate(keyPickup, "amount", 1);
        SetPrivate(keyPickup, "interactText", "[E] Nhặt chìa khóa tủ");
        EnsureCollider(key, false);
        EditorUtility.SetDirty(keyPickup);

        var cabinet = EnsureRootChild(root, "P2_Linh_Doll_Cabinet");
        cabinet.position = new Vector3(4.8f, 3.92f, -6.7f);
        cabinet.rotation = Quaternion.Euler(0f, 90f, 0f);

        var closedVisual = EnsureRootChild(cabinet, "ClosedVisual").gameObject;
        ClearPrimitiveChildren(closedVisual.transform);
        AddCube(closedVisual.transform, "CabinetBody", Vector3.zero, new Vector3(1.25f, 1.55f, 0.52f), materials.DarkWood);
        AddCube(closedVisual.transform, "CabinetDoor", new Vector3(0f, 0f, -0.31f), new Vector3(1.15f, 1.42f, 0.08f), materials.Wood);
        AddCube(closedVisual.transform, "CabinetHandle", new Vector3(0.42f, 0f, -0.38f), new Vector3(0.08f, 0.32f, 0.08f), materials.Brass);

        var openVisual = EnsureRootChild(cabinet, "OpenVisual").gameObject;
        ClearPrimitiveChildren(openVisual.transform);
        AddCube(openVisual.transform, "OpenCabinetBody", Vector3.zero, new Vector3(1.25f, 1.55f, 0.52f), materials.DarkWood);
        AddCube(openVisual.transform, "OpenCabinetDoorLeft", new Vector3(-0.68f, 0f, -0.22f), new Vector3(0.08f, 1.42f, 0.52f), materials.Wood);
        AddCube(openVisual.transform, "OpenCabinetDoorRight", new Vector3(0.68f, 0f, -0.22f), new Vector3(0.08f, 1.42f, 0.52f), materials.Wood);

        var contents = EnsureRootChild(cabinet, "Contents").gameObject;
        ClearPrimitiveChildren(contents.transform);
        var doll = EnsureRootChild(contents.transform, "P2_Linh_Doll_Pickup").gameObject;
        doll.transform.localPosition = new Vector3(0f, -0.1f, -0.47f);
        doll.transform.localRotation = Quaternion.identity;
        doll.transform.localScale = Vector3.one;
        ClearPrimitiveChildren(doll.transform);
        AddCube(doll.transform, "DollBody", new Vector3(0f, 0f, 0f), new Vector3(0.28f, 0.46f, 0.16f), materials.FadedCloth);
        AddCube(doll.transform, "DollHead", new Vector3(0f, 0.33f, 0f), new Vector3(0.22f, 0.22f, 0.18f), materials.Pale);
        AddCube(doll.transform, "DollLeftArm", new Vector3(-0.22f, 0.06f, 0f), new Vector3(0.12f, 0.36f, 0.1f), materials.FadedCloth);
        AddCube(doll.transform, "DollRightArm", new Vector3(0.22f, 0.06f, 0f), new Vector3(0.12f, 0.36f, 0.1f), materials.FadedCloth);
        AddCube(doll.transform, "DollLegs", new Vector3(0f, -0.35f, 0f), new Vector3(0.24f, 0.28f, 0.12f), materials.Wood);

        var dollCollider = doll.GetComponent<BoxCollider>();
        if (dollCollider == null)
            dollCollider = doll.AddComponent<BoxCollider>();
        dollCollider.isTrigger = false;
        dollCollider.center = new Vector3(0f, 0f, 0f);
        dollCollider.size = new Vector3(0.78f, 0.95f, 0.42f);
        EditorUtility.SetDirty(dollCollider);

        var dollPickup = doll.GetComponent<P2DollPickup>();
        if (dollPickup == null)
            dollPickup = doll.AddComponent<P2DollPickup>();

        var dollAudioSource = doll.GetComponent<AudioSource>();
        if (dollAudioSource == null)
            dollAudioSource = doll.AddComponent<AudioSource>();
        dollAudioSource.clip = linh02;
        dollAudioSource.playOnAwake = false;
        dollAudioSource.loop = false;
        dollAudioSource.spatialBlend = 1f;
        dollAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        dollAudioSource.minDistance = 6f;
        dollAudioSource.maxDistance = 35f;
        EditorUtility.SetDirty(dollAudioSource);

        SetPrivate(dollPickup, "dollItem", dollItem);
        SetPrivate(dollPickup, "linhVoiceClip", linh02);
        SetPrivate(dollPickup, "dollAudioSource", dollAudioSource);
        SetPrivate(dollPickup, "audioSpatialBlend", 1f);
        SetPrivate(dollPickup, "audioMinDistance", 6f);
        SetPrivate(dollPickup, "audioMaxDistance", 35f);
        SetPrivate(dollPickup, "audioPlayingMessage", "Đang phát nhạc...");
        SetPrivate(dollPickup, "audioMessageRefreshSeconds", 1.1f);
        SetPrivate(dollPickup, "inspectRoot", doll.transform);
        SetPrivate(dollPickup, "interactText", "[E] Cầm búp bê");
        SetPrivate(dollPickup, "heldLocalPosition", new Vector3(0f, -0.18f, 0.68f));
        SetPrivate(dollPickup, "heldLocalEulerAngles", new Vector3(12f, 0f, 0f));
        SetPrivate(dollPickup, "moveSeconds", 0.18f);
        SetPrivate(dollPickup, "mouseRotationDegreesPerPixel", 0.18f);
        EditorUtility.SetDirty(dollPickup);

        var cabinetCollider = cabinet.GetComponent<BoxCollider>();
        if (cabinetCollider == null)
            cabinetCollider = cabinet.gameObject.AddComponent<BoxCollider>();
        cabinetCollider.isTrigger = false;
        cabinetCollider.center = new Vector3(0f, 0f, -0.12f);
        cabinetCollider.size = new Vector3(1.42f, 1.75f, 0.78f);
        EditorUtility.SetDirty(cabinetCollider);

        var lockedCabinet = cabinet.GetComponent<P2LockedCabinet>();
        if (lockedCabinet == null)
            lockedCabinet = cabinet.gameObject.AddComponent<P2LockedCabinet>();
        SetPrivate(lockedCabinet, "requiredKey", keyItem);
        SetPrivate(lockedCabinet, "closedVisual", closedVisual);
        SetPrivate(lockedCabinet, "openVisual", openVisual);
        SetPrivate(lockedCabinet, "contentsRoot", contents);
        SetPrivate(lockedCabinet, "useKeyFirstText", "Hãy sử dụng chìa khóa tủ trong hành trang trước.");
        SetPrivate(lockedCabinet, "wrongKeyText", "Chìa khóa này không mở được tủ.");
        SetPrivate(lockedCabinet, "lockedText", "Tủ bị khóa. Cần chìa khóa tủ.");
        SetPrivate(lockedCabinet, "openText", "[E] Mở tủ");
        SetPrivate(lockedCabinet, "openedText", "Đã mở tủ. Bên trong có một con búp bê.");
        SetPrivate(lockedCabinet, "consumeKey", false);
        closedVisual.SetActive(true);
        openVisual.SetActive(false);
        contents.SetActive(false);
        EditorUtility.SetDirty(lockedCabinet);
    }

    private static void ConfigureUpperFloorLinhTrigger(AudioClip linh01)
    {
        var blocker = FindSceneTransform("UpperFloorBlocker_AfterGramophone");
        if (blocker == null)
            return;

        var component = blocker.GetComponent<UpperFloorBlocker>();
        if (component == null)
            component = blocker.gameObject.AddComponent<UpperFloorBlocker>();

        foreach (var collider in blocker.GetComponents<BoxCollider>())
        {
            collider.enabled = true;
            collider.isTrigger = true;
            EditorUtility.SetDirty(collider);
        }

        SetPrivate(component, "triggerOnlyMode", true);
        SetPrivate(component, "triggerVoiceClip", linh01);
        SetPrivate(component, "triggerSubtitle", "Má ơi... con thấy nó lại rồi. Trong cái gương ở phòng tắm...");
        SetPrivate(component, "showMessageOnlyOnce", true);
        SetPrivate(component, "triggerSubtitle", string.Empty);
        EditorUtility.SetDirty(component);
    }

    private static ItemData EnsureItemData(
        string path,
        string id,
        string itemName,
        string description,
        ItemType itemType,
        string keyId,
        bool canUse)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.id = id;
        item.itemName = itemName;
        item.description = description;
        item.itemType = itemType;
        item.canStack = false;
        item.maxStack = 1;
        item.canUse = canUse;
        item.keyID = keyId;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static GameObject EnsurePrimitive(Transform parent, string name, PrimitiveType primitiveType)
    {
        var existing = FindSceneTransform(name);
        if (existing != null)
        {
            existing.SetParent(parent, true);
            return existing.gameObject;
        }

        var obj = GameObject.CreatePrimitive(primitiveType);
        obj.name = name;
        obj.transform.SetParent(parent, true);
        return obj;
    }

    private static Transform EnsureRoot(string name)
    {
        var root = FindSceneTransform(name);
        if (root != null)
            return root;

        return new GameObject(name).transform;
    }

    private static Transform EnsureRootChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
            return existing;

        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        SetMaterial(cube, material);

        var collider = cube.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider, true);

        return cube;
    }

    private static void ClearPrimitiveChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject, true);
    }

    private static Collider EnsureCollider(GameObject obj, bool isTrigger)
    {
        var collider = obj.GetComponent<Collider>();
        if (collider == null)
            collider = obj.AddComponent<BoxCollider>();

        collider.isTrigger = isTrigger;
        EditorUtility.SetDirty(collider);
        return collider;
    }

    private static void SetMaterial(GameObject obj, Material material)
    {
        if (material == null)
            return;

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static Materials EnsureMaterials()
    {
        return new Materials
        {
            Wood = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_Wood.mat", new Color(0.35f, 0.22f, 0.13f)),
            DarkWood = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_DarkWood.mat", new Color(0.18f, 0.12f, 0.08f)),
            Brass = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_Brass.mat", new Color(0.95f, 0.72f, 0.32f)),
            FadedCloth = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_FadedCloth.mat", new Color(0.55f, 0.18f, 0.22f)),
            Pale = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_Pale.mat", new Color(0.78f, 0.67f, 0.58f))
        };
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

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName)
                return transform;
        }

        return null;
    }

    private static void SetPrivate(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivate(Object target, string propertyName, string value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivate(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivate(Object target, string propertyName, bool value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivate(Object target, string propertyName, float value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivate(Object target, string propertyName, Vector3 value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private sealed class Materials
    {
        public Material Wood;
        public Material DarkWood;
        public Material Brass;
        public Material FadedCloth;
        public Material Pale;
    }
}
