using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CutSceneSceneUpdater
{
    private const string DataFolder = "Assets/MainGame/Data";
    private const string IntroDialogueDataPath = DataFolder + "/IntroCutSceneDialogueData.asset";
    private const string LegacyIntroDataPath = DataFolder + "/IntroCutSceneData.asset";
    private const string IntroCutSceneId = "intro";
    private const string IntroWindowEntryCutSceneId = "intro_window_entry";

    private static readonly string[] IntroDialogueIds =
    {
        "intro_gate_arrival",
        "intro_arrived_line",
        "intro_dogia_sign",
        "intro_villa_history",
        "intro_khoa_smiles",
        "intro_thesis_line",
        "intro_locked_gate",
        "intro_front_sketch_line",
        "intro_window_open",
        "intro_enter_window_line",
        "intro_dining_room"
    };

    [MenuItem("Tools/MainGame/Apply Intro Cut Scene")]
    public static void Apply()
    {
        var player = FindInActiveScene("Player");
        var mainCamera = FindCamera();
        var followCamera = FindInActiveScene("FollowCamera");
        var narrationPanel = FindInActiveScene("NarrationPanel");
        var narrationText = FindInActiveScene("NarrationText");

        if (player == null || mainCamera == null)
        {
            Debug.LogError("Cannot apply intro cut scene. Player or Main Camera was not found.");
            return;
        }

        var managerObject = EnsureCutSceneManager();
        var sequenceRoot = EnsureChild(managerObject.transform, "IntroCutSceneSequence");
        var windowEntrySequenceRoot = EnsureChild(managerObject.transform, "IntroWindowEntryCutSceneSequence");
        var pointsRoot = EnsurePointsRoot(managerObject.transform, sequenceRoot);

        var forestStart = EnsurePoint(pointsRoot, "ForestStartPoint", new Vector3(-6f, 1.07f, -81.8f), Vector3.forward);
        var gateApproach = EnsurePoint(pointsRoot, "GateApproachPoint", new Vector3(-8.68f, 1.07f, -47.41f), Vector3.forward);
        var frontDoor = EnsurePoint(pointsRoot, "FrontDoorPoint", new Vector3(-28.78f, 0.66f, -19.79f), Quaternion.Euler(0f, -12.03f, 0f) * Vector3.forward);
        var fenceWalk = EnsurePoint(pointsRoot, "FenceWalkPoint", new Vector3(-4.7f, 1.07f, -23.42f), Vector3.forward);
        var diningWindow = EnsurePoint(pointsRoot, "DiningWindowPoint", new Vector3(-31.93f, 1.07f, -8.78f), Quaternion.Euler(0f, 47.8f, 0f) * Vector3.forward);
        var diningRoom = EnsurePoint(pointsRoot, "DiningRoomPoint", new Vector3(-29.94f, 1.07f, -6.24f), Vector3.right);

        var sequence = sequenceRoot.GetComponent<CutSceneSequence>() ?? sequenceRoot.gameObject.AddComponent<CutSceneSequence>();
        sequence.Configure(
            IntroCutSceneId,
            true,
            forestStart,
            true,
            CreateIntroOpeningRoute(gateApproach, frontDoor, fenceWalk));
        EditorUtility.SetDirty(sequence);

        var windowEntrySequence = windowEntrySequenceRoot.GetComponent<CutSceneSequence>()
            ?? windowEntrySequenceRoot.gameObject.AddComponent<CutSceneSequence>();
        windowEntrySequence.Configure(
            IntroWindowEntryCutSceneId,
            true,
            diningWindow,
            false,
            CreateIntroWindowEntryRoute(diningWindow, diningRoom));
        EditorUtility.SetDirty(windowEntrySequence);

        var manager = managerObject.GetComponent<CutSceneManager>() ?? managerObject.AddComponent<CutSceneManager>();
        var voiceSource = managerObject.GetComponent<AudioSource>() ?? managerObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;

        var fps = player.GetComponent<FpsHorrorKit.FpsController>();
        WireFollowCamera(followCamera, fps);

        var controller = FindFirstObject<GameController>();
        WireCutSceneManager(
            manager,
            sequence,
            windowEntrySequence,
            player,
            fps,
            mainCamera,
            followCamera,
            narrationPanel,
            narrationText,
            voiceSource);
        EnsureIntroWindowTrigger(managerObject.transform, diningWindow, manager);

        if (controller != null)
            WireGameController(controller, manager);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Intro cut scenes applied. Edit IntroCutSceneSequence and IntroWindowEntryCutSceneSequence points directly to change dialogue text and voice clips.");
    }

    private static void WireFollowCamera(Transform followCamera, FpsHorrorKit.FpsController fps)
    {
        if (followCamera == null || fps == null)
            return;

        var cinemachineCamera = followCamera.GetComponent<CinemachineCamera>();
        if (cinemachineCamera == null)
            return;

        if (fps.virtualCamera == null)
        {
            fps.virtualCamera = cinemachineCamera;
            EditorUtility.SetDirty(fps);
        }

        var target = fps.followTarget != null ? fps.followTarget : fps.transform;
        cinemachineCamera.Target.TrackingTarget = target;
        EditorUtility.SetDirty(cinemachineCamera);
    }

    private static CutSceneDialogueData EnsureIntroDialogueData()
    {
        EnsureFolder("Assets/MainGame", "Data");

        var data = AssetDatabase.LoadAssetAtPath<CutSceneDialogueData>(IntroDialogueDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CutSceneDialogueData>();
            AssetDatabase.CreateAsset(data, IntroDialogueDataPath);
        }

        var legacy = AssetDatabase.LoadAssetAtPath<IntroCutSceneData>(LegacyIntroDataPath);
        data.ReplaceLines(CreateDefaultLines(legacy != null ? legacy.cues : null, data.Lines));
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static IEnumerable<CutSceneDialogueLine> CreateDefaultLines(
        IntroCutSceneCue[] legacyCues,
        IReadOnlyList<CutSceneDialogueLine> existingLines)
    {
        var lines = new[]
        {
            Line("intro_gate_arrival", "Cổng sắt gỉ sét khép hờ giữa đêm sương Đà Lạt dày đặc. Khoa đi bộ từ đầu con dốc lên, một tay cầm đèn pin, một tay ôm khư khư cái máy ảnh film mượn của phòng lab trường. Cậu dừng lại trước tấm bảng tên hoen gỉ, thở ra một hơi dài.", 7.5f),
            Line("intro_arrived_line", "\"Cuối cùng cũng đến nơi rồi. Đường lên đây xa thật.\"", 3.2f),
            Line("intro_dogia_sign", "Ánh đèn pin quét lên hàng chữ khắc mờ: Đỗ Gia.", 3.2f),
            Line("intro_villa_history", "\"Biệt thự Đỗ Gia... xây năm 1940. Kiến trúc sư người Pháp cùng ông Đỗ Văn Minh dựng nên căn biệt thự biệt lập này.\"", 6f),
            Line("intro_khoa_smiles", "Cậu mỉm cười một mình, kiểu cười của người vừa gạch được một dòng trong danh sách việc cần làm.", 4.6f),
            Line("intro_thesis_line", "\"Đề tài tốt nghiệp của mình đúng là không thể thiếu chỗ này.\"", 3.4f),
            Line("intro_locked_gate", "Cổng khoá. Khoa đi vòng theo hàng rào, đèn pin lia qua từng ô cửa sổ tối om.", 5.4f),
            Line("intro_front_sketch_line", "\"Chụp vài tấm, phác mặt tiền rồi về thôi. Mình không dám ở đây lâu đâu.\"", 4.4f),
            Line("intro_window_open", "Cửa chính khoá chặt, nhưng một khung cửa sổ phòng ăn lại hé mở vài phân - đủ để một người gầy lách qua. Cậu soi đèn vào trong, ngần ngừ đúng một giây.", 7.2f),
            Line("intro_enter_window_line", "\"Ủa... cửa sổ phòng ăn mở sẵn hả? Thôi vô đường này vậy. Xin lỗi ông Đỗ... con không có ý mạo phạm đâu.\"", 6.2f),
            Line("intro_dining_room", "Cậu trèo qua khung cửa, rơi xuống nền gạch bụi phủ dày của Phòng Ăn - một cái bàn dài dành cho mười người trở lên, giờ chỉ còn ngồi im lặng với chính nó. Chưa kịp cất bước, Khoa đã khựng lại, nghe tiếng bước chân chậm rãi từ đâu đó trong hành lang - không phải bước chân của cậu. Cậu đứng im chờ tiếng động tan hết mới dám nhúc nhích tiếp, rồi tự trấn an bằng phản xạ quen thuộc nhất của một đứa học kiến trúc: chụp ảnh, ghi bố cục.", 13f)
        };

        for (var i = 0; legacyCues != null && i < legacyCues.Length && i < lines.Length; i++)
        {
            lines[i].audioClip = legacyCues[i]?.audioClip;
        }

        foreach (var line in lines)
        {
            var existing = FindLine(existingLines, line.id);
            if (existing == null)
                continue;

            if (!string.IsNullOrWhiteSpace(existing.text))
                line.text = existing.text;
            if (existing.audioClip != null)
                line.audioClip = existing.audioClip;
            if (existing.fallbackDuration > 0f)
                line.fallbackDuration = existing.fallbackDuration;
        }

        return lines;
    }

    private static CutSceneDialogueLine FindLine(IReadOnlyList<CutSceneDialogueLine> lines, string id)
    {
        if (lines == null)
            return null;

        foreach (var line in lines)
        {
            if (line != null && line.id == id)
                return line;
        }

        return null;
    }

    private static CutSceneDialogueLine Line(string id, string text, float duration)
    {
        return new CutSceneDialogueLine(id, text, duration, DefaultDialogueClip(id));
    }

    private static IEnumerable<CutScenePoint> CreateIntroOpeningRoute(
        Transform gateApproach,
        Transform frontDoor,
        Transform fenceWalk)
    {
        return new[]
        {
            Point("Walk to gate", gateApproach, IntroDialogueIds[0], true, CutSceneCameraShot.OverheadFollow),
            Point("Stop at gate", gateApproach, IntroDialogueIds[1], false, CutSceneCameraShot.DescendBehind),
            Point("Read Do Gia sign", gateApproach, IntroDialogueIds[2], false, CutSceneCameraShot.SignClose),
            Point("Villa history", gateApproach, IntroDialogueIds[3], false, CutSceneCameraShot.SignClose),
            Point("Khoa smiles", gateApproach, IntroDialogueIds[4], false, CutSceneCameraShot.BehindShoulder),
            Point("Thesis note", gateApproach, IntroDialogueIds[5], false, CutSceneCameraShot.BehindShoulder),
            Point("Walk around fence", fenceWalk, IntroDialogueIds[6], true, CutSceneCameraShot.DescendBehind),
            Point("Stop at front door", frontDoor, IntroDialogueIds[7], true, CutSceneCameraShot.BehindShoulder)
        };
    }

    private static IEnumerable<CutScenePoint> CreateIntroWindowEntryRoute(Transform diningWindow, Transform diningRoom)
    {
        return new[]
        {
            Point("Find dining window", diningWindow, IntroDialogueIds[8], false, CutSceneCameraShot.WindowInspect),
            Point("Decide to climb in", diningWindow, IntroDialogueIds[9], false, CutSceneCameraShot.WindowInspect),
            Point("Enter dining room", diningRoom, IntroDialogueIds[10], true, CutSceneCameraShot.InteriorSettle)
        };
    }

    private static CutScenePoint Point(string name, Transform transform, string dialogueId, bool moveToPoint, CutSceneCameraShot shot)
    {
        return new CutScenePoint(name, transform, dialogueId, shot)
        {
            moveToPoint = moveToPoint,
            overrideText = DefaultDialogueText(dialogueId),
            overrideAudioClip = DefaultDialogueClip(dialogueId),
            overrideFallbackDuration = DefaultDialogueDuration(dialogueId),
            waitAfter = 0.16f
        };
    }

    private static AudioClip DefaultDialogueClip(string dialogueId)
    {
        var path = dialogueId switch
        {
            "intro_arrived_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_01.wav",
            "intro_villa_history" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_02.wav",
            "intro_thesis_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_03.wav",
            "intro_front_sketch_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_04.wav",
            "intro_enter_window_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_05.wav",
            _ => null
        };

        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static string DefaultDialogueText(string dialogueId)
    {
        return dialogueId switch
        {
            "intro_gate_arrival" => "Cổng sắt gỉ sét khép hờ giữa đêm sương Đà Lạt dày đặc. Khoa đi bộ từ đầu con dốc lên, một tay cầm đèn pin, một tay ôm khư khư cái máy ảnh film mượn của phòng lab trường. Cậu dừng lại trước tấm bảng tên hoen gỉ, thở ra một hơi dài.",
            "intro_arrived_line" => "\"Cuối cùng cũng đến nơi rồi. Đường lên đây xa thật.\"",
            "intro_dogia_sign" => "Ánh đèn pin quét lên hàng chữ khắc mờ: Đỗ Gia.",
            "intro_villa_history" => "\"Biệt thự Đỗ Gia... xây năm 1940. Kiến trúc sư người Pháp cùng ông Đỗ Văn Minh dựng nên căn biệt thự biệt lập này.\"",
            "intro_khoa_smiles" => "Cậu mỉm cười một mình, kiểu cười của người vừa gạch được một dòng trong danh sách việc cần làm.",
            "intro_thesis_line" => "\"Đề tài tốt nghiệp của mình đúng là không thể thiếu chỗ này.\"",
            "intro_locked_gate" => "Cổng khoá. Khoa đi vòng theo hàng rào, đèn pin lia qua từng ô cửa sổ tối om.",
            "intro_front_sketch_line" => "\"Chụp vài tấm, phác mặt tiền rồi về thôi. Mình không dám ở đây lâu đâu.\"",
            "intro_window_open" => "Cửa chính khoá chặt, nhưng một khung cửa sổ phòng ăn lại hé mở vài phân - đủ để một người gầy lách qua. Cậu soi đèn vào trong, ngần ngừ đúng một giây.",
            "intro_enter_window_line" => "\"Ủa... cửa sổ phòng ăn mở sẵn hả? Thôi vô đường này vậy. Xin lỗi ông Đỗ... con không có ý mạo phạm đâu.\"",
            "intro_dining_room" => "Cậu trèo qua khung cửa, rơi xuống nền gạch bụi phủ dày của Phòng Ăn - một cái bàn dài dành cho mười người trở lên, giờ chỉ còn ngồi im lặng với chính nó. Chưa kịp cất bước, Khoa đã khựng lại, nghe tiếng bước chân chậm rãi từ đâu đó trong hành lang - không phải bước chân của cậu. Cậu đứng im chờ tiếng động tan hết mới dám nhúc nhích tiếp, rồi tự trấn an bằng phản xạ quen thuộc nhất của một đứa học kiến trúc: chụp ảnh, ghi bố cục.",
            _ => string.Empty
        };
    }

    private static float DefaultDialogueDuration(string dialogueId)
    {
        return dialogueId switch
        {
            "intro_gate_arrival" => 7.5f,
            "intro_arrived_line" => 3.2f,
            "intro_dogia_sign" => 3.2f,
            "intro_villa_history" => 6f,
            "intro_khoa_smiles" => 4.6f,
            "intro_thesis_line" => 3.4f,
            "intro_locked_gate" => 5.4f,
            "intro_front_sketch_line" => 4.4f,
            "intro_window_open" => 7.2f,
            "intro_enter_window_line" => 6.2f,
            "intro_dining_room" => 13f,
            _ => 4f
        };
    }

    private static void WireCutSceneManager(
        CutSceneManager manager,
        CutSceneSequence introSequence,
        CutSceneSequence introWindowEntrySequence,
        Transform player,
        FpsHorrorKit.FpsController fps,
        Camera mainCamera,
        Transform followCamera,
        Transform narrationPanel,
        Transform narrationText,
        AudioSource voiceSource)
    {
        var serialized = new SerializedObject(manager);
        Set(serialized, "introCutSceneId", IntroCutSceneId);
        Set(serialized, "introWindowEntryCutSceneId", IntroWindowEntryCutSceneId);
        Set(serialized, "autoResolveSequences", true);
        SetSequenceList(serialized, introSequence, introWindowEntrySequence);
        Set(serialized, "playerController", fps);
        Set(serialized, "playerRoot", player);
        Set(serialized, "cinematicCamera", mainCamera);
        Set(serialized, "cinemachineBrain", mainCamera.GetComponent<CinemachineBrain>());
        Set(serialized, "gameplayVirtualCamera", followCamera != null ? followCamera.GetComponent<CinemachineCamera>() : null);
        Set(serialized, "subtitlePanel", narrationPanel != null ? narrationPanel.gameObject : null);
        Set(serialized, "subtitleText", narrationText != null ? narrationText.GetComponent<TMP_Text>() : null);
        Set(serialized, "voiceSource", voiceSource);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    private static void EnsureIntroWindowTrigger(Transform parent, Transform diningWindow, CutSceneManager manager)
    {
        if (diningWindow == null)
            return;

        var triggerTransform = EnsureChild(parent, "IntroWindowCutsceneTrigger");
        triggerTransform.position = diningWindow.position;
        triggerTransform.rotation = diningWindow.rotation;

        var collider = triggerTransform.GetComponent<BoxCollider>() ?? triggerTransform.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.center = new Vector3(0f, 1.1f, 0f);
        collider.size = new Vector3(2.8f, 2.2f, 2.8f);
        EditorUtility.SetDirty(collider);

        var trigger = triggerTransform.GetComponent<IntroWindowCutsceneTrigger>()
            ?? triggerTransform.gameObject.AddComponent<IntroWindowCutsceneTrigger>();
        var serialized = new SerializedObject(trigger);
        Set(serialized, "cutSceneManager", manager);
        Set(serialized, "cutSceneId", IntroWindowEntryCutSceneId);
        Set(serialized, "triggerOnce", true);
        Set(serialized, "requireIntroPhase", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
    }

    private static void WireGameController(GameController controller, CutSceneManager manager)
    {
        var serialized = new SerializedObject(controller);
        Set(serialized, "cutSceneManager", manager);
        Set(serialized, "playIntroOnStart", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static GameObject EnsureCutSceneManager()
    {
        var existing = FindInActiveScene("CutSceneManager");
        if (existing != null)
            return existing.gameObject;

        return new GameObject("CutSceneManager", typeof(CutSceneManager), typeof(AudioSource));
    }

    private static Transform EnsurePointsRoot(Transform managerRoot, Transform sequenceRoot)
    {
        var pointsRoot = sequenceRoot.Find("IntroPathPoints");
        if (pointsRoot != null)
            return pointsRoot;

        var legacyRoot = managerRoot.Find("IntroPathPoints");
        if (legacyRoot != null)
        {
            legacyRoot.SetParent(sequenceRoot, true);
            return legacyRoot;
        }

        return EnsureChild(sequenceRoot, "IntroPathPoints");
    }

    private static Transform EnsurePoint(Transform parent, string name, Vector3 position, Vector3 forward)
    {
        var point = EnsureChild(parent, name);
        point.position = position;
        if (forward.sqrMagnitude > 0.001f)
            point.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return point;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child;

        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static Camera FindCamera()
    {
        var main = Camera.main;
        if (main != null)
            return main;

        var cameraTransform = FindInActiveScene("Main Camera_1") ?? FindInActiveScene("Main Camera");
        return cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
    }

    private static T FindFirstObject<T>() where T : Object
    {
        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item is Component component && component.gameObject.scene == EditorSceneManager.GetActiveScene())
                return item;
            if (item is GameObject gameObject && gameObject.scene == EditorSceneManager.GetActiveScene())
                return item;
        }

        return null;
    }

    private static Transform FindInActiveScene(string name)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == name && transform.gameObject.scene == activeScene)
                return transform;
        }

        return null;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder(Path.Combine(parent, folderName).Replace('\\', '/')))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void SetSequenceList(SerializedObject serialized, params CutSceneSequence[] sequences)
    {
        var property = serialized.FindProperty("sequences");
        if (property == null)
            return;

        property.arraySize = sequences.Length;
        for (var i = 0; i < sequences.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sequences[i];
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
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

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }
}
