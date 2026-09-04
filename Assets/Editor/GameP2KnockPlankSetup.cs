using System;
using FpsHorrorKit;
using MainGame.P2;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameP2KnockPlankSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const string RootName = "P2_WestWall_KnockPlanks";
    private const string CavityName = "HiddenWallCavity_SilverMirror";
    private const string SilverMirrorName = "P2_SilverMirror_Item";
    private const string RecorderName = "BL_LOG_03_LinhRoomRecorder";
    private const string AlternateRecorderName = "BL_LOG_03_WindowRecorder";
    private const string CameraPointName = "P2_WallPlankZoom_CameraPoint";
    private const string LookTargetName = "P2_WallPlankZoom_LookTarget";

    private static readonly string[] NoteLabels = { "E", "C", "F", "D", "G", "Bộp" };
    private static readonly string[] NoteClipPaths =
    {
        "Assets/MainGame/Audio/wav/e1.wav",
        "Assets/MainGame/Audio/wav/c1.wav",
        "Assets/MainGame/Audio/wav/f1.wav",
        "Assets/MainGame/Audio/wav/d1.wav",
        "Assets/MainGame/Audio/wav/g1.wav"
    };

    [MenuItem("MainGame/P2/Apply P2 Knock Plank Puzzle")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var woodMaterial = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_Wood.mat", new Color(0.45f, 0.29f, 0.16f, 1f));
        var darkMaterial = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_DarkWood.mat", new Color(0.14f, 0.08f, 0.05f, 1f));
        var silverMaterial = EnsureMaterial("Assets/MainGame/Materials/InventoryProgression/P2_Block_SilverMirror.mat", new Color(0.72f, 0.78f, 0.82f, 1f));

        var root = EnsureRoot(RootName);
        var planks = EnsurePlanks(root, woodMaterial);
        var cavity = EnsureHiddenCavity(darkMaterial, silverMaterial);
        var puzzle = EnsurePuzzle(root, planks, cavity);
        var lookTarget = EnsurePoint(LookTargetName, root, new Vector3(1.08f, 3.74f, -2.9f));
        var cameraPoint = EnsurePoint(CameraPointName, root, new Vector3(3.35f, 3.95f, -2.9f));
        cameraPoint.rotation = Quaternion.LookRotation((lookTarget.position - cameraPoint.position).normalized, Vector3.up);
        EditorUtility.SetDirty(cameraPoint.gameObject);

        AttachZoomSequence(puzzle, cameraPoint, lookTarget);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("P2 knock plank puzzle applied.");
    }

    private static P2KnockPlank[] EnsurePlanks(Transform root, Material woodMaterial)
    {
        var planks = new P2KnockPlank[6];
        for (var i = 0; i < planks.Length; i++)
        {
            var plankName = $"P2_WestWall_Plank_{i + 1:00}_{SanitizeName(NoteLabels[i])}";
            var transform = FindSceneTransform(plankName) ?? FindSceneTransform("WestWall_KnockPanel_" + (i + 1));
            if (transform == null && i == planks.Length - 1)
                transform = FindSceneTransform("P2_WestWall_Plank_06_Bopp") ?? FindSceneTransform("P2_WestWall_Plank_06_Bop");
            if (transform == null)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = plankName;
                transform = obj.transform;
                transform.position = new Vector3(1.08f, 3.68f, -7.4f + i * 0.9f);
                transform.rotation = Quaternion.identity;
                transform.localScale = new Vector3(0.14f, 1.1f, 0.72f);
            }

            transform.name = plankName;
            transform.SetParent(root, true);

            var renderer = transform.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = woodMaterial;
                EditorUtility.SetDirty(renderer);
            }

            var collider = transform.GetComponent<Collider>();
            if (collider == null)
                collider = transform.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            EditorUtility.SetDirty(collider);

            RemoveOldP2Interactable(transform.gameObject);

            var plank = transform.GetComponent<P2KnockPlank>();
            if (plank == null)
                plank = transform.gameObject.AddComponent<P2KnockPlank>();
            plank.Configure(i, NoteLabels[i], i == planks.Length - 1);
            EditorUtility.SetDirty(plank);
            EditorUtility.SetDirty(transform.gameObject);
            planks[i] = plank;
        }

        var oldRoot = FindSceneTransform("LinhRoom_WestWallKnockPuzzle");
        if (oldRoot != null)
        {
            var oldPuzzle = oldRoot.GetComponent<P2WallKnockPuzzle>();
            if (oldPuzzle != null)
                UnityEngine.Object.DestroyImmediate(oldPuzzle, true);
            EditorUtility.SetDirty(oldRoot.gameObject);
        }

        return planks;
    }

    private static GameObject EnsureHiddenCavity(Material darkMaterial, Material silverMaterial)
    {
        var cavity = FindSceneTransform(CavityName);
        if (cavity == null)
        {
            var obj = new GameObject(CavityName);
            cavity = obj.transform;
        }

        cavity.position = Vector3.zero;
        cavity.rotation = Quaternion.identity;

        var back = FindChild(cavity, "Cavity_Back");
        if (back == null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Cavity_Back";
            back = obj.transform;
        }

        back.SetParent(cavity, true);
        back.position = new Vector3(1.0f, 3.83f, -2.9f);
        back.rotation = Quaternion.identity;
        back.localScale = new Vector3(0.08f, 1.0f, 0.72f);
        var backRenderer = back.GetComponent<Renderer>();
        if (backRenderer != null)
        {
            backRenderer.sharedMaterial = darkMaterial;
            EditorUtility.SetDirty(backRenderer);
        }

        var mirror = FindSceneTransform(SilverMirrorName);
        if (mirror == null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = SilverMirrorName;
            mirror = obj.transform;
        }

        mirror.SetParent(cavity, true);
        mirror.position = new Vector3(1.15f, 3.83f, -2.9f);
        mirror.rotation = Quaternion.Euler(0f, 90f, 0f);
        mirror.localScale = new Vector3(0.08f, 0.68f, 0.42f);
        var mirrorRenderer = mirror.GetComponent<Renderer>();
        if (mirrorRenderer != null)
        {
            mirrorRenderer.sharedMaterial = silverMaterial;
            EditorUtility.SetDirty(mirrorRenderer);
        }

        cavity.gameObject.SetActive(false);
        EditorUtility.SetDirty(back.gameObject);
        EditorUtility.SetDirty(mirror.gameObject);
        EditorUtility.SetDirty(cavity.gameObject);
        return cavity.gameObject;
    }

    private static P2KnockPlankPuzzle EnsurePuzzle(Transform root, P2KnockPlank[] planks, GameObject cavity)
    {
        var puzzle = root.GetComponent<P2KnockPlankPuzzle>();
        if (puzzle == null)
            puzzle = root.gameObject.AddComponent<P2KnockPlankPuzzle>();

        var serialized = new SerializedObject(puzzle);
        SetArray(serialized, "planks", planks);
        Set(serialized, "requireSequentialClicks", true);
        Set(serialized, "hollowPlankIndex", 5);
        SetArray(serialized, "noteClips", LoadNoteClips());
        Set(serialized, "hollowThudClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MainGame/Audio/SFX/SFX_Interact_Generic_01.wav"));
        Set(serialized, "volume", 1f);
        Set(serialized, "audioMinDistance", 2f);
        Set(serialized, "audioMaxDistance", 18f);
        Set(serialized, "hiddenCavity", cavity);
        Set(serialized, "hollowPopLocalOffset", new Vector3(0.42f, -0.42f, 0.08f));
        Set(serialized, "hollowPopLocalEuler", new Vector3(0f, 0f, -18f));
        Set(serialized, "hollowPopSeconds", 0.55f);
        Set(serialized, "exitDelayAfterReveal", 0.25f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(puzzle);
        return puzzle;
    }

    private static void AttachZoomSequence(P2KnockPlankPuzzle puzzle, Transform cameraPoint, Transform lookTarget)
    {
        var recorder = FindSceneTransform(RecorderName) ?? FindSceneTransform(AlternateRecorderName);
        if (recorder == null)
        {
            Debug.LogWarning("Could not find BL03 recorder. Run MainGame/P2/Apply P2 Audio Logs first if the recorder is missing.");
            return;
        }

        var audioLog = recorder.GetComponent<P2AudioLogItem>();
        if (audioLog == null)
            audioLog = recorder.gameObject.AddComponent<P2AudioLogItem>();

        var sequence = recorder.GetComponent<P2KnockPlankZoomSequence>();
        if (sequence == null)
            sequence = recorder.gameObject.AddComponent<P2KnockPlankZoomSequence>();

        var serialized = new SerializedObject(sequence);
        Set(serialized, "audioLog", audioLog);
        Set(serialized, "triggerOnce", true);
        Set(serialized, "delayAfterAudio", 0.35f);
        Set(serialized, "targetCamera", Camera.main);
        Set(serialized, "targetVirtualCamera", FindGameplayVirtualCamera());
        Set(serialized, "cameraZoomPoint", cameraPoint);
        Set(serialized, "lookTarget", lookTarget);
        Set(serialized, "zoomFieldOfView", 38f);
        Set(serialized, "enterZoomSeconds", 0.65f);
        Set(serialized, "exitZoomSeconds", 0.5f);
        Set(serialized, "puzzle", puzzle);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(audioLog);
        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(recorder.gameObject);
    }

    private static AudioClip[] LoadNoteClips()
    {
        var clips = new AudioClip[NoteClipPaths.Length];
        for (var i = 0; i < clips.Length; i++)
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(NoteClipPaths[i]);
        return clips;
    }

    private static CinemachineCamera FindGameplayVirtualCamera()
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<FpsController>(FindObjectsInactive.Include);
        if (controller != null && controller.virtualCamera != null)
            return controller.virtualCamera;

        return UnityEngine.Object.FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
    }

    private static Transform EnsureRoot(string name)
    {
        var root = FindSceneTransform(name);
        if (root != null)
            return root;

        return new GameObject(name).transform;
    }

    private static Transform EnsurePoint(string name, Transform parent, Vector3 position)
    {
        var point = FindSceneTransform(name);
        if (point == null)
        {
            var obj = new GameObject(name);
            point = obj.transform;
        }

        point.SetParent(parent, true);
        point.position = position;
        return point;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName && transform.gameObject.scene.IsValid())
                return transform;
        }

        return null;
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private static Material EnsureMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var created = false;
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
            created = true;
        }

        if (created)
            material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RemoveOldP2Interactable(GameObject target)
    {
        foreach (var component in target.GetComponents<Component>())
        {
            if (component == null)
                continue;

            var type = component.GetType();
            if (type == typeof(P2Interactable))
                UnityEngine.Object.DestroyImmediate(component, true);
        }
    }

    private static string SanitizeName(string value)
    {
        return value == "Bộp" ? "Thud" : value;
    }

    private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
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

    private static void Set(SerializedObject serialized, string propertyName, Vector3 value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    private static void SetArray<T>(SerializedObject serialized, string propertyName, T[] values) where T : UnityEngine.Object
    {
        var property = serialized.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = values != null ? values.Length : 0;
        for (var i = 0; values != null && i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
