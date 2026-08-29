using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameSceneSettingsIntegrator
{
    private const string FontPath = "Assets/FpsHorrorKit/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string SpriteFolder = "Assets/MainGame/UI/Sprites";
    private const string AudioDataPath = "Assets/MainGame/Resources/Audio/AudioData.asset";

    private static readonly Color PanelColor = new(0.01f, 0.035f, 0.055f, 0.78f);
    private static readonly Color ButtonColor = new(0.01f, 0.035f, 0.050f, 0.86f);
    private static readonly Color TextColor = new(0.86f, 0.92f, 0.96f, 1f);
    private static readonly Color AccentColor = new(0.55f, 0.88f, 1f, 1f);

    private static TMP_FontAsset font;
    private static readonly Dictionary<string, Sprite> sprites = new();

    [MenuItem("Tools/MainGame/Apply Game Settings Integration")]
    public static void Apply()
    {
        LoadAssets();

        var canvas = MainGameEditorCanvasUtility.FindOrCreateScreenCanvas();
        if (canvas == null)
        {
            Debug.LogError("Canvas not found in current scene.");
            return;
        }

        var gameController = Object.FindFirstObjectByType<GameController>();
        if (gameController == null)
        {
            Debug.LogError("GameController not found in current scene.");
            return;
        }

        EnsurePersistentManagers();

        var pauseMenu = RebuildPauseMenu(canvas.transform);
        var settingUI = FindTransform("SettingUI")?.gameObject;
        if (settingUI == null)
        {
            Debug.LogError("SettingUI not found in current scene.");
            return;
        }

        var settingsController = settingUI.GetComponent<SettingsUIController>() ?? settingUI.AddComponent<SettingsUIController>();
        RemoveIfFound(settingUI.transform, "ShortcutPanel");
        WireSettingsController(settingsController, settingUI, pauseMenu);
        WireGameController(gameController, pauseMenu, settingUI);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("Game scene settings integration applied.");
    }

    private static void LoadAssets()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        sprites.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites[System.IO.Path.GetFileNameWithoutExtension(path)] = sprite;
        }
    }

    private static void EnsurePersistentManagers()
    {
        if (Object.FindFirstObjectByType<GameData>() == null)
            new GameObject("GameData", typeof(GameData));

        var audioManager = Object.FindFirstObjectByType<AudioManager>();
        if (audioManager == null)
            audioManager = new GameObject("AudioManager", typeof(AudioManager)).GetComponent<AudioManager>();

        var audioObject = audioManager.gameObject;
        var musicSource = EnsureAudioSource(audioObject.transform, "MusicSource", true);
        var ambienceSource = EnsureAudioSource(audioObject.transform, "AmbienceSource", false);
        var sfxSource = EnsureAudioSource(audioObject.transform, "SfxSource", false);
        var uiSource = EnsureAudioSource(audioObject.transform, "UiSource", false);

        var serialized = new SerializedObject(audioManager);
        Set(serialized, "audioData", AssetDatabase.LoadAssetAtPath<AudioData>(AudioDataPath));
        Set(serialized, "musicSource", musicSource);
        Set(serialized, "ambienceSource", ambienceSource);
        Set(serialized, "sfxSource", sfxSource);
        Set(serialized, "uiSource", uiSource);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioSource EnsureAudioSource(Transform parent, string name, bool loop)
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
        return source;
    }

    private static GameObject RebuildPauseMenu(Transform canvas)
    {
        var existing = canvas.Find("PauseMenuUI");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        var root = AddObject(canvas, "PauseMenuUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PauseMenuController));
        SetStretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var dim = root.GetComponent<Image>();
        dim.sprite = SpriteOrNull("vignette");
        dim.type = Image.Type.Sliced;
        dim.color = new Color(0.004f, 0.015f, 0.026f, 0.42f);
        dim.raycastTarget = true;

        var panel = AddImage(root.transform, "PauseMenuPanel", SpriteOrNull("menu_chapter_card"), PanelColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(430, 360), Vector2.zero);

        var continueButton = AddPauseButton(panel.transform, "ContinueButton", "Tiếp tục", 92, true);
        var settingsButton = AddPauseButton(panel.transform, "SettingsButton", "Cài đặt", 0, false);
        var mainMenuButton = AddPauseButton(panel.transform, "MainMenuButton", "Về menu chính", -92, false);

        var serialized = new SerializedObject(root.GetComponent<PauseMenuController>());
        Set(serialized, "continueButton", continueButton);
        Set(serialized, "settingsButton", settingsButton);
        Set(serialized, "mainMenuButton", mainMenuButton);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        return root;
    }

    private static Button AddPauseButton(Transform root, string name, string label, float y, bool selected)
    {
        var image = AddImage(root, name, SpriteOrNull(selected ? "menu_button_selected" : "menu_button"), ButtonColor, Image.Type.Sliced);
        image.raycastTarget = true;
        SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(360, 76), new Vector2(0, y));

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateButtonColors();

        var text = AddText(image.transform, "LabelText", label, 30, TextColor, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, new Vector2(12, 0), new Vector2(-12, 0));
        return button;
    }

    private static void WireSettingsController(SettingsUIController controller, GameObject settingUI, GameObject pauseMenu)
    {
        var serialized = new SerializedObject(controller);
        Set(serialized, "panelToHide", settingUI);
        Set(serialized, "backTargetPanel", pauseMenu);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireGameController(GameController controller, GameObject pauseMenu, GameObject settingUI)
    {
        var serialized = new SerializedObject(controller);
        Set(serialized, "pauseUI", pauseMenu);
        Set(serialized, "settingsUI", settingUI);
        Set(serialized, "mainMenuSceneName", "Menu");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindTransform(string name)
    {
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == name && transform.gameObject.scene == EditorSceneManager.GetActiveScene())
                return transform;
        }

        return null;
    }

    private static void RemoveIfFound(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
    {
        var gameObject = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text AddText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var gameObject = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var tmp = gameObject.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.fontStyle = FontStyles.SmallCaps;
        tmp.outlineColor = new Color(0.01f, 0.04f, 0.06f, 0.92f);
        tmp.outlineWidth = size >= 22f ? 0.12f : 0.08f;

        var shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.55f, 0.72f, 0.22f);
        shadow.effectDistance = new Vector2(1.2f, -1.2f);
        return tmp;
    }

    private static GameObject AddObject(Transform parent, string name, params System.Type[] components)
    {
        var gameObject = new GameObject(name, components);
        gameObject.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Sprite SpriteOrNull(string name)
    {
        return sprites.TryGetValue(name, out var sprite) ? sprite : null;
    }

    private static ColorBlock CreateButtonColors()
    {
        return new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.68f, 0.92f, 1f, 1f),
            pressedColor = new Color(0.40f, 0.72f, 0.85f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
        rect.localScale = Vector3.one;
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        serialized.FindProperty(propertyName).objectReferenceValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        serialized.FindProperty(propertyName).stringValue = value;
    }
}
