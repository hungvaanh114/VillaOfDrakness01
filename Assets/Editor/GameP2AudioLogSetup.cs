using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2AudioLogSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string AudioLogPrefabPath = "Assets/MainGame/Prefab/PrefabsOld/Interactables/Prop_MusicBox_Cylinder.prefab";
    private const string BL02ClipPath = "Assets/MainGame/Audio/Phan 2/Mấy đêm nay tôi-197da84be4_a84be4.mp3";
    private const string BL03ClipPath = "Assets/MainGame/Audio/Phan 2/Đây_là_chỗ_duy_nhất_3201f8dbc67d.mp3";

    private const string BL02Text =
        "Mấy đêm nay tôi không sao ngủ được. Có tiếng gõ vào mặt gương lúc nửa đêm... không mạnh, chỉ như móng tay gõ nhẹ, nhưng đều đều, không dứt. Tôi đã thử không phủ vải một đêm... chỉ để coi thử. Tôi không nên làm vậy. Tôi thấy nó ở trong đó... đứng yên, nhìn ngược lại tôi lâu hơn tôi dám nhìn nó. Bài nhạc... tôi mới ghi được năm nốt đầu. Hai nốt cuối... tôi không biết mình còn đủ can đảm để tìm ra hay không.";

    private const string BL03Text =
        "Đây là chỗ duy nhất trong nhà tôi còn thấy yên tâm để viết. Năm nốt... tôi khắc bằng phấn để nhớ, vì tôi sợ trí nhớ của mình không còn đáng tin nữa. Nhưng phấn hết ngay sau khi tôi viết xong nốt thứ năm. Tôi định đi tìm phấn khác... nhưng có lẽ tôi không còn kịp nữa.";

    [MenuItem("MainGame/P2/Apply P2 Audio Logs")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        ApplyAudioData();

        var root = EnsureRoot("P2_AudioLogs");
        EnsureAudioLog(
            root,
            "BL_LOG_02_MechanicalRecorder",
            BL02ClipPath,
            BL02Text,
            "[E] Hộp ghi âm",
            new Vector3(9.1f, 4.05f, 7.3f),
            Quaternion.Euler(0f, 35f, 0f),
            true);

        EnsureAudioLog(
            root,
            "BL_LOG_03_LinhRoomRecorder",
            BL03ClipPath,
            BL03Text,
            "[E] Hộp ghi âm",
            new Vector3(2.2f, 3.25f, -3.1f),
            Quaternion.Euler(0f, -20f, 0f),
            true);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("P2 audio log recorders applied.");
    }

    private static void ApplyAudioData()
    {
        var data = AssetDatabase.LoadAssetAtPath<AudioData>("Assets/MainGame/Resources/Audio/AudioData.asset");
        if (data == null)
            return;

        var serialized = new SerializedObject(data);
        Set(serialized, "p2AudioLogBL02", AssetDatabase.LoadAssetAtPath<AudioClip>(BL02ClipPath));
        Set(serialized, "p2AudioLogBL03", AssetDatabase.LoadAssetAtPath<AudioClip>(BL03ClipPath));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static void EnsureAudioLog(
        Transform parent,
        string objectName,
        string clipPath,
        string subtitle,
        string interactText,
        Vector3 position,
        Quaternion rotation,
        bool canReplay)
    {
        bool created = false;
        var transform = FindSceneTransform(objectName);
        if (transform == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioLogPrefabPath);
            GameObject instance = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene())
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            instance.name = objectName;
            transform = instance.transform;
            created = true;
        }

        transform.SetParent(parent, true);
        if (created)
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        RemoveP1AudioLogComponents(transform.gameObject);
        var collider = transform.GetComponent<Collider>();
        if (collider == null)
            collider = transform.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        EditorUtility.SetDirty(collider);

        var source = transform.GetComponent<AudioSource>();
        if (source == null)
            source = transform.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        EditorUtility.SetDirty(source);

        var item = transform.GetComponent<P2AudioLogItem>();
        if (item == null)
            item = transform.gameObject.AddComponent<P2AudioLogItem>();

        var serialized = new SerializedObject(item);
        Set(serialized, "clip", AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath));
        Set(serialized, "subtitle", subtitle);
        Set(serialized, "interactText", interactText);
        Set(serialized, "canReplay", canReplay);
        Set(serialized, "fallbackSource", source);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        EditorUtility.SetDirty(transform.gameObject);
    }

    private static void RemoveP1AudioLogComponents(GameObject target)
    {
        foreach (var component in target.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            var type = component.GetType();
            if (type.Name != "AudioLogItem" && (type.FullName == null || !type.FullName.Contains("AudioLogItem")))
                continue;

            Object.DestroyImmediate(component, true);
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
            if (transform != null && transform.name == objectName)
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

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, bool value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }
}
