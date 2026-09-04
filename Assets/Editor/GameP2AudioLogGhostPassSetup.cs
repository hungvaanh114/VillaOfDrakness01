using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2AudioLogGhostPassSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string RecorderName = "BL_LOG_02_MechanicalRecorder";
    private const string RootName = "P2_BL02_GhostPassPoints";
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

        DeleteLegacyGhostPassPoints();
        var sequence = recorder.GetComponent<P2AudioLogGhostPassSequence>();
        if (sequence == null)
            sequence = recorder.gameObject.AddComponent<P2AudioLogGhostPassSequence>();

        var audioData = Resources.Load<AudioData>("Audio/AudioData");
        var serialized = new SerializedObject(sequence);
        Set(serialized, "ghostDirector", Object.FindFirstObjectByType<P2GhostDoorApparitionDirector>(FindObjectsInactive.Include));
        Set(serialized, "delayAfterAudio", 0.25f);
        Set(serialized, "delayAfterGhostAppears", 0.35f);
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

    private static void DeleteLegacyGhostPassPoints()
    {
        var root = FindSceneTransform(RootName);
        if (root != null)
            Object.DestroyImmediate(root.gameObject);
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
