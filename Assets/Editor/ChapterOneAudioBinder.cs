using System.Collections.Generic;
using System.IO;
using FpsHorrorKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChapterOneAudioBinder
{
    private const string GameScenePath = "Assets/MainGame/Game.unity";
    private const string AudioDataPath = "Assets/MainGame/Resources/Audio/AudioData.asset";
    private const string IntroDialogueDataPath = "Assets/MainGame/Data/IntroCutSceneDialogueData.asset";
    private const string AutoBindSessionKey = "ChapterOneAudioBinder.AutoBindComplete";

    [InitializeOnLoadMethod]
    private static void AutoBindOnceIfNeeded()
    {
        if (SessionState.GetBool(AutoBindSessionKey, false))
            return;

        var data = AssetDatabase.LoadAssetAtPath<AudioData>(AudioDataPath);
        if (data != null && data.menuMusic != null && data.introAmbience != null && data.doorOpenSlow != null)
            return;

        SessionState.SetBool(AutoBindSessionKey, true);
        EditorApplication.delayCall += Bind;
    }

    [MenuItem("Tools/MainGame/Bind Chapter 1 Story Audio")]
    [MenuItem("Assets/MainGame/Bind Chapter 1 Story Audio")]
    public static void Bind()
    {
        var audioData = EnsureAudioData();
        BindIntroDialogueData();

        if (File.Exists(GameScenePath))
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath);
            BindScene(audioData);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Chapter 1 story audio binding completed.");
    }

    private static AudioData EnsureAudioData()
    {
        EnsureFolder("Assets/MainGame", "Resources");
        EnsureFolder("Assets/MainGame/Resources", "Audio");

        var data = AssetDatabase.LoadAssetAtPath<AudioData>(AudioDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<AudioData>();
            AssetDatabase.CreateAsset(data, AudioDataPath);
        }

        var serialized = new SerializedObject(data);
        Set(serialized, "menuMusic", Clip("Assets/MainGame/Audio/BGM/BGM_MainMenu_TheHollowRoom.wav"));
        Set(serialized, "introAmbience", Clip("Assets/MainGame/Audio/Ambient/Amb_Exterior_Garden.mp3"));
        Set(serialized, "gameplayAmbience", Clip("Assets/MainGame/Audio/Ambient/Amb_CH1_Day_01.wav"));
        Set(serialized, "ghostAmbience", Clip("Assets/MainGame/Audio/Ambient/Amb_CH1_Ghost.wav"));
        Set(serialized, "chaseMusic", Clip("Assets/MainGame/Audio/BGM/BGM_CH1_Scene3_Chase.mp3"));
        Set(serialized, "deathMusic", Clip("Assets/MainGame/Audio/BGM/BGM_DeathScreen_TheLastBroadcast.wav"));

        Set(serialized, "buttonHover", Clip("Assets/MainGame/Audio/SFX/sfxclick.mp3"));
        Set(serialized, "buttonClick", Clip("Assets/MainGame/Audio/SFX/sfxclick.mp3"));
        Set(serialized, "applySettings", Clip("Assets/MainGame/Audio/SFX/SFX_DialogueAdvance_Click.mp3"));
        Set(serialized, "back", Clip("Assets/MainGame/Audio/SFX/sfxclick.mp3"));

        Set(serialized, "flashlightToggle", Clip("Assets/MainGame/Audio/SFX/SFX_FlashlightShake.wav"));
        Set(serialized, "cameraShot", Clip("Assets/MainGame/Audio/SFX/Sfx.mp3"));
        Set(serialized, "keyPickup", Clip("Assets/MainGame/Audio/SFX/SFX_KeyPickup.mp3"));
        Set(serialized, "notePickup", Clip("Assets/MainGame/Audio/SFX/SFX_PaperPickup_Wood.mp3"));
        Set(serialized, "paperPickup", Clip("Assets/MainGame/Audio/SFX/SFX_Diary_PageFlip.mp3"));
        Set(serialized, "genericInteract", Clip("Assets/MainGame/Audio/SFX/SFX_Interact_Generic_01.wav"));
        Set(serialized, "doorLocked", Clip("Assets/MainGame/Audio/SFX/SFX_Door_Locked.mp3"));
        Set(serialized, "doorOpenSlow", Clip("Assets/MainGame/Audio/SFX/SFX_Door_OpenSlow.mp3"));
        Set(serialized, "doorUnlock", Clip("Assets/MainGame/Audio/SFX/SFX_Door_Unlock.mp3"));
        Set(serialized, "itemUnlock", Clip("Assets/MainGame/Audio/SFX/SFX_ItemLock_Unlock.wav"));
        Set(serialized, "musicBoxStartup", Clip("Assets/MainGame/Audio/SFX/SFX_MusicBox_Startup_01.wav"));
        Set(serialized, "pianoWrong", Clip("Assets/MainGame/Audio/SFX/SFX_Sanity_Warning_01.wav"));
        Set(serialized, "sanityWarning", Clip("Assets/MainGame/Audio/SFX/SFX_Sanity_Warning_02.wav"));
        Set(serialized, "ghostJumpscare", Clip("Assets/MainGame/Audio/SFX/SFX_Ghost_Jumpscare_Scream_01.wav"));
        Set(serialized, "wellJumpscare", Clip("Assets/MainGame/Audio/SFX/SFX_WellJumpscare_MaDa.wav"));
        Set(serialized, "clothTearOff", Clip("Assets/MainGame/Audio/SFX/SFX_Cloth_TearOff.mp3"));
        Set(serialized, "dialogueAdvance", Clip("Assets/MainGame/Audio/SFX/SFX_DialogueAdvance_Click.mp3"));
        Set(serialized, "dialogueTextBlip", Clip("Assets/MainGame/Audio/SFX/SFX_DialogueTextBlip_Loop.mp3"));

        Set(serialized, "intro01", Clip("Assets/MainGame/Audio/Voice/VO_Ch1_Intro_01.wav"));
        Set(serialized, "intro02", Clip("Assets/MainGame/Audio/Voice/VO_Ch1_Intro_02.wav"));
        Set(serialized, "intro03", Clip("Assets/MainGame/Audio/Voice/VO_Ch1_Intro_03.wav"));
        Set(serialized, "intro04", Clip("Assets/MainGame/Audio/Voice/VO_Ch1_Intro_04.wav"));
        Set(serialized, "intro05", Clip("Assets/MainGame/Audio/Voice/VO_Ch1_Intro_05.wav"));
        Set(serialized, "baLanTapeFull", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_BaLan_Tape_Full.wav"));
        Set(serialized, "maVuDaiPatrolFull", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MaVuDai_Patrol_Full.wav"));
        Set(serialized, "diaryReaction01", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_DiaryReaction_01.wav"));
        Set(serialized, "diaryReaction02", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_DiaryReaction_02.wav"));
        Set(serialized, "diaryReaction03", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_DiaryReaction_03.wav"));
        Set(serialized, "mkHide01", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-HIDE-01.wav"));
        Set(serialized, "mkHide02", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-HIDE-02.wav"));
        Set(serialized, "mkHide03", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-HIDE-03.wav"));
        Set(serialized, "mkHide04", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-HIDE-04.wav"));
        Set(serialized, "mkHide05", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-HIDE-05.wav"));
        Set(serialized, "mkHide06", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-HIDE-06.wav"));
        Set(serialized, "mkDeath01", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-DEATH-01.wav"));
        Set(serialized, "mkDeath02", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-DEATH-02.wav"));
        Set(serialized, "mkDeath03", Clip("Assets/MainGame/Audio/Voice/Chapter1/VO_Ch1_MK-DEATH-03.wav"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);

        return data;
    }

    private static void BindScene(AudioData audioData)
    {
        var audioManager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
        if (audioManager == null)
            audioManager = new GameObject("AudioManager", typeof(AudioManager)).GetComponent<AudioManager>();

        var serialized = new SerializedObject(audioManager);
        Set(serialized, "audioData", audioData);
        Set(serialized, "musicSource", EnsureAudioSource(audioManager.transform, "MusicSource", true, null));
        Set(serialized, "ambienceSource", EnsureAudioSource(audioManager.transform, "AmbienceSource", true, null));
        Set(serialized, "sfxSource", EnsureAudioSource(audioManager.transform, "SfxSource", false, null));
        Set(serialized, "uiSource", EnsureAudioSource(audioManager.transform, "UiSource", false, null));
        Set(serialized, "voiceSource", EnsureAudioSource(audioManager.transform, "VoiceSource", false, null));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(audioManager);

        var doorClip = Clip("Assets/MainGame/Audio/SFX/SFX_Door_OpenSlow.mp3");
        foreach (var door in UnityEngine.Object.FindObjectsByType<DoorSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            door.doorAudioSource = EnsureAudioSource(door.transform, "DoorAudioSource", false, doorClip);
            EditorUtility.SetDirty(door);
        }

        foreach (var hiding in UnityEngine.Object.FindObjectsByType<HidingSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var hidingSerialized = new SerializedObject(hiding);
            Set(hidingSerialized, "doorAudioSource", EnsureAudioSource(hiding.transform, "HidingDoorAudioSource", false, doorClip));
            hidingSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hiding);
        }

        var pianoNote = Clip("Assets/MainGame/Audio/wav/c1.wav");
        foreach (var pianoUi in UnityEngine.Object.FindObjectsByType<PianoPuzzleUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var pianoSerialized = new SerializedObject(pianoUi);
            Set(pianoSerialized, "noteClip", pianoNote);
            Set(pianoSerialized, "noteClipC", pianoNote);
            Set(pianoSerialized, "noteClipD", Clip("Assets/MainGame/Audio/wav/d1.wav"));
            Set(pianoSerialized, "noteClipE", Clip("Assets/MainGame/Audio/wav/e1.wav"));
            Set(pianoSerialized, "noteClipF", Clip("Assets/MainGame/Audio/wav/f1.wav"));
            Set(pianoSerialized, "noteClipG", Clip("Assets/MainGame/Audio/wav/g1.wav"));
            Set(pianoSerialized, "noteClipA", Clip("Assets/MainGame/Audio/wav/a1.wav"));
            Set(pianoSerialized, "noteClipB", Clip("Assets/MainGame/Audio/wav/b1.wav"));
            Set(pianoSerialized, "noteClipHighC", Clip("Assets/MainGame/Audio/wav/c2.wav"));
            Set(pianoSerialized, "noteClipCSharp", Clip("Assets/MainGame/Audio/wav/c1s.wav"));
            Set(pianoSerialized, "noteClipDSharp", Clip("Assets/MainGame/Audio/wav/d1s.wav"));
            Set(pianoSerialized, "noteClipFSharp", Clip("Assets/MainGame/Audio/wav/f1s.wav"));
            Set(pianoSerialized, "noteClipGSharp", Clip("Assets/MainGame/Audio/wav/g1s.wav"));
            Set(pianoSerialized, "noteClipASharp", Clip("Assets/MainGame/Audio/wav/a1s.wav"));
            pianoSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pianoUi);
        }

        var typewriterClick = Clip("Assets/MainGame/Audio/SFX/SFX_DialogueAdvance_Click.mp3");
        foreach (var manager in UnityEngine.Object.FindObjectsByType<CutSceneManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var managerSerialized = new SerializedObject(manager);
            Set(managerSerialized, "typewriterClickClip", typewriterClick);
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        foreach (var sequence in UnityEngine.Object.FindObjectsByType<CutSceneSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (var point in sequence.Points)
            {
                if (point != null)
                    point.overrideAudioClip = IntroVoiceClip(point.dialogueId);
            }

            EditorUtility.SetDirty(sequence);
        }
    }

    private static void BindIntroDialogueData()
    {
        var data = AssetDatabase.LoadAssetAtPath<CutSceneDialogueData>(IntroDialogueDataPath);
        if (data == null)
            return;

        foreach (var line in data.Lines)
        {
            if (line != null)
                line.audioClip = IntroVoiceClip(line.id);
        }

        EditorUtility.SetDirty(data);
    }

    private static AudioClip IntroVoiceClip(string id)
    {
        var path = id switch
        {
            "intro_arrived_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_01.wav",
            "intro_villa_history" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_02.wav",
            "intro_thesis_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_03.wav",
            "intro_front_sketch_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_04.wav",
            "intro_enter_window_line" => "Assets/MainGame/Audio/Voice/VO_Ch1_Intro_05.wav",
            _ => null
        };

        return string.IsNullOrEmpty(path) ? null : Clip(path);
    }

    private static AudioSource EnsureAudioSource(Transform parent, string name, bool loop, AudioClip clip)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var childObject = new GameObject(name, typeof(AudioSource));
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        var source = child.GetComponent<AudioSource>() ?? child.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.clip = clip;
        EditorUtility.SetDirty(source);
        return source;
    }

    private static AudioClip Clip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        var fullPath = Path.Combine(parent, folderName).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }
}
