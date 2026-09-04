using FpsHorrorKit;
using MainGame.P2;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2JournalPaperSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string JournalObjectName = "NhatKy";
    private const string JournalTextName = "TextNhatKy";
    private const string ReactionSubtitle = "Gió không đổi hướng. Phòng bé Linh. Tường phía tây.";

    [MenuItem("MainGame/P2/Apply P2 Journal Paper Rules")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        ConfigureJournal();
        DisableChapterOneGramophoneFlow();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("P2 journal paper rules applied.");
    }

    private static void ConfigureJournal()
    {
        var journal = FindSceneTransform(JournalObjectName);
        if (journal == null)
        {
            Debug.LogWarning("Could not find P2 journal object named NhatKy.");
            return;
        }

        foreach (var oldJournal in journal.GetComponents<JournalInteractable>())
            Object.DestroyImmediate(oldJournal, true);

        var paper = journal.GetComponent<P2JournalPaperInteractable>();
        if (paper == null)
            paper = journal.gameObject.AddComponent<P2JournalPaperInteractable>();

        var text = FindChildByName(journal, JournalTextName)?.GetComponent<TextMeshProUGUI>();
        var audioData = Resources.Load<AudioData>("Audio/AudioData");
        var serialized = new SerializedObject(paper);
        Set(serialized, "paperRoot", journal);
        Set(serialized, "paperText", text);
        Set(serialized, "targetCamera", Camera.main);
        Set(serialized, "interactText", "[E] Đọc nhật ký");
        Set(serialized, "heldLocalPosition", new Vector3(0f, -0.16f, 0.58f));
        Set(serialized, "heldLocalEulerAngles", new Vector3(68f, 0f, 0f));
        Set(serialized, "overrideHeldScale", false);
        Set(serialized, "ngocReturnVoiceClip", audioData != null ? audioData.p2Ngoc04 : null);
        Set(serialized, "ngocReturnSubtitle", ReactionSubtitle);
        Set(serialized, "fallbackSubtitleSeconds", 4f);
        Set(serialized, "playReactionOnce", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(paper);
        EditorUtility.SetDirty(journal.gameObject);
    }

    private static void DisableChapterOneGramophoneFlow()
    {
        foreach (var flow in Object.FindObjectsByType<ChapterOneStoryFlow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (flow == null || !flow.gameObject.scene.IsValid())
                continue;

            flow.enabled = false;
            EditorUtility.SetDirty(flow);
        }

        foreach (var tapePlayer in Object.FindObjectsByType<GramophoneTapePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tapePlayer == null || !tapePlayer.gameObject.scene.IsValid())
                continue;

            tapePlayer.enabled = false;
            EditorUtility.SetDirty(tapePlayer);

            foreach (var source in tapePlayer.GetComponents<AudioSource>())
            {
                source.Stop();
                source.playOnAwake = false;
                source.enabled = false;
                EditorUtility.SetDirty(source);
            }
        }
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

    private static Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            var found = FindChildByName(child, childName);
            if (found != null)
                return found;
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

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, Vector3 value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }
}
