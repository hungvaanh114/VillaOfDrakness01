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
        var pointsRoot = EnsurePointsRoot(managerObject.transform, sequenceRoot);

        var forestStart = EnsurePoint(pointsRoot, "ForestStartPoint", new Vector3(-9.52f, 1.07f, -58f), Vector3.forward);
        var gateApproach = EnsurePoint(pointsRoot, "GateApproachPoint", new Vector3(-9.52f, 1.07f, -40f), Vector3.forward);
        var frontDoor = EnsurePoint(pointsRoot, "FrontDoorPoint", new Vector3(-9.52f, 1.07f, -25f), Vector3.forward);
        var fenceWalk = EnsurePoint(pointsRoot, "FenceWalkPoint", new Vector3(-2.25f, 1.07f, -34f), new Vector3(0.6f, 0f, 1f));
        var diningWindow = EnsurePoint(pointsRoot, "DiningWindowPoint", new Vector3(-2.2f, 1.07f, -18f), Vector3.forward);
        var diningRoom = EnsurePoint(pointsRoot, "DiningRoomPoint", new Vector3(-2.2f, 1.07f, -12.5f), Vector3.forward);

        var sequence = sequenceRoot.GetComponent<CutSceneSequence>() ?? sequenceRoot.gameObject.AddComponent<CutSceneSequence>();
        sequence.Configure(
            "intro",
            true,
            forestStart,
            true,
            CreateIntroRoute(gateApproach, frontDoor, fenceWalk, diningWindow, diningRoom));
        EditorUtility.SetDirty(sequence);

        var manager = managerObject.GetComponent<CutSceneManager>() ?? managerObject.AddComponent<CutSceneManager>();
        var voiceSource = managerObject.GetComponent<AudioSource>() ?? managerObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;

        var fps = player.GetComponent<FpsHorrorKit.FpsController>();
        var controller = FindFirstObject<GameController>();
        WireCutSceneManager(
            manager,
            sequence,
            player,
            fps,
            mainCamera,
            followCamera,
            narrationPanel,
            narrationText,
            voiceSource);

        if (controller != null)
            WireGameController(controller, manager);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Intro cut scene sequence applied. Edit IntroCutSceneSequence points directly to change dialogue text and voice clips.");
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

    private static IEnumerable<CutScenePoint> CreateIntroRoute(
        Transform gateApproach,
        Transform frontDoor,
        Transform fenceWalk,
        Transform diningWindow,
        Transform diningRoom)
    {
        return new[]
        {
            Point("Đi tới cổng", gateApproach, IntroDialogueIds[0], true, CutSceneCameraShot.OverheadFollow),
            Point("Dừng trước cổng", gateApproach, IntroDialogueIds[1], false, CutSceneCameraShot.DescendBehind),
            Point("Soi bảng Đỗ Gia", gateApproach, IntroDialogueIds[2], false, CutSceneCameraShot.SignClose),
            Point("Lịch sử biệt thự", gateApproach, IntroDialogueIds[3], false, CutSceneCameraShot.SignClose),
            Point("Khoa mỉm cười", gateApproach, IntroDialogueIds[4], false, CutSceneCameraShot.BehindShoulder),
            Point("Đề tài tốt nghiệp", gateApproach, IntroDialogueIds[5], false, CutSceneCameraShot.BehindShoulder),
            Point("Đi vòng hàng rào", fenceWalk, IntroDialogueIds[6], true, CutSceneCameraShot.DescendBehind),
            Point("Tới cửa chính", frontDoor, IntroDialogueIds[7], true, CutSceneCameraShot.BehindShoulder),
            Point("Thấy cửa sổ phòng ăn", diningWindow, IntroDialogueIds[8], true, CutSceneCameraShot.WindowInspect),
            Point("Quyết định chui vào", diningWindow, IntroDialogueIds[9], false, CutSceneCameraShot.WindowInspect),
            Point("Vào phòng ăn", diningRoom, IntroDialogueIds[10], true, CutSceneCameraShot.InteriorSettle)
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
        Transform player,
        FpsHorrorKit.FpsController fps,
        Camera mainCamera,
        Transform followCamera,
        Transform narrationPanel,
        Transform narrationText,
        AudioSource voiceSource)
    {
        var serialized = new SerializedObject(manager);
        Set(serialized, "introCutSceneId", "intro");
        Set(serialized, "autoResolveSequences", true);
        SetSequenceList(serialized, introSequence);
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

    private static void SetSequenceList(SerializedObject serialized, CutSceneSequence sequence)
    {
        var property = serialized.FindProperty("sequences");
        if (property == null)
            return;

        property.arraySize = 1;
        property.GetArrayElementAtIndex(0).objectReferenceValue = sequence;
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
