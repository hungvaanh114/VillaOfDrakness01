using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2AudioLogGhostPassSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string RecorderName = "BL_LOG_02_MechanicalRecorder";
    private const string RootName = "P2_BL02_GhostPassPoints";
    private const string StartName = "P2_BL02_GhostPass_Start";
    private const string EndName = "P2_BL02_GhostPass_End";
    private const string ReactionSubtitle = "Bà dặn đừng nhìn vào mặt nước. Đừng nhìn vào.";

    [MenuItem("MainGame/P2/Apply BL02 Ghost Pass Sequence")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var recorder = FindSceneTransform(RecorderName);
        if (recorder == null)
        {
            Debug.LogWarning("Could not find BL_LOG_02_MechanicalRecorder.");
            return;
        }

        var root = EnsureRoot();
        var start = EnsurePoint(StartName, root, recorder.position + new Vector3(-1.8f, 0f, 0.4f));
        var end = EnsurePoint(EndName, root, recorder.position + new Vector3(1.8f, 0f, 0.4f));
        var sequence = recorder.GetComponent<P2AudioLogGhostPassSequence>();
        if (sequence == null)
            sequence = recorder.gameObject.AddComponent<P2AudioLogGhostPassSequence>();

        var audioData = Resources.Load<AudioData>("Audio/AudioData");
        var serialized = new SerializedObject(sequence);
        Set(serialized, "ghostDirector", Object.FindFirstObjectByType<P2GhostDoorApparitionDirector>(FindObjectsInactive.Include));
        Set(serialized, "ghostPassStart", start);
        Set(serialized, "ghostPassEnd", end);
        Set(serialized, "delayAfterAudio", 0.25f);
        Set(serialized, "delayAfterGhostPass", 0.35f);
        Set(serialized, "triggerOnce", true);
        Set(serialized, "doorToOpenBeforePass", (Object)null);
        Set(serialized, "openDoorBeforePass", false);
        Set(serialized, "ngocReactionClip", audioData != null ? audioData.p2Ngoc05 : null);
        Set(serialized, "ngocReactionSubtitle", ReactionSubtitle);
        Set(serialized, "fallbackSubtitleSeconds", 3f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(recorder.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("BL02 ghost pass sequence applied.");
    }

    private static Transform EnsureRoot()
    {
        var root = FindSceneTransform(RootName);
        if (root != null)
            return root;

        return new GameObject(RootName).transform;
    }

    private static Transform EnsurePoint(string name, Transform root, Vector3 position)
    {
        var point = FindSceneTransform(name);
        if (point == null)
        {
            var obj = new GameObject(name);
            point = obj.transform;
            point.position = position;
            point.rotation = Quaternion.identity;
        }

        point.SetParent(root, true);
        EditorUtility.SetDirty(point.gameObject);
        return point;
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
}
