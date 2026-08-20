using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OuttroCutSceneSequenceSetup
{
    private const string MenuPath = "Tools/MainGame/Setup Outtro CutScene Sequence";

    [MenuItem(MenuPath)]
    public static void CreateOrUpdateOuttro()
    {
        var manager = Object.FindFirstObjectByType<CutSceneManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            var managerObject = new GameObject("CutSceneManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create CutSceneManager");
            manager = managerObject.AddComponent<CutSceneManager>();
        }

        Transform root = manager.transform;
        Transform outtro = FindOrCreateChild(root, "Outtro", Vector3.zero);

        Transform p1 = FindOrCreateChild(outtro, "OuttroPlayerPoint_01", new Vector3(-6.2f, 2.44f, 20.8f));
        Transform p2 = FindOrCreateChild(outtro, "OuttroPlayerPoint_02", new Vector3(-4.25f, 2.44f, 23.9f));
        Transform lookWell = FindOrCreateChild(outtro, "OuttroPlayerLookWell", new Vector3(-4.25f, 2.44f, 25.85f));
        Transform cameraBackHigh = FindOrCreateChild(outtro, "OuttroCameraBackHigh", new Vector3(-4.25f, 6.2f, 22.2f));
        Transform cameraWellMouth = FindOrCreateChild(outtro, "OuttroCameraWellMouth", new Vector3(-4.25f, 3.85f, 27.15f));
        Transform cameraLookAt = FindOrCreateChild(outtro, "OuttroCameraLookAt", new Vector3(-4.245471f, 2.82f, 28.603832f));
        Transform dropPoint = FindOrCreateChild(outtro, "OuttroGramophoneDropPoint", new Vector3(-2.9f, 2.52f, 27.85f));
        dropPoint.rotation = Quaternion.Euler(0f, 35f, -8f);

        var sequence = outtro.GetComponent<CutSceneSequence>();
        if (sequence == null)
            sequence = outtro.gameObject.AddComponent<CutSceneSequence>();

        var points = new List<CutScenePoint>
        {
            new()
            {
                name = "Chạy ra sân sau",
                point = p1,
                moveToPoint = true,
                moveSpeedOverride = 5.8f,
                turnSpeedOverride = 720f,
                overrideText = "\"Khoa tìm đường ra sân sau, hộp nhạc vẫn ôm chặt trong tay.\"",
                overrideFallbackDuration = 2.2f,
                waitAfter = 0.1f,
                cameraShot = CutSceneCameraShot.BehindShoulder,
                cameraPositionOverride = cameraBackHigh,
                cameraLookAtPlayer = true,
                cameraLookHeight = 1.25f,
                cameraSmoothTimeOverride = 0.18f
            },
            new()
            {
                name = "Áp sát giếng",
                point = p2,
                moveToPoint = true,
                moveSpeedOverride = 5.8f,
                turnSpeedOverride = 720f,
                overrideText = string.Empty,
                overrideFallbackDuration = 1.1f,
                waitAfter = 0.05f,
                cameraShot = CutSceneCameraShot.DescendBehind,
                cameraPositionOverride = cameraBackHigh,
                cameraLookAtPlayer = true,
                cameraLookHeight = 1.25f,
                cameraSmoothTimeOverride = 0.16f
            },
            new()
            {
                name = "Nhìn xuống giếng",
                point = lookWell,
                moveToPoint = true,
                moveSpeedOverride = 4.4f,
                turnSpeedOverride = 720f,
                overrideText = "\"Cái gì vậy... ánh sáng trong giếng?\"",
                overrideFallbackDuration = 2.3f,
                waitAfter = 0.35f,
                cameraShot = CutSceneCameraShot.WindowInspect,
                cameraPositionOverride = cameraWellMouth,
                cameraLookAtOverride = cameraLookAt,
                cameraLookAtPlayer = false,
                cameraLookHeight = 0f,
                cameraSmoothTimeOverride = 0.2f
            },
            new()
            {
                name = "Mặt giếng phản chiếu",
                point = lookWell,
                moveToPoint = false,
                overrideText = "\"Ơ... cái gì-\"",
                overrideFallbackDuration = 0.85f,
                waitAfter = 0.75f,
                cameraShot = CutSceneCameraShot.WindowInspect,
                cameraPositionOverride = cameraWellMouth,
                cameraLookAtOverride = cameraLookAt,
                cameraLookAtPlayer = false,
                cameraLookHeight = 0f,
                cameraSmoothTimeOverride = 0.12f
            }
        };

        sequence.Configure("outtro", false, null, false, points);

        AssignWellEnding(manager, dropPoint);

        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Outtro CutSceneSequence configured under CutSceneManager/Outtro.");
    }

    private static Transform FindOrCreateChild(Transform parent, string name, Vector3 worldPosition)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        var childObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(childObject, "Create " + name);
        child = childObject.transform;
        child.SetParent(parent, true);
        child.position = worldPosition;
        return child;
    }

    private static void AssignWellEnding(CutSceneManager manager, Transform dropPoint)
    {
        var ending = Object.FindFirstObjectByType<WellEndingTrigger>(FindObjectsInactive.Include);
        if (ending == null)
            return;

        var serializedEnding = new SerializedObject(ending);
        SetObject(serializedEnding, "cutSceneManager", manager);
        SetString(serializedEnding, "outtroCutSceneId", "outtro");
        SetObject(serializedEnding, "gramophoneDropPoint", dropPoint);
        serializedEnding.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ending);
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }
}
