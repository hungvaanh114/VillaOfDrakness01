using System.Collections.Generic;
using FpsHorrorKit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainGameInventoryProgressionSetup
{
    private const string ScenePath = "Assets/MainGame/Game.unity";
    private const string DataFolder = "Assets/MainGame/Data/InventoryProgression";
    private const string PrefabFolder = "Assets/MainGame/Prefab";
    private const string MaterialFolder = "Assets/MainGame/Materials/InventoryProgression";
    private const string AutoBuildSessionKey = "MainGameInventoryProgressionSetup.AutoBuildComplete";

    [InitializeOnLoadMethod]
    private static void AutoBuildOnceIfMissing()
    {
        if (SessionState.GetBool(AutoBuildSessionKey, false))
            return;
        if (AssetDatabase.LoadAssetAtPath<ItemData>($"{DataFolder}/Item_Battery.asset") != null)
            return;

        SessionState.SetBool(AutoBuildSessionKey, true);
        EditorApplication.delayCall += Build;
    }

    [MenuItem("Tools/MainGame/Build Inventory Progression System")]
    [MenuItem("Assets/MainGame/Build Inventory Progression System")]
    public static void Build()
    {
        EnsureFolder("Assets/MainGame/Data", "InventoryProgression");
        EnsureFolder("Assets/MainGame/Materials", "InventoryProgression");
        EnsureFolder("Assets/MainGame", "Prefab");

        var scene = EditorSceneManager.OpenScene(ScenePath);

        var sprites = LoadSprites();
        var itemAssets = CreateItems(sprites);
        var musicAssets = CreateMusicSheets(sprites);
        var journalEntry = CreateJournalEntry();
        CreatePrefabs(itemAssets, musicAssets);
        SetupScene(itemAssets, musicAssets, journalEntry, sprites);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Inventory + Journal + Music Sheet + Key/Door + Battery + Piano progression setup completed.");
    }

    private static Dictionary<string, Sprite> LoadSprites()
    {
        return new Dictionary<string, Sprite>
        {
            ["khung"] = SpriteAt("Assets/MainGame/UI/Inventory/khung.png"),
            ["khungThongTin"] = SpriteAt("Assets/MainGame/UI/Inventory/khungThongTin.png"),
            ["khungVatPham"] = SpriteAt("Assets/MainGame/UI/Inventory/khungVatPham.png"),
            ["KhungVatPhamChon"] = SpriteAt("Assets/MainGame/UI/Inventory/KhungVatPhamChon.png"),
            ["nut"] = SpriteAt("Assets/MainGame/UI/Inventory/nut.png"),
            ["nutChon"] = SpriteAt("Assets/MainGame/UI/Inventory/nutChon.png"),
            ["thoat"] = SpriteAt("Assets/MainGame/UI/Inventory/thoat.png"),
            ["denPin"] = SpriteAt("Assets/MainGame/UI/Inventory/denPin.png"),
            ["chiaKhoa"] = SpriteAt("Assets/MainGame/UI/Inventory/chiaKhoa.png"),
            ["divider"] = SpriteAt("Assets/MainGame/UI/Sprites/divider.png"),
            ["keycap"] = SpriteAt("Assets/MainGame/UI/Sprites/keycap.png"),
            ["slot"] = SpriteAt("Assets/MainGame/UI/Sprites/slot.png"),
            ["slot_selected"] = SpriteAt("Assets/MainGame/UI/Sprites/slot_selected.png"),
            ["panel"] = SpriteAt("Assets/MainGame/UI/Sprites/panel.png"),
            ["notDo"] = SpriteAt("Assets/MainGame/UI/Nhac/notDo.png"),
            ["notRe"] = SpriteAt("Assets/MainGame/UI/Nhac/notRe.png"),
            ["notMi"] = SpriteAt("Assets/MainGame/UI/Nhac/notMi.png"),
            ["notFa"] = SpriteAt("Assets/MainGame/UI/Nhac/notFa.png"),
            ["notSol"] = SpriteAt("Assets/MainGame/UI/Nhac/notSol.png"),
            ["danPiano"] = SpriteAt("Assets/MainGame/UI/Nhac/danPiano.png"),
            ["phimDan"] = SpriteAt("Assets/MainGame/UI/Nhac/phimDan.png")
        };
    }

    private static Dictionary<string, ItemData> CreateItems(Dictionary<string, Sprite> sprites)
    {
        var items = new Dictionary<string, ItemData>
        {
            ["Flashlight"] = Item("Item_Flashlight", "Đèn pin", "Một chiếc đèn pin cầm tay.\nVẫn hoạt động tốt, có thể soi sáng trong bóng tối.", ItemType.Flashlight, sprites["denPin"], false, 1, true, ""),
            ["Battery"] = Item("Item_Battery", "Pin đèn pin", "Một viên pin còn điện.\nCó thể dùng để sạc lại đèn pin.", ItemType.Battery, null, true, 8, true, ""),
            ["GuestKey"] = Item("Item_Key_Guest", "Chìa Tiệp Khách", "Chìa khóa cũ có ký hiệu phòng tiếp khách.", ItemType.Key, sprites["chiaKhoa"], false, 1, true, "GuestKey"),
            ["SalonKey"] = Item("Item_Key_Salon", "Chìa Salon", "Chìa khóa đồng mở khu salon.", ItemType.Key, sprites["chiaKhoa"], false, 1, true, "SalonKey"),
            ["BackyardKey"] = Item("Item_Key_Backyard", "Chìa Sân Sau", "Chìa khóa dẫn ra khu sân sau.", ItemType.Key, sprites["chiaKhoa"], false, 1, true, "BackyardKey")
        };

        return items;
    }

    private static Dictionary<string, MusicSheetData> CreateMusicSheets(Dictionary<string, Sprite> sprites)
    {
        return new Dictionary<string, MusicSheetData>
        {
            ["MusicSheet_01"] = MusicSheet("MusicSheet_01", 1, sprites["notDo"], "Mảnh bản nhạc đầu tiên, nốt Đô."),
            ["MusicSheet_02"] = MusicSheet("MusicSheet_02", 2, sprites["notRe"], "Mảnh bản nhạc thứ hai, nốt Rê."),
            ["MusicSheet_03"] = MusicSheet("MusicSheet_03", 3, sprites["notMi"], "Mảnh bản nhạc thứ ba, nốt Mi."),
            ["MusicSheet_04"] = MusicSheet("MusicSheet_04", 4, sprites["notFa"], "Mảnh bản nhạc thứ tư, nốt Fa."),
            ["MusicSheet_05"] = MusicSheet("MusicSheet_05", 5, sprites["notSol"], "Mảnh bản nhạc cuối cùng, nốt Sol.")
        };
    }

    private static JournalEntryData CreateJournalEntry()
    {
        var entry = AssetAt<JournalEntryData>($"{DataFolder}/Journal_Intro.asset");
        if (entry == null)
        {
            entry = ScriptableObject.CreateInstance<JournalEntryData>();
            AssetDatabase.CreateAsset(entry, $"{DataFolder}/Journal_Intro.asset");
        }

        entry.id = "Journal_Intro";
        entry.title = "Biệt thự bỏ hoang";
        entry.content = "Những cánh cửa trong biệt thự dường như không chỉ cần chìa khóa. Có một bản nhạc bị xé thành nhiều mảnh, và cây đàn piano đang chờ ai đó đánh đúng giai điệu.";
        EditorUtility.SetDirty(entry);
        return entry;
    }

    private static ItemData Item(string id, string name, string description, ItemType type, Sprite icon, bool stack, int maxStack, bool canUse, string keyID)
    {
        var item = AssetAt<ItemData>($"{DataFolder}/{id}.asset");
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, $"{DataFolder}/{id}.asset");
        }

        item.id = id;
        item.itemName = name;
        item.description = description;
        item.itemType = type;
        item.icon = icon;
        item.canStack = stack;
        item.maxStack = maxStack;
        item.canUse = canUse;
        item.keyID = keyID;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static MusicSheetData MusicSheet(string id, int index, Sprite icon, string description)
    {
        var sheet = AssetAt<MusicSheetData>($"{DataFolder}/{id}.asset");
        if (sheet == null)
        {
            sheet = ScriptableObject.CreateInstance<MusicSheetData>();
            AssetDatabase.CreateAsset(sheet, $"{DataFolder}/{id}.asset");
        }

        sheet.musicSheetID = id;
        sheet.index = index;
        sheet.icon = icon;
        sheet.description = description;
        EditorUtility.SetDirty(sheet);
        return sheet;
    }

    private static void CreatePrefabs(Dictionary<string, ItemData> items, Dictionary<string, MusicSheetData> sheets)
    {
        CreateItemPrefab("Item_Battery", Color.green, items["Battery"], 1f);
        CreateItemPrefab("Item_Key_Guest", new Color(1f, 0.78f, 0.16f), items["GuestKey"], 0.7f);
        CreateItemPrefab("Item_Key_Salon", new Color(1f, 0.68f, 0.08f), items["SalonKey"], 0.7f);
        CreateItemPrefab("Item_Key_Backyard", new Color(0.88f, 0.6f, 0.05f), items["BackyardKey"], 0.7f);
        CreateMusicPrefab("MusicSheet_01", new Color(1f, 0.96f, 0.78f), sheets["MusicSheet_01"]);
        CreateMusicPrefab("MusicSheet_02", new Color(1f, 0.96f, 0.78f), sheets["MusicSheet_02"]);
        CreateMusicPrefab("MusicSheet_03", new Color(1f, 0.96f, 0.78f), sheets["MusicSheet_03"]);
        CreateMusicPrefab("MusicSheet_04", new Color(1f, 0.96f, 0.78f), sheets["MusicSheet_04"]);
        CreateMusicPrefab("MusicSheet_05", new Color(1f, 0.96f, 0.78f), sheets["MusicSheet_05"]);

        foreach (var item in items.Values)
        {
            if (item.itemType == ItemType.Flashlight)
                continue;
            var prefab = AssetAt<GameObject>($"{PrefabFolder}/{PrefabNameFor(item)}.prefab");
            item.worldPrefab = prefab;
            item.heldPrefab = prefab;
            EditorUtility.SetDirty(item);
        }
    }

    private static void CreateItemPrefab(string name, Color color, ItemData item, float scale)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = name;
        temp.transform.localScale = Vector3.one * scale;
        temp.GetComponent<Renderer>().sharedMaterial = Material(name, color);
        var pickup = temp.AddComponent<ItemPickup>();
        SetObject(pickup, "itemData", item);
        SetObject(pickup, "interactText", $"[E] Nhặt {item.itemName}");
        SavePrefab(temp, path);
    }

    private static void CreateMusicPrefab(string name, Color color, MusicSheetData sheet)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = name;
        temp.transform.localScale = new Vector3(0.7f, 0.08f, 0.5f);
        temp.GetComponent<Renderer>().sharedMaterial = Material(name, color);
        var pickup = temp.AddComponent<MusicSheetPickup>();
        SetObject(pickup, "musicSheetData", sheet);
        SavePrefab(temp, path);
    }

    private static void SetupScene(Dictionary<string, ItemData> items, Dictionary<string, MusicSheetData> sheets, JournalEntryData journalEntry, Dictionary<string, Sprite> sprites)
    {
        var systems = GameObject.Find("MainGameSystems");
        if (systems == null) systems = new GameObject("MainGameSystems");

        var inventoryManager = EnsureComponent<InventoryManager>(systems);
        SetObject(inventoryManager, "startingFlashlight", items["Flashlight"]);

        var heldItemController = EnsureComponent<HeldItemController>(systems);
        EnsureComponent<GameProgressManager>(systems);
        var pianoPuzzle = EnsureComponent<PianoPuzzle>(systems);
        SetStringArray(pianoPuzzle, "requiredMelody", new[] { "C", "D", "E", "F", "G" });

        var musicManager = EnsureComponent<MusicSheetManager>(systems);
        SetObject(musicManager, "requiredMusicSheetCount", 5);
        SetList(musicManager, "sheets", new List<Object>(sheets.Values));

        var journalManager = EnsureComponent<JournalManager>(systems);
        SetList(journalManager, "startingEntries", new List<Object> { journalEntry });

        var canvas = MainGameEditorCanvasUtility.FindOrCreateScreenCanvas();

        var inventoryUIObject = FindOrCreateChild(canvas.transform, "InventoryUIRoot");
        var inventoryUI = EnsureComponent<InventoryUI>(inventoryUIObject);
        SetInventoryUISprites(inventoryUI, sprites);

        var pianoUIObject = FindOrCreateChild(canvas.transform, "PianoPuzzleUIRoot");
        var pianoUI = EnsureComponent<PianoPuzzleUI>(pianoUIObject);
        SetObject(pianoUI, "panelSprite", sprites["khungThongTin"]);
        SetObject(pianoUI, "pianoBackgroundSprite", sprites["danPiano"]);
        SetObject(pianoUI, "whiteKeySprite", sprites["phimDan"]);
        SetObject(pianoUI, "dividerSprite", sprites["divider"]);
        SetObject(pianoUI, "font", AssetAt<TMP_FontAsset>("Assets/MainGame/UI/Fonts/PatrickHand-Regular SDF.asset"));
        SetObject(pianoUI, "noteClip", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/c1.wav"));
        SetObject(pianoUI, "noteClipC", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/c1.wav"));
        SetObject(pianoUI, "noteClipD", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/d1.wav"));
        SetObject(pianoUI, "noteClipE", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/e1.wav"));
        SetObject(pianoUI, "noteClipF", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/f1.wav"));
        SetObject(pianoUI, "noteClipG", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/g1.wav"));
        SetObject(pianoUI, "noteClipA", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/a1.wav"));
        SetObject(pianoUI, "noteClipB", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/b1.wav"));
        SetObject(pianoUI, "noteClipHighC", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/c2.wav"));
        SetObject(pianoUI, "noteClipCSharp", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/c1s.wav"));
        SetObject(pianoUI, "noteClipDSharp", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/d1s.wav"));
        SetObject(pianoUI, "noteClipFSharp", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/f1s.wav"));
        SetObject(pianoUI, "noteClipGSharp", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/g1s.wav"));
        SetObject(pianoUI, "noteClipASharp", AssetAt<AudioClip>("Assets/MainGame/Audio/wav/a1s.wav"));

        SetObject(heldItemController, "itemHoldPoint", EnsureHoldPoint());
        PlaceTestObjects(items, sheets);
        SetupFirstDoor();
    }

    private static void SetInventoryUISprites(InventoryUI ui, Dictionary<string, Sprite> sprites)
    {
        SetObject(ui, "backgroundSprite", sprites["khung"]);
        SetObject(ui, "panelSprite", sprites["khungVatPham"]);
        SetObject(ui, "infoPanelSprite", sprites["khungThongTin"]);
        SetObject(ui, "slotSprite", sprites["slot"] != null ? sprites["slot"] : sprites["khungVatPham"]);
        SetObject(ui, "selectedSlotSprite", sprites["slot_selected"] != null ? sprites["slot_selected"] : sprites["KhungVatPhamChon"]);
        SetObject(ui, "tabSprite", sprites["nut"]);
        SetObject(ui, "selectedTabSprite", sprites["nutChon"]);
        SetObject(ui, "closeSprite", sprites["thoat"]);
        SetObject(ui, "dividerSprite", sprites["divider"]);
        SetObject(ui, "keycapSprite", sprites["keycap"]);
        SetObject(ui, "unknownMusicSprite", sprites["notSol"]);
        SetObject(ui, "flashlightFallbackIcon", sprites["denPin"]);
        SetObject(ui, "keyFallbackIcon", sprites["chiaKhoa"]);
        SetObject(ui, "titleFont", AssetAt<TMP_FontAsset>("Assets/MainGame/UI/Fonts/ProtestGuerrilla-Regular SDF.asset"));
        SetObject(ui, "bodyFont", AssetAt<TMP_FontAsset>("Assets/MainGame/UI/Fonts/PatrickHand-Regular SDF.asset"));
    }

    private static void PlaceTestObjects(Dictionary<string, ItemData> items, Dictionary<string, MusicSheetData> sheets)
    {
        var player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position + player.transform.forward * 3f + Vector3.up * 0.7f : new Vector3(0f, 1f, 0f);
        string[] itemOrder = { "Battery", "GuestKey", "SalonKey", "BackyardKey" };
        for (int i = 0; i < itemOrder.Length; i++)
            PlacePrefab($"Test_{PrefabNameFor(items[itemOrder[i]])}", items[itemOrder[i]].worldPrefab, origin + new Vector3((i - 1.5f) * 1.2f, 0f, 0f));

        int musicIndex = 0;
        foreach (var sheet in sheets.Values)
        {
            var prefab = AssetAt<GameObject>($"{PrefabFolder}/{sheet.musicSheetID}.prefab");
            PlacePrefab($"Test_{sheet.musicSheetID}", prefab, origin + new Vector3((musicIndex - 1.5f) * 1.2f, 0f, 1.35f));
            musicIndex++;
        }

        var piano = GameObject.Find("Test_PianoPuzzle");
        if (piano == null)
        {
            piano = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piano.name = "Test_PianoPuzzle";
            piano.transform.localScale = new Vector3(2.2f, 1f, 0.7f);
            piano.GetComponent<Renderer>().sharedMaterial = Material("Piano_Test_Mat", new Color(0.18f, 0.09f, 0.05f));
            piano.AddComponent<PianoInteractable>();
        }
        piano.transform.position = origin + new Vector3(0f, 0f, 3f);
    }

    private static void SetupFirstDoor()
    {
        var doors = Object.FindObjectsByType<DoorSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (doors.Length == 0)
            return;

        var door = doors[0];
        door.isLocked = true;
        door.requiredKeyID = "GuestKey";
        door.consumeKeyOnUse = false;
        EditorUtility.SetDirty(door);
    }

    private static Transform EnsureHoldPoint()
    {
        var leftHand = FindSceneTransform("LeftHandProp") ?? FindSceneTransform("LeftHand");
        if (leftHand == null)
            return null;

        var holdPoint = leftHand.Find("ItemHoldPoint");
        if (holdPoint == null)
        {
            var obj = new GameObject("ItemHoldPoint");
            holdPoint = obj.transform;
            holdPoint.SetParent(leftHand, false);
        }

        holdPoint.localPosition = new Vector3(0.02f, 0.04f, 0.02f);
        holdPoint.localEulerAngles = new Vector3(0f, 90f, 0f);
        return holdPoint;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == objectName && transform.gameObject.scene == EditorSceneManager.GetActiveScene())
                return transform;
        }

        return null;
    }

    private static void PlacePrefab(string sceneName, GameObject prefab, Vector3 position)
    {
        if (prefab == null)
            return;

        var existing = GameObject.Find(sceneName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = sceneName;
        instance.transform.position = position;
        EditorUtility.SetDirty(instance);
    }

    private static string PrefabNameFor(ItemData item)
    {
        return item.id.Replace("Item_Key_Guest", "Item_Key_Guest").Replace("Item_Key_Salon", "Item_Key_Salon").Replace("Item_Key_Backyard", "Item_Key_Backyard").Replace("Item_Battery", "Item_Battery");
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child.gameObject;

        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        if (component == null)
            component = target.AddComponent<T>();
        EditorUtility.SetDirty(component);
        return component;
    }

    private static Material Material(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetAt<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("HDRP/Lit") != null ? Shader.Find("HDRP/Lit") : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SavePrefab(GameObject temp, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
    }

    private static void SetObject(Object target, string propertyName, object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            return;

        if (value is Object objectValue) property.objectReferenceValue = objectValue;
        else if (value is string stringValue) property.stringValue = stringValue;
        else if (value is int intValue) property.intValue = intValue;
        else if (value is bool boolValue) property.boolValue = boolValue;
        else if (value is float floatValue) property.floatValue = floatValue;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetList(Object target, string propertyName, List<Object> values)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return;

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetStringArray(Object target, string propertyName, string[] values)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return;

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i];
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static T AssetAt<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static Sprite SpriteAt(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string fullPath = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, child);
    }
}
