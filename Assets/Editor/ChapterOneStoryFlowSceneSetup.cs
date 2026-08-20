using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChapterOneStoryFlowSceneSetup
{
    private const string MenuPath = "Tools/MainGame/Create Chapter One Story Markers";
    private const string AutoRunFlagPath = "Library/ChapterOneStoryFlowSceneSetup.run";

    [InitializeOnLoadMethod]
    private static void RunQueuedSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoRunFlagPath))
                return;

            File.Delete(AutoRunFlagPath);
            CreateOrUpdateMarkers();
        };
    }

    [MenuItem(MenuPath)]
    public static void CreateOrUpdateMarkers()
    {
        var flow = FindSceneComponent<ChapterOneStoryFlow>();
        if (flow == null)
        {
            var flowObject = new GameObject("ChapterOneStoryFlow");
            Undo.RegisterCreatedObjectUndo(flowObject, "Create ChapterOneStoryFlow");
            flow = flowObject.AddComponent<ChapterOneStoryFlow>();
        }

        var monster = ResolveMonster();
        var gramophone = FindSceneComponent<FpsHorrorKit.GramophoneTapePlayer>();
        var closet = FindSceneComponent<FpsHorrorKit.ClosetHiding>();
        var wellEnding = ResolveWellEnding();
        var player = FindSceneComponent<FpsHorrorKit.FpsController>();
        Transform playerTransform = player != null ? player.transform : null;

        var markerRoot = FindOrCreateRoot("Chapter1StoryMarkers");
        var hallStart = FindOrCreateMarker(markerRoot, "HallGhostStart", GetHallMarkerPosition(playerTransform, -7f));
        var hallMid = FindOrCreateMarker(markerRoot, "HallGhostMid", GetHallMarkerPosition(playerTransform, 0f));
        var hallEnd = FindOrCreateMarker(markerRoot, "HallGhostEnd", GetHallMarkerPosition(playerTransform, 7f));
        var hallHide = FindOrCreateMarker(markerRoot, "HallGhostHidePoint", hallEnd.position + hallEnd.right * 2f);
        var upperSpawn = FindOrCreateMarker(markerRoot, "MonsterUpperFloorSpawn", GetUpperSpawnPosition(monster, playerTransform));
        var wellGhostStart = FindOrCreateMarker(markerRoot, "WellGhostStart", GetWellPosition(wellEnding, -1.4f));
        var wellPullTarget = FindOrCreateMarker(markerRoot, "WellPullTarget", GetWellPosition(wellEnding, -3.5f));

        AssignFlow(flow, monster, gramophone, closet, wellEnding, hallStart, hallMid, hallEnd, hallHide, upperSpawn);
        AssignMonster(monster);
        AssignWell(wellEnding, wellGhostStart, wellPullTarget);

        EditorUtility.SetDirty(flow);
        if (monster != null)
            EditorUtility.SetDirty(monster);
        if (wellEnding != null)
            EditorUtility.SetDirty(wellEnding);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Chapter one story markers created and assigned. Move the marker objects in Chapter1StoryMarkers to tune the ghost path, upstairs spawn, and well ending.");
    }

    private static MonsterAI ResolveMonster()
    {
        var holder = GameObject.Find("MonsterPlaceholder");
        var monster = holder != null ? holder.GetComponent<MonsterAI>() : FindSceneComponent<MonsterAI>();
        if (monster != null)
            return monster;

        if (holder == null)
        {
            holder = new GameObject("MonsterPlaceholder");
            Undo.RegisterCreatedObjectUndo(holder, "Create MonsterPlaceholder");
        }

        return holder.AddComponent<MonsterAI>();
    }

    private static WellEndingTrigger ResolveWellEnding()
    {
        var ending = FindSceneComponent<WellEndingTrigger>();
        if (ending != null)
            return ending;

        var well = FindSceneObjectByNameToken("Well") ?? FindSceneObjectByNameToken("Gieng") ?? FindSceneObjectByNameToken("Giếng");
        if (well == null)
            return null;

        return well.GetComponent<WellEndingTrigger>() ?? well.AddComponent<WellEndingTrigger>();
    }

    private static void AssignFlow(
        ChapterOneStoryFlow flow,
        MonsterAI monster,
        FpsHorrorKit.GramophoneTapePlayer gramophone,
        FpsHorrorKit.ClosetHiding closet,
        WellEndingTrigger wellEnding,
        Transform hallStart,
        Transform hallMid,
        Transform hallEnd,
        Transform hallHide,
        Transform upperSpawn)
    {
        var serializedFlow = new SerializedObject(flow);
        SetObject(serializedFlow, "monster", monster);
        SetObject(serializedFlow, "gramophoneTapePlayer", gramophone);
        SetObject(serializedFlow, "preferredCloset", closet);
        SetObject(serializedFlow, "wellEndingTrigger", wellEnding);
        SetObject(serializedFlow, "hallGhostStart", hallStart);
        SetTransformArray(serializedFlow, "hallGhostRoutePoints", hallMid, hallEnd);
        SetObject(serializedFlow, "hallGhostEnd", hallEnd);
        SetObject(serializedFlow, "hallGhostHidePoint", hallHide);
        SetFloat(serializedFlow, "fallbackHallPassWait", 2f);
        SetObject(serializedFlow, "monsterUpperFloorSpawn", upperSpawn);
        SetFloat(serializedFlow, "postGramophoneSilence", 5f);
        serializedFlow.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignMonster(MonsterAI monster)
    {
        if (monster == null)
            return;

        var audioSource = monster.GetComponent<AudioSource>() ?? monster.gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 25f;

        var serializedMonster = new SerializedObject(monster);
        SetObject(serializedMonster, "animator", monster.GetComponentInChildren<Animator>(true));
        SetObject(serializedMonster, "monsterAudioSource", audioSource);
        SetFloat(serializedMonster, "scriptedPassSpeed", 12f);
        SetFloat(serializedMonster, "chaseSpeed", 5.4f);
        SetFloat(serializedMonster, "searchSpeed", 3.8f);
        SetFloat(serializedMonster, "runFootstepInterval", 0.18f);
        SetFloat(serializedMonster, "walkFootstepInterval", 0.65f);
        SetAudioClipArray(serializedMonster, "footstepClips",
            "Assets/MainGame/Audio/FootSteps/Footstep_Wood_01.wav",
            "Assets/MainGame/Audio/FootSteps/Footstep_Wood_02.wav");
        SetAudioClipArray(serializedMonster, "attackClips",
            "Assets/MainGame/Audio/SFX/SFX_Ghost_Jumpscare_Scream_01.wav",
            "Assets/MainGame/Audio/SFX/SFX_Ghost_Jumpscare_Scream_02.wav",
            "Assets/MainGame/Audio/SFX/SFX_Ghost_Jumpscare_Scream_03.wav");
        SetAudioClipArray(serializedMonster, "idleHorrorClips",
            "Assets/MainGame/Audio/SFX/SFX_Sanity_Warning_01.wav",
            "Assets/MainGame/Audio/SFX/SFX_Sanity_Warning_02.wav");
        SetAudioClipArray(serializedMonster, "voiceClips",
            "Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MaVuDai_Patrol_Full.wav");
        serializedMonster.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignWell(WellEndingTrigger wellEnding, Transform ghostStart, Transform pullTarget)
    {
        if (wellEnding == null)
            return;

        var serializedWell = new SerializedObject(wellEnding);
        SetObject(serializedWell, "ghostStartPoint", ghostStart);
        SetObject(serializedWell, "pullTargetPoint", pullTarget);
        serializedWell.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindOrCreateRoot(string objectName)
    {
        var existing = GameObject.Find(objectName);
        if (existing != null)
            return existing.transform;

        var created = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(created, "Create Chapter One Marker Root");
        return created.transform;
    }

    private static Transform FindOrCreateMarker(Transform parent, string objectName, Vector3 defaultPosition)
    {
        Transform marker = parent.Find(objectName);
        if (marker != null)
            return marker;

        var markerObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Chapter One Marker");
        markerObject.transform.SetParent(parent);
        markerObject.transform.position = defaultPosition;
        markerObject.transform.rotation = Quaternion.identity;
        return markerObject.transform;
    }

    private static Vector3 GetHallMarkerPosition(Transform player, float sideOffset)
    {
        if (player == null)
            return new Vector3(sideOffset, 0f, 0f);

        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return player.position + forward * 4f + right * sideOffset;
    }

    private static Vector3 GetUpperSpawnPosition(MonsterAI monster, Transform player)
    {
        if (monster != null)
            return monster.transform.position + Vector3.up * 0.1f;
        if (player != null)
            return player.position + Vector3.up * 4f;
        return new Vector3(0f, 4f, 0f);
    }

    private static Vector3 GetWellPosition(WellEndingTrigger wellEnding, float yOffset)
    {
        if (wellEnding == null)
            return new Vector3(0f, yOffset, 0f);

        return wellEnding.transform.position + Vector3.up * yOffset;
    }

    private static GameObject FindSceneObjectByNameToken(string token)
    {
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var item in transforms)
        {
            if (item != null && item.gameObject.scene.IsValid() && item.name.Contains(token, System.StringComparison.OrdinalIgnoreCase))
                return item.gameObject;
        }

        return null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        var components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var component in components)
        {
            if (component != null && component.gameObject.scene.IsValid())
                return component;
        }

        return null;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetTransformArray(SerializedObject serializedObject, string propertyName, params Transform[] values)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetAudioClipArray(SerializedObject serializedObject, string propertyName, params string[] assetPaths)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.arraySize = assetPaths.Length;
        for (int i = 0; i < assetPaths.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPaths[i]);
            property.GetArrayElementAtIndex(i).objectReferenceValue = clip;
        }
    }
}
