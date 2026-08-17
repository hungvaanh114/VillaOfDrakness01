using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/MainGame/Menu.unity";
    private const string GameScenePath = "Assets/MainGame/Game.unity";
    private const string SpriteFolder = "Assets/MainGame/UI/Sprites";
    private const string FontFolder = "Assets/MainGame/UI/Fonts";
    private const string FontPath = FontFolder + "/HorrorSerif.ttf";
    private const string FontAssetPath = "Assets/FpsHorrorKit/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string AudioResourceFolder = "Assets/MainGame/Resources/Audio";
    private const string AudioDataPath = AudioResourceFolder + "/AudioData.asset";

    private static readonly Color TextColor = new(0.86f, 0.92f, 0.96f, 1f);
    private static readonly Color MutedTextColor = new(0.62f, 0.72f, 0.80f, 1f);
    private static readonly Color AccentColor = new(0.55f, 0.88f, 1f, 1f);
    private static readonly Color PanelColor = new(0.01f, 0.035f, 0.055f, 0.78f);
    private static readonly Color ButtonColor = new(0.01f, 0.035f, 0.050f, 0.86f);

    private enum TextRole { Title, Header, Button, Body, Meta }

    private static TMP_FontAsset font;
    private static Dictionary<string, Sprite> sprites;
    private static AudioData audioData;

    [MenuItem("Tools/MainGame/Create Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        EditorSceneManager.SaveOpenScenes();
        EnsureAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Menu";

        var camera = CreateCamera();
        var canvas = CreateCanvas(camera);
        var menuRoot = AddObject(canvas.transform, "MainMenuRoot", typeof(RectTransform));
        SetStretch(menuRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var controller = menuRoot.AddComponent<MainMenuController>();
        CreatePersistentManagers();

        AddBackground(canvas.transform);
        AddFrame(canvas.transform);
        AddTitle(canvas.transform);

        var mainPanel = CreateMainPanel(canvas.transform, out var buttons);
        CreateChapterPickerCard(
            mainPanel.transform,
            out var previousChapterButton,
            out var nextChapterButton,
            out var chapterPlayButton,
            out var saveTexts,
            out var lockedChapterOverlay,
            out var lockedChapterText);
        var settingsPanel = CreateSettingsPanel(canvas.transform, mainPanel, out var settingsController);

        SetControllerReferences(
            controller,
            mainPanel,
            settingsPanel,
            buttons,
            previousChapterButton,
            nextChapterButton,
            chapterPlayButton,
            saveTexts,
            lockedChapterOverlay,
            lockedChapterText,
            settingsController);

        CreateEventSystem();
        UpdateBuildSettings();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Main menu scene created at {ScenePath}");
    }

    private static Camera CreateCamera()
    {
        var cameraObject = AddObject(null, "MenuCamera", typeof(Camera), typeof(AudioListener));
        cameraObject.layer = 0;
        cameraObject.transform.position = new Vector3(0, 0, -10);

        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.002f, 0.010f, 0.018f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        return camera;
    }

    private static void CreatePersistentManagers()
    {
        var dataObject = AddObject(null, "GameData", typeof(GameData));
        dataObject.layer = 0;

        var audioObject = AddObject(null, "AudioManager", typeof(AudioManager));
        audioObject.layer = 0;
        var musicSource = CreateAudioSource(audioObject.transform, "MusicSource");
        var ambienceSource = CreateAudioSource(audioObject.transform, "AmbienceSource");
        var sfxSource = CreateAudioSource(audioObject.transform, "SfxSource");
        var uiSource = CreateAudioSource(audioObject.transform, "UiSource");

        var serialized = new SerializedObject(audioObject.GetComponent<AudioManager>());
        Set(serialized, "audioData", audioData);
        Set(serialized, "musicSource", musicSource);
        Set(serialized, "ambienceSource", ambienceSource);
        Set(serialized, "sfxSource", sfxSource);
        Set(serialized, "uiSource", uiSource);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioSource CreateAudioSource(Transform parent, string name)
    {
        var sourceObject = AddObject(parent, name, typeof(AudioSource));
        sourceObject.layer = 0;
        var source = sourceObject.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = name is "MusicSource" or "AmbienceSource";
        return source;
    }

    private static GameObject CreateCanvas(Camera camera)
    {
        var canvasObject = AddObject(null, "Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        SetStretch(canvasObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        return canvasObject;
    }

    private static void AddBackground(Transform root)
    {
        var background = AddImage(root, "BackgroundImage", sprites["menu_background_clean"], Color.white, Image.Type.Simple);
        SetStretch(background.rectTransform, Vector2.zero, Vector2.zero);

        var dim = AddImage(root, "BlueNightDimming", sprites["vignette"], new Color(0.01f, 0.04f, 0.08f, 0.26f), Image.Type.Sliced);
        SetStretch(dim.rectTransform, Vector2.zero, Vector2.zero);
    }

    private static void AddFrame(Transform root)
    {
        var corners = new[]
        {
            ("CornerTopLeft", new Vector2(0f, 1f), Vector3.zero),
            ("CornerTopRight", new Vector2(1f, 1f), new Vector3(0, 180, 0)),
            ("CornerBottomLeft", new Vector2(0f, 0f), new Vector3(180, 0, 0)),
            ("CornerBottomRight", new Vector2(1f, 0f), new Vector3(180, 180, 0))
        };

        foreach (var (name, anchor, rotation) in corners)
        {
            var corner = AddImage(root, name, sprites["corner"], new Color(0.86f, 0.92f, 0.98f, 0.34f), Image.Type.Simple);
            SetRect(corner.rectTransform, anchor, new Vector2(190, 190), Vector2.zero);
            corner.rectTransform.localEulerAngles = rotation;
        }
    }

    private static void AddTitle(Transform root)
    {
        var title = AddText(root, "GameTitleText", "BIỆT THỰ BÓNG TỐI", TextRole.Title, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(820, 88), new Vector2(0, -108));

        var divider = AddImage(root, "TitleDivider", sprites["divider"], new Color(0.82f, 0.92f, 0.98f, 0.82f), Image.Type.Simple);
        SetRect(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(620, 26), new Vector2(0, -196));
    }

    private static GameObject CreateMainPanel(Transform root, out Button[] buttons)
    {
        var panel = AddObject(root, "MainPanel", typeof(RectTransform));
        SetStretch(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        var buttonPanel = AddObject(panel.transform, "MenuButtons", typeof(RectTransform));
        SetRect(buttonPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(460, 390), new Vector2(0, 54));

        buttons = new[]
        {
            AddMenuButton(buttonPanel.transform, "ContinueButton", "Tiếp tục", 132, true),
            AddMenuButton(buttonPanel.transform, "NewGameButton", "Chơi mới", 44, false),
            AddMenuButton(buttonPanel.transform, "SettingsButton", "Cài đặt", -44, false),
            AddMenuButton(buttonPanel.transform, "QuitButton", "Thoát game", -132, false)
        };

        return panel;
    }

    private static GameObject CreateChapterPickerCard(
        Transform root,
        out Button previousButton,
        out Button nextButton,
        out Button playButton,
        out TMP_Text[] saveTexts,
        out GameObject lockedOverlay,
        out TMP_Text lockedText)
    {
        var card = AddImage(root, "ChapterPickerCard", sprites["menu_chapter_card"], PanelColor, Image.Type.Sliced);
        SetRect(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(640, 220), new Vector2(0, 128));
        card.raycastTarget = true;

        playButton = card.gameObject.AddComponent<Button>();
        playButton.targetGraphic = card;
        playButton.colors = CreateButtonColors();

        var preview = AddImage(card.transform, "ChapterPreviewImage", sprites["save_preview"], Color.white, Image.Type.Simple);
        SetRect(preview.rectTransform, new Vector2(0f, 0.5f), new Vector2(165, 145), new Vector2(42, 0));

        var chapterText = AddText(card.transform, "SaveChapterText", "Chương 1", TextRole.Header, TextAlignmentOptions.Left);
        SetRect(chapterText.rectTransform, new Vector2(0f, 1f), new Vector2(320, 36), new Vector2(245, -48));

        AddInfoRow(card.transform, "LastSave", "Lần lưu gần nhất", "25/05/2025  22:47", sprites["icon_calendar"], -98, out var lastValue);
        AddInfoRow(card.transform, "PlayTime", "Thời gian chơi", "01:32:18", sprites["icon_clock"], -144, out var timeValue);

        lockedOverlay = AddImage(card.transform, "LockedChapterOverlay", sprites["chapter_locked_preview"], Color.white, Image.Type.Sliced).gameObject;
        SetStretch(lockedOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        var chainA = AddImage(lockedOverlay.transform, "ChainA", sprites["chain_overlay"], new Color(0.66f, 0.88f, 0.95f, 0.56f), Image.Type.Simple);
        SetRect(chainA.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(620, 88), new Vector2(0, 34));
        chainA.rectTransform.localEulerAngles = new Vector3(0, 0, -7);

        var chainB = AddImage(lockedOverlay.transform, "ChainB", sprites["chain_overlay"], new Color(0.42f, 0.72f, 0.84f, 0.42f), Image.Type.Simple);
        SetRect(chainB.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(620, 88), new Vector2(0, -42));
        chainB.rectTransform.localEulerAngles = new Vector3(0, 0, 8);

        var lockIcon = AddImage(lockedOverlay.transform, "LockIcon", sprites["icon_lock"], new Color(0.86f, 0.96f, 1f, 0.95f), Image.Type.Simple);
        SetRect(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(78, 96), new Vector2(0, 12));

        lockedText = AddText(lockedOverlay.transform, "LockedChapterText", "Chương 2 đang bị khóa", TextRole.Body, TextAlignmentOptions.Center);
        SetRect(lockedText.rectTransform, new Vector2(0.5f, 0f), new Vector2(360, 34), new Vector2(0, 32));
        lockedOverlay.SetActive(false);

        previousButton = AddArrowButton(card.transform, "PreviousChapterButton", "<", new Vector2(-354, 0));
        nextButton = AddArrowButton(card.transform, "NextChapterButton", ">", new Vector2(354, 0));

        saveTexts = new[] { chapterText, lastValue, timeValue };
        return card.gameObject;
    }

    private static GameObject CreateSettingsPanel(Transform root, GameObject mainPanel, out SettingsUIController controller)
    {
        var panel = AddImage(root, "SettingsPanel", sprites["menu_chapter_card"], PanelColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(960, 575), new Vector2(0, -18));
        panel.gameObject.SetActive(false);

        controller = panel.gameObject.AddComponent<SettingsUIController>();
        controller.panelToHide = panel.gameObject;
        controller.backTargetPanel = mainPanel;

        var title = AddText(panel.transform, "SettingsTitleText", "CÀI ĐẶT", TextRole.Header, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(340, 44), new Vector2(70, -46));

        var topLine = AddImage(panel.transform, "SettingsTopSeparator", sprites["smoke_line"], new Color(0.55f, 0.78f, 0.88f, 0.35f), Image.Type.Simple);
        SetRect(topLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(810, 14), new Vector2(0, -90));

        controller.resolutionDropdown = AddSettingsDropdown(panel.transform, "ResolutionDropdown", "Độ phân giải", 0, GameSettings.ResolutionLabels);
        controller.displayModeDropdown = AddSettingsDropdown(panel.transform, "DisplayModeDropdown", "Chế độ hiển thị", 1, GameSettings.DisplayModeLabels);
        controller.brightnessSlider = AddSettingsSlider(panel.transform, "BrightnessSlider", "Độ sáng", 2, 60, out controller.brightnessValueText);

        var middleLine = AddImage(panel.transform, "SettingsAudioSeparator", sprites["smoke_line"], new Color(0.55f, 0.78f, 0.88f, 0.28f), Image.Type.Simple);
        SetRect(middleLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(810, 14), new Vector2(0, -255));

        controller.masterVolumeSlider = AddSettingsSlider(panel.transform, "MasterVolumeSlider", "Âm lượng tổng", 4, 80, out controller.masterVolumeValueText);
        controller.musicVolumeSlider = AddSettingsSlider(panel.transform, "MusicVolumeSlider", "Âm nhạc", 5, 40, out controller.musicVolumeValueText);
        controller.sfxVolumeSlider = AddSettingsSlider(panel.transform, "SfxVolumeSlider", "Hiệu ứng", 6, 70, out controller.sfxVolumeValueText);

        controller.applyButton = AddSmallSettingsButton(panel.transform, "SettingsApplyButton", "ÁP DỤNG", new Vector2(-300, 44), true);
        controller.resetButton = AddSmallSettingsButton(panel.transform, "SettingsResetButton", "KHÔI PHỤC", new Vector2(0, 44), false);
        controller.backButton = AddSmallSettingsButton(panel.transform, "SettingsBackButton", "QUAY LẠI", new Vector2(300, 44), false);
        return panel.gameObject;
    }

    private static GameObject CreateSettingsPanel(Transform root, out Button applyButton, out Button backButton, out Slider volumeSlider, out Toggle fullscreenToggle)
    {
        var panel = AddImage(root, "SettingsPanel", sprites["menu_chapter_card"], PanelColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720, 430), new Vector2(0, -8));
        panel.gameObject.SetActive(false);

        var title = AddText(panel.transform, "SettingsTitleText", "CÀI ĐẶT", TextRole.Header, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(420, 44), new Vector2(0, -45));

        var volumeLabel = AddText(panel.transform, "VolumeLabelText", "Âm lượng tổng", TextRole.Body, TextAlignmentOptions.Left);
        SetRect(volumeLabel.rectTransform, new Vector2(0f, 1f), new Vector2(260, 34), new Vector2(95, -132));

        volumeSlider = AddFilledSlider(panel.transform, "VolumeSlider", 0.8f, new Vector2(120, -172));
        fullscreenToggle = AddToggle(panel.transform, "FullscreenToggle", "Toàn màn hình", new Vector2(95, -238));

        applyButton = AddMenuButton(panel.transform, "SettingsApplyButton", "Áp dụng", -130, true);
        SetRect(applyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(260, 60), new Vector2(-160, -130));

        backButton = AddMenuButton(panel.transform, "SettingsBackButton", "Quay lại", -130, false);
        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(260, 60), new Vector2(160, -130));

        return panel.gameObject;
    }

    private static TMP_Dropdown AddSettingsDropdown(Transform root, string name, string label, int row, string[] options)
    {
        AddSettingsLabel(root, name + "LabelText", label, row);

        var image = AddImage(root, name, sprites["menu_button"], new Color(0.012f, 0.04f, 0.06f, 0.62f), Image.Type.Sliced);
        image.raycastTarget = true;
        SetRect(image.rectTransform, new Vector2(1f, 1f), new Vector2(360, 46), new Vector2(-84, -124 - row * 62));

        var dropdown = image.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = image;
        dropdown.options.Clear();
        foreach (var option in options)
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));

        var caption = AddText(image.transform, "CaptionText", options.Length > 0 ? options[0] : string.Empty, TextRole.Body, TextAlignmentOptions.Center);
        SetStretch(caption.rectTransform, new Vector2(36, 0), new Vector2(-50, 0));
        dropdown.captionText = caption;

        var arrow = AddText(image.transform, "ArrowText", "⌄", TextRole.Body, TextAlignmentOptions.Center);
        SetRect(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(32, 32), new Vector2(-22, 0));

        BuildSettingsDropdownTemplate(image.transform, dropdown);
        return dropdown;
    }

    private static void BuildSettingsDropdownTemplate(Transform root, TMP_Dropdown dropdown)
    {
        var template = AddImage(root, "Template", sprites["menu_chapter_card"], new Color(0.01f, 0.035f, 0.055f, 0.96f), Image.Type.Sliced);
        template.gameObject.SetActive(false);
        SetRect(template.rectTransform, new Vector2(0.5f, 0f), new Vector2(360, 176), new Vector2(0, -90));

        var scrollRect = template.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        var viewport = AddImage(template.transform, "Viewport", null, Color.clear, Image.Type.Simple);
        viewport.gameObject.AddComponent<RectMask2D>();
        SetStretch(viewport.rectTransform, new Vector2(8, 8), new Vector2(-8, -8));

        var content = AddObject(viewport.transform, "Content", typeof(RectTransform));
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0, 40);
        contentRect.anchoredPosition = Vector2.zero;

        var item = AddObject(content.transform, "Item", typeof(RectTransform), typeof(Toggle), typeof(Image));
        var itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 0.5f);
        itemRect.anchorMax = new Vector2(1, 0.5f);
        itemRect.sizeDelta = new Vector2(0, 38);
        itemRect.anchoredPosition = Vector2.zero;

        var itemImage = item.GetComponent<Image>();
        itemImage.sprite = sprites["menu_button"];
        itemImage.type = Image.Type.Sliced;
        itemImage.color = new Color(0.02f, 0.08f, 0.1f, 0.58f);

        var checkmark = AddImage(item.transform, "Item Checkmark", sprites["diamond"], AccentColor, Image.Type.Simple);
        SetRect(checkmark.rectTransform, new Vector2(0f, 0.5f), new Vector2(16, 16), new Vector2(16, 0));

        var itemLabel = AddText(item.transform, "Item Label", "Option", TextRole.Body, TextAlignmentOptions.Left);
        SetStretch(itemLabel.rectTransform, new Vector2(42, 0), new Vector2(-14, 0));

        var toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemImage;
        toggle.graphic = checkmark;

        scrollRect.viewport = viewport.rectTransform;
        scrollRect.content = contentRect;
        dropdown.template = template.rectTransform;
        dropdown.itemText = itemLabel;
    }

    private static Slider AddSettingsSlider(Transform root, string name, string label, int row, int value, out TMP_Text valueText)
    {
        AddSettingsLabel(root, name + "LabelText", label, row);

        var sliderImage = AddImage(root, name, sprites["smoke_line"], new Color(0.23f, 0.67f, 0.86f, 0.26f), Image.Type.Simple);
        sliderImage.raycastTarget = true;
        SetRect(sliderImage.rectTransform, new Vector2(1f, 1f), new Vector2(400, 32), new Vector2(-128, -130 - row * 62));

        var slider = sliderImage.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.value = value;

        var fill = AddImage(sliderImage.transform, "Fill", sprites["smoke_line"], new Color(0.35f, 0.90f, 1f, 0.90f), Image.Type.Filled);
        ConfigureHorizontalFill(fill, value / 100f);
        SetStretch(fill.rectTransform, Vector2.zero, Vector2.zero);

        var handleArea = AddObject(sliderImage.transform, "Handle Slide Area", typeof(RectTransform));
        SetStretch(handleArea.GetComponent<RectTransform>(), new Vector2(0, -12), new Vector2(0, 12));

        var handle = AddImage(handleArea.transform, "Handle", sprites["diamond"], new Color(0.90f, 0.98f, 1f, 0.96f), Image.Type.Simple);
        handle.raycastTarget = true;
        SetRect(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30, 44), Vector2.zero);

        slider.fillRect = null;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;

        var filledGraphic = sliderImage.gameObject.AddComponent<FilledSliderGraphic>();
        filledGraphic.slider = slider;
        filledGraphic.fillImage = fill;

        valueText = AddText(root, name + "ValueText", value.ToString(), TextRole.Body, TextAlignmentOptions.Right);
        SetRect(valueText.rectTransform, new Vector2(1f, 1f), new Vector2(50, 30), new Vector2(-60, -128 - row * 62));
        return slider;
    }

    private static void AddSettingsLabel(Transform root, string name, string text, int row)
    {
        var label = AddText(root, name, text, TextRole.Body, TextAlignmentOptions.Left);
        SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(300, 32), new Vector2(70, -126 - row * 62));
    }

    private static Button AddSmallSettingsButton(Transform root, string name, string label, Vector2 position, bool selected)
    {
        var image = AddImage(root, name, sprites[selected ? "menu_button_selected" : "menu_button"], ButtonColor, Image.Type.Sliced);
        image.raycastTarget = true;
        SetRect(image.rectTransform, new Vector2(0.5f, 0f), new Vector2(240, 58), position);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateButtonColors();

        var text = AddText(image.transform, "LabelText", label, TextRole.Body, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, new Vector2(12, 0), new Vector2(-12, 0));
        return button;
    }

    private static Button AddMenuButton(Transform root, string name, string label, float y, bool selected)
    {
        var image = AddImage(root, name, sprites[selected ? "menu_button_selected" : "menu_button"], ButtonColor, Image.Type.Sliced);
        image.raycastTarget = true;
        SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(430, 74), new Vector2(0, y));

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateButtonColors();

        var text = AddText(image.transform, "LabelText", label, TextRole.Button, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, new Vector2(18, 0), new Vector2(-18, 0));
        return button;
    }

    private static Button AddArrowButton(Transform root, string name, string label, Vector2 position)
    {
        var image = AddImage(root, name, sprites["menu_arrow_button"], new Color(0.03f, 0.13f, 0.18f, 0.86f), Image.Type.Sliced);
        image.raycastTarget = true;
        SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(58, 86), position);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateButtonColors();

        var text = AddText(image.transform, "LabelText", label, TextRole.Button, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    private static void AddInfoRow(Transform parent, string prefix, string labelValue, string value, Sprite icon, float y, out TMP_Text valueText)
    {
        var iconImage = AddImage(parent, prefix + "Icon", icon, new Color(0.86f, 0.92f, 0.96f, 0.92f), Image.Type.Simple);
        SetRect(iconImage.rectTransform, new Vector2(0f, 1f), new Vector2(28, 28), new Vector2(245, y - 3));

        var label = AddText(parent, prefix + "LabelText", labelValue, TextRole.Body, TextAlignmentOptions.Left);
        SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(230, 30), new Vector2(286, y));

        valueText = AddText(parent, prefix + "ValueText", value, TextRole.Meta, TextAlignmentOptions.Left);
        SetRect(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(210, 30), new Vector2(500, y));
    }

    private static Slider AddFilledSlider(Transform root, string name, float value, Vector2 pos)
    {
        var background = AddImage(root, name, sprites["smoke_line"], new Color(0.20f, 0.65f, 0.84f, 0.24f), Image.Type.Simple);
        background.raycastTarget = true;
        SetRect(background.rectTransform, new Vector2(0f, 1f), new Vector2(430, 32), pos);

        var slider = background.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;

        var fill = AddImage(background.transform, "Fill", sprites["smoke_line"], new Color(0.42f, 0.92f, 1f, 0.92f), Image.Type.Filled);
        ConfigureHorizontalFill(fill, value);
        SetStretch(fill.rectTransform, Vector2.zero, Vector2.zero);

        var handleArea = AddObject(background.transform, "Handle Slide Area", typeof(RectTransform));
        SetStretch(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        var handle = AddImage(handleArea.transform, "Handle", sprites["diamond"], new Color(0.90f, 0.98f, 1f, 0.96f), Image.Type.Simple);
        handle.raycastTarget = true;
        SetRect(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(28, 40), Vector2.zero);

        slider.fillRect = null;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;

        var graphic = background.gameObject.AddComponent<FilledSliderGraphic>();
        graphic.slider = slider;
        graphic.fillImage = fill;
        return slider;
    }

    private static Toggle AddToggle(Transform root, string name, string label, Vector2 pos)
    {
        var toggleRoot = AddObject(root, name, typeof(RectTransform), typeof(Toggle));
        SetRect(toggleRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(430, 42), pos);

        var box = AddImage(toggleRoot.transform, "Box", sprites["keycap"], new Color(0.01f, 0.04f, 0.06f, 0.80f), Image.Type.Sliced);
        box.raycastTarget = true;
        SetRect(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(34, 34), new Vector2(0, 0));

        var check = AddImage(box.transform, "Checkmark", sprites["diamond"], AccentColor, Image.Type.Simple);
        SetRect(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(22, 22), Vector2.zero);

        var text = AddText(toggleRoot.transform, "LabelText", label, TextRole.Body, TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(320, 34), new Vector2(54, 0));

        var toggle = toggleRoot.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.isOn = true;
        return toggle;
    }

    private static void SetControllerReferences(
        MainMenuController controller,
        GameObject mainPanel,
        GameObject settingsPanel,
        Button[] buttons,
        Button previousChapterButton,
        Button nextChapterButton,
        Button chapterPlayButton,
        TMP_Text[] saveTexts,
        GameObject lockedChapterOverlay,
        TMP_Text lockedChapterText,
        SettingsUIController settingsController)
    {
        var serialized = new SerializedObject(controller);
        Set(serialized, "gameSceneName", "Game");
        Set(serialized, "mainPanel", mainPanel);
        Set(serialized, "settingsPanel", settingsPanel);
        Set(serialized, "chapterCount", 4);
        Set(serialized, "unlockedChapterCount", 1);
        Set(serialized, "continueButton", buttons[0]);
        Set(serialized, "newGameButton", buttons[1]);
        Set(serialized, "settingsButton", buttons[2]);
        Set(serialized, "quitButton", buttons[3]);
        Set(serialized, "previousChapterButton", previousChapterButton);
        Set(serialized, "nextChapterButton", nextChapterButton);
        Set(serialized, "chapterPlayButton", chapterPlayButton);
        Set(serialized, "chapterText", saveTexts[0]);
        Set(serialized, "lastSaveText", saveTexts[1]);
        Set(serialized, "playTimeText", saveTexts[2]);
        Set(serialized, "lockedChapterOverlay", lockedChapterOverlay);
        Set(serialized, "lockedChapterText", lockedChapterText);
        Set(serialized, "settingsController", settingsController);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static TMP_Text AddText(Transform parent, string name, string value, TextRole role, TextAlignmentOptions alignment)
    {
        var textObject = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        ApplyTextStyle(text, role);
        return text;
    }

    private static void ApplyTextStyle(TMP_Text text, TextRole role)
    {
        text.color = role == TextRole.Header ? AccentColor : TextColor;
        text.fontStyle = FontStyles.SmallCaps;
        text.outlineColor = new Color(0f, 0.025f, 0.04f, 0.95f);

        switch (role)
        {
            case TextRole.Title:
                text.fontSize = 58;
                text.characterSpacing = 12;
                text.outlineWidth = 0.18f;
                break;
            case TextRole.Header:
                text.fontSize = 29;
                text.characterSpacing = 2;
                text.outlineWidth = 0.14f;
                break;
            case TextRole.Button:
                text.fontSize = 31;
                text.characterSpacing = 1;
                text.outlineWidth = 0.12f;
                break;
            case TextRole.Body:
                text.fontSize = 21;
                text.characterSpacing = 0;
                text.outlineWidth = 0.08f;
                break;
            case TextRole.Meta:
                text.fontSize = 19;
                text.characterSpacing = 0;
                text.outlineWidth = 0.07f;
                text.color = MutedTextColor;
                break;
        }

        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.58f, 0.74f, role == TextRole.Title ? 0.45f : 0.22f);
        shadow.effectDistance = role == TextRole.Title ? new Vector2(2f, -2f) : new Vector2(1.2f, -1.2f);
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
    {
        var imageObject = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject AddObject(Transform parent, string name, params System.Type[] components)
    {
        var gameObject = new GameObject(name, components);
        gameObject.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
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

    private static void CreateEventSystem()
    {
        AddObject(null, "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static void UpdateBuildSettings()
    {
        var paths = new List<string> { ScenePath, GameScenePath };
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!paths.Contains(scene.path))
                paths.Add(scene.path);
        }

        var scenes = new EditorBuildSettingsScene[paths.Count];
        for (var i = 0; i < paths.Count; i++)
            scenes[i] = new EditorBuildSettingsScene(paths[i], true);
        EditorBuildSettings.scenes = scenes;
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

    private static void ConfigureHorizontalFill(Image image, float amount)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = Mathf.Clamp01(amount);
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        serialized.FindProperty(propertyName).objectReferenceValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        serialized.FindProperty(propertyName).stringValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, int value)
    {
        serialized.FindProperty(propertyName).intValue = value;
    }

    private static void EnsureAssets()
    {
        EnsureFolders();
        EnsureFontAsset();
        EnsureAudioDataAsset();
        CreateMenuBackground();
        CreateVignette();
        CreateCorner();
        CreateDivider();
        CreateDiamond();
        CreateKeycap();
        CreateSmokeLine();
        CreateMenuButton("menu_button", false);
        CreateMenuButton("menu_button_selected", true);
        CreateArrowButtonSprite();
        CreateChapterCard();
        CreateSavePreview();
        CreateLockedChapterPreview();
        CreateChainOverlay();
        CreateLockIcon();
        CreateCalendarIcon();
        CreateClockIcon();

        AssetDatabase.Refresh();

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        audioData = AssetDatabase.LoadAssetAtPath<AudioData>(AudioDataPath);
        sprites = new Dictionary<string, Sprite>();
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites[Path.GetFileNameWithoutExtension(path)] = sprite;
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/MainGame/UI"))
            AssetDatabase.CreateFolder("Assets/MainGame", "UI");
        if (!AssetDatabase.IsValidFolder(SpriteFolder))
            AssetDatabase.CreateFolder("Assets/MainGame/UI", "Sprites");
        if (!AssetDatabase.IsValidFolder(FontFolder))
            AssetDatabase.CreateFolder("Assets/MainGame/UI", "Fonts");
        if (!AssetDatabase.IsValidFolder("Assets/MainGame/Resources"))
            AssetDatabase.CreateFolder("Assets/MainGame", "Resources");
        if (!AssetDatabase.IsValidFolder(AudioResourceFolder))
            AssetDatabase.CreateFolder("Assets/MainGame/Resources", "Audio");
    }

    private static void EnsureFontAsset()
    {
        if (!File.Exists(FontPath))
            File.Copy("C:/Windows/Fonts/georgia.ttf", FontPath, false);

        AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
    }

    private static void EnsureAudioDataAsset()
    {
        var data = AssetDatabase.LoadAssetAtPath<AudioData>(AudioDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<AudioData>();
            AssetDatabase.CreateAsset(data, AudioDataPath);
        }

        EditorUtility.SetDirty(data);
    }

    private static void CreateMenuBackground()
    {
        var texture = NewTexture(1600, 900);
        FillVerticalGradient(texture, new Color(0.006f, 0.020f, 0.036f, 1f), new Color(0.030f, 0.085f, 0.130f, 1f));
        DrawMoon(texture, 310, 132, 52);
        DrawVilla(texture, 330, 238, 990, 655);
        DrawTrees(texture);
        DrawFog(texture, 210, 760, 250, 520);
        DrawWetGround(texture);
        SaveSprite(texture, "menu_background_clean", Vector4.zero);
    }

    private static void CreateMenuButton(string name, bool selected)
    {
        var texture = NewTexture(512, 96);
        Fill(texture, new Color(0.008f, 0.030f, 0.044f, selected ? 0.88f : 0.72f));
        DrawRect(texture, 7, 7, 504, 88, new Color(0.74f, 0.94f, 1f, selected ? 0.90f : 0.58f), 2);
        DrawRect(texture, 14, 14, 497, 81, new Color(0.86f, 0.98f, 1f, selected ? 0.46f : 0.24f), 1);
        DrawLine(texture, 52, 48, 460, 48, new Color(0.32f, 0.80f, 1f, selected ? 0.20f : 0.10f), 10);
        DrawDiamond(texture, 30, 48, 16, new Color(0.58f, 0.90f, 1f, selected ? 0.95f : 0.65f));
        DrawDiamond(texture, 482, 48, 16, new Color(0.58f, 0.90f, 1f, selected ? 0.95f : 0.65f));
        AddScratches(texture, 512, 96, 34);
        SaveSprite(texture, name, new Vector4(24, 24, 24, 24));
    }

    private static void CreateArrowButtonSprite()
    {
        var texture = NewTexture(96, 128);
        Fill(texture, new Color(0.006f, 0.024f, 0.034f, 0.76f));
        DrawRect(texture, 8, 8, 87, 119, new Color(0.54f, 0.86f, 1f, 0.72f), 2);
        DrawDiamond(texture, 48, 64, 20, new Color(0.55f, 0.88f, 1f, 0.76f));
        SaveSprite(texture, "menu_arrow_button", new Vector4(18, 18, 18, 18));
    }

    private static void CreateChapterCard()
    {
        var texture = NewTexture(768, 264);
        Fill(texture, new Color(0.006f, 0.026f, 0.038f, 0.78f));
        DrawRect(texture, 8, 8, 759, 255, new Color(0.72f, 0.90f, 1f, 0.54f), 2);
        DrawRect(texture, 16, 16, 751, 247, new Color(0.86f, 0.98f, 1f, 0.24f), 1);
        DrawLine(texture, 70, 254, 698, 254, new Color(0.82f, 0.94f, 1f, 0.52f), 2);
        DrawLine(texture, 70, 10, 698, 10, new Color(0.82f, 0.94f, 1f, 0.52f), 2);
        DrawDiamond(texture, 384, 254, 12, new Color(0.70f, 0.92f, 1f, 0.84f));
        DrawDiamond(texture, 384, 10, 12, new Color(0.70f, 0.92f, 1f, 0.84f));
        AddScratches(texture, 768, 264, 56);
        SaveSprite(texture, "menu_chapter_card", new Vector4(26, 26, 26, 26));
    }

    private static void CreateSavePreview()
    {
        var texture = NewTexture(256, 160);
        FillVerticalGradient(texture, new Color(0.02f, 0.02f, 0.025f, 1), new Color(0.08f, 0.07f, 0.06f, 1));
        FillRect(texture, 18, 18, 238, 142, new Color(0.06f, 0.055f, 0.05f, 1));
        DrawRect(texture, 18, 18, 238, 142, new Color(0.35f, 0.28f, 0.20f, 1), 2);
        for (var i = 0; i < 6; i++)
        {
            var x = 40 + i * 32;
            DrawLine(texture, x, 24, x, 136, new Color(0.16f, 0.13f, 0.10f, 1), 2);
        }
        FillRect(texture, 170, 48, 185, 112, new Color(0.70f, 0.52f, 0.31f, 1));
        DrawCircle(texture, 178, 118, 18, new Color(0.95f, 0.78f, 0.52f, 0.34f), true);
        SaveSprite(texture, "save_preview", Vector4.zero);
    }

    private static void CreateLockedChapterPreview()
    {
        var texture = NewTexture(256, 160);
        FillVerticalGradient(texture, new Color(0.002f, 0.010f, 0.016f, 0.86f), new Color(0.006f, 0.026f, 0.038f, 0.92f));
        DrawRect(texture, 2, 2, 253, 157, new Color(0.36f, 0.60f, 0.68f, 0.40f), 2);
        for (var i = 0; i < 10; i++)
        {
            var y = 20 + i * 14;
            DrawLine(texture, 0, y, 255, y + Mathf.RoundToInt(Mathf.Sin(i * 0.8f) * 10), new Color(0.32f, 0.72f, 0.86f, 0.055f), 5);
        }
        AddScratches(texture, 256, 160, 32);
        SaveSprite(texture, "chapter_locked_preview", new Vector4(18, 18, 18, 18));
    }

    private static void CreateChainOverlay()
    {
        var texture = NewTexture(512, 72);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        for (var i = -1; i < 14; i++)
            DrawChainLink(texture, 22 + i * 42, 36, 34, 20, i % 2 == 0);
        SaveSprite(texture, "chain_overlay", Vector4.zero);
    }

    private static void CreateLockIcon()
    {
        var texture = NewTexture(128, 160);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawCircle(texture, 64, 58, 38, new Color(0.70f, 0.90f, 0.98f, 0.92f), false);
        FillRect(texture, 32, 68, 96, 82, new Color(0.70f, 0.90f, 0.98f, 0.92f));
        FillRect(texture, 24, 72, 104, 136, new Color(0.04f, 0.16f, 0.21f, 0.96f));
        DrawRect(texture, 24, 72, 104, 136, new Color(0.83f, 0.96f, 1f, 0.95f), 4);
        DrawCircle(texture, 64, 104, 8, new Color(0.88f, 0.98f, 1f, 1f), true);
        FillRect(texture, 61, 108, 67, 126, new Color(0.88f, 0.98f, 1f, 1f));
        SaveSprite(texture, "icon_lock", Vector4.zero);
    }

    private static void CreateCalendarIcon()
    {
        var texture = NewTexture(48, 48);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawRect(texture, 9, 12, 39, 39, TextColor, 3);
        DrawLine(texture, 10, 20, 38, 20, TextColor, 2);
        DrawLine(texture, 16, 7, 16, 15, TextColor, 3);
        DrawLine(texture, 32, 7, 32, 15, TextColor, 3);
        SaveSprite(texture, "icon_calendar", Vector4.zero);
    }

    private static void CreateClockIcon()
    {
        var texture = NewTexture(48, 48);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawCircle(texture, 24, 24, 16, TextColor, false);
        DrawLine(texture, 24, 24, 24, 13, TextColor, 3);
        DrawLine(texture, 24, 24, 33, 28, TextColor, 3);
        SaveSprite(texture, "icon_clock", Vector4.zero);
    }

    private static void CreateVignette()
    {
        var texture = NewTexture(64, 64);
        var center = new Vector2(31.5f, 31.5f);
        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
        {
            var d = Vector2.Distance(new Vector2(x, y), center) / 42f;
            texture.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(d * d)));
        }
        SaveSprite(texture, "vignette", new Vector4(22, 22, 22, 22));
    }

    private static void CreateCorner()
    {
        var texture = NewTexture(192, 192);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        for (var i = 0; i < 5; i++)
        {
            DrawLine(texture, 10 + i, 180 - i, 154, 180 - i, new Color(0.78f, 0.92f, 1f, 0.38f), 1);
            DrawLine(texture, 10 + i, 180 - i, 10 + i, 36, new Color(0.78f, 0.92f, 1f, 0.38f), 1);
        }
        AddScratches(texture, 192, 192, 42);
        SaveSprite(texture, "corner", Vector4.zero);
    }

    private static void CreateDivider()
    {
        var texture = NewTexture(640, 28);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawLine(texture, 0, 14, 288, 14, new Color(0.82f, 0.94f, 1f, 0.62f), 2);
        DrawLine(texture, 352, 14, 639, 14, new Color(0.82f, 0.94f, 1f, 0.62f), 2);
        DrawDiamond(texture, 320, 14, 13, new Color(0.70f, 0.92f, 1f, 0.82f));
        DrawDiamond(texture, 294, 14, 6, new Color(0.70f, 0.92f, 1f, 0.56f));
        DrawDiamond(texture, 346, 14, 6, new Color(0.70f, 0.92f, 1f, 0.56f));
        SaveSprite(texture, "divider", Vector4.zero);
    }

    private static void CreateDiamond()
    {
        var texture = NewTexture(64, 64);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawDiamond(texture, 32, 32, 24, AccentColor);
        SaveSprite(texture, "diamond", Vector4.zero);
    }

    private static void CreateKeycap()
    {
        var texture = NewTexture(64, 64);
        Fill(texture, new Color(0.006f, 0.026f, 0.038f, 0.78f));
        DrawRect(texture, 5, 5, 58, 58, new Color(0.76f, 0.92f, 1f, 0.58f), 2);
        SaveSprite(texture, "keycap", new Vector4(12, 12, 12, 12));
    }

    private static void CreateSmokeLine()
    {
        var texture = NewTexture(512, 36);
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        for (var i = 0; i < 7; i++)
            DrawLine(texture, 10, 18 + Mathf.RoundToInt(Mathf.Sin(i) * 4), 502, 18 + Mathf.RoundToInt(Mathf.Sin(i * 1.7f) * 4), new Color(0.36f, 0.86f, 1f, 0.10f + i * 0.035f), 2 + i);
        SaveSprite(texture, "smoke_line", Vector4.zero);
    }

    private static Texture2D NewTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private static void SaveSprite(Texture2D texture, string name, Vector4 border)
    {
        texture.Apply();
        var path = $"{SpriteFolder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    private static void Fill(Texture2D texture, Color color)
    {
        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
            texture.SetPixel(x, y, color);
    }

    private static void FillVerticalGradient(Texture2D texture, Color bottom, Color top)
    {
        for (var y = 0; y < texture.height; y++)
        {
            var color = Color.Lerp(bottom, top, y / (float)(texture.height - 1));
            for (var x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, color);
        }
    }

    private static void DrawVilla(Texture2D texture, int x0, int y0, int x1, int y1)
    {
        var wall = new Color(0.10f, 0.16f, 0.20f, 1f);
        var trim = new Color(0.24f, 0.30f, 0.34f, 1f);
        FillRect(texture, x0, y0 + 70, x1, y1 - 65, wall);
        FillRect(texture, x0 + 86, y0, x1 - 86, y0 + 80, new Color(0.08f, 0.12f, 0.16f, 1f));
        DrawLine(texture, x0 - 20, y0 + 74, x1 + 20, y0 + 74, trim, 7);
        DrawLine(texture, x0 + 35, y0, x1 - 35, y0, trim, 9);

        for (var i = 0; i < 9; i++)
        {
            var x = x0 + 64 + i * 78;
            FillRect(texture, x, y0 + 150, x + 38, y0 + 248, new Color(0.01f, 0.025f, 0.04f, 1));
            DrawRect(texture, x, y0 + 150, x + 38, y0 + 248, new Color(0.18f, 0.24f, 0.28f, 1), 2);
            FillRect(texture, x, y0 + 290, x + 38, y0 + 384, new Color(0.01f, 0.025f, 0.04f, 1));
            DrawRect(texture, x, y0 + 290, x + 38, y0 + 384, new Color(0.18f, 0.24f, 0.28f, 1), 2);
        }

        for (var i = 0; i < 6; i++)
        {
            var x = x0 + 116 + i * 110;
            FillRect(texture, x, y0 + 74, x + 22, y1 - 32, new Color(0.28f, 0.31f, 0.32f, 1));
            DrawLine(texture, x + 11, y0 + 74, x + 11, y1 - 32, new Color(0.42f, 0.46f, 0.48f, 0.45f), 2);
        }

        FillRect(texture, x0 + 360, y0 + 270, x0 + 480, y1 - 30, new Color(0.01f, 0.018f, 0.025f, 1));
        DrawRect(texture, x0 + 360, y0 + 270, x0 + 480, y1 - 30, new Color(0.20f, 0.23f, 0.26f, 1), 3);
    }

    private static void DrawTrees(Texture2D texture)
    {
        FillRect(texture, 82, 0, 150, 900, new Color(0.005f, 0.01f, 0.015f, 1));
        FillRect(texture, 1420, 0, 1492, 900, new Color(0.005f, 0.01f, 0.015f, 1));
        for (var i = 0; i < 20; i++)
        {
            DrawLine(texture, 120, 740 - i * 18, 430 + i * 24, 815 - i * 13, new Color(0.005f, 0.01f, 0.015f, 1), 4);
            DrawLine(texture, 1458, 740 - i * 18, 1030 - i * 21, 815 - i * 13, new Color(0.005f, 0.01f, 0.015f, 1), 4);
        }
    }

    private static void DrawMoon(Texture2D texture, int cx, int cy, int radius)
    {
        var y = texture.height - cy;
        DrawCircle(texture, cx, y, radius + 26, new Color(0.48f, 0.68f, 0.84f, 0.12f), true);
        DrawCircle(texture, cx, y, radius, new Color(0.70f, 0.82f, 0.92f, 0.86f), true);
        DrawCircle(texture, cx + 18, y + 10, radius, new Color(0.56f, 0.70f, 0.82f, 0.22f), true);
    }

    private static void DrawFog(Texture2D texture, int startX, int endX, int startY, int endY)
    {
        for (var i = 0; i < 26; i++)
        {
            var y = startY + i * ((endY - startY) / 26);
            DrawLine(texture, startX, y, endX, y + Mathf.RoundToInt(Mathf.Sin(i) * 10), new Color(0.35f, 0.62f, 0.78f, 0.048f), 14);
        }
    }

    private static void DrawWetGround(Texture2D texture)
    {
        FillRect(texture, 0, 0, texture.width - 1, 190, new Color(0.02f, 0.04f, 0.055f, 1f));
        for (var i = 0; i < 52; i++)
        {
            var y = 35 + i * 3;
            DrawLine(texture, 130, y, 1460, y + Mathf.RoundToInt(Mathf.Sin(i * 0.7f) * 9), new Color(0.18f, 0.34f, 0.46f, 0.12f), 2);
        }
    }

    private static void FillRect(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
    {
        for (var y = Mathf.Max(0, y0); y <= Mathf.Min(texture.height - 1, y1); y++)
        for (var x = Mathf.Max(0, x0); x <= Mathf.Min(texture.width - 1, x1); x++)
            texture.SetPixel(x, y, color);
    }

    private static void DrawRect(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        for (var i = 0; i < thickness; i++)
        {
            DrawLine(texture, x0 + i, y0 + i, x1 - i, y0 + i, color, 1);
            DrawLine(texture, x0 + i, y1 - i, x1 - i, y1 - i, color, 1);
            DrawLine(texture, x0 + i, y0 + i, x0 + i, y1 - i, color, 1);
            DrawLine(texture, x1 - i, y0 + i, x1 - i, y1 - i, color, 1);
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        var dx = Mathf.Abs(x1 - x0);
        var dy = Mathf.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            Plot(texture, x0, y0, color, thickness);
            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color color, bool filled)
    {
        for (var y = -radius; y <= radius; y++)
        for (var x = -radius; x <= radius; x++)
        {
            var d = x * x + y * y;
            if ((filled && d <= radius * radius) || (!filled && Mathf.Abs(d - radius * radius) <= radius * 3))
                SetPixel(texture, cx + x, cy + y, color);
        }
    }

    private static void DrawDiamond(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        for (var x = -radius; x <= radius; x++)
        {
            if (Mathf.Abs(x) + Mathf.Abs(y) <= radius)
                SetPixel(texture, cx + x, cy + y, color);
        }
    }

    private static void DrawChainLink(Texture2D texture, int cx, int cy, int width, int height, bool leanRight)
    {
        var angle = (leanRight ? 24f : -24f) * Mathf.Deg2Rad;
        var sin = Mathf.Sin(angle);
        var cos = Mathf.Cos(angle);
        var outer = new Color(0.76f, 0.92f, 0.98f, 0.72f);
        var inner = new Color(0.18f, 0.42f, 0.50f, 0.34f);
        var glow = new Color(0.36f, 0.86f, 1f, 0.16f);

        for (var y = -height; y <= height; y++)
        for (var x = -width; x <= width; x++)
        {
            var rx = x * cos - y * sin;
            var ry = x * sin + y * cos;
            var d = (rx * rx) / (width * width * 0.25f) + (ry * ry) / (height * height * 0.25f);

            if (d > 0.78f && d < 1.25f)
                SetPixel(texture, cx + x, cy + y, outer);
            else if (d > 0.55f && d <= 0.78f)
                SetPixel(texture, cx + x, cy + y, inner);
            else if (d > 1.25f && d < 1.65f)
                SetPixel(texture, cx + x, cy + y, glow);
        }
    }

    private static void Plot(Texture2D texture, int x, int y, Color color, int thickness)
    {
        var half = Mathf.Max(0, thickness / 2);
        for (var yy = y - half; yy <= y + half; yy++)
        for (var xx = x - half; xx <= x + half; xx++)
            SetPixel(texture, xx, yy, color);
    }

    private static void SetPixel(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            texture.SetPixel(x, y, color);
    }

    private static void AddScratches(Texture2D texture, int width, int height, int count)
    {
        var random = new System.Random(width * 37 + height * 11 + count);
        for (var i = 0; i < count; i++)
        {
            var x = random.Next(0, width);
            var y = random.Next(0, height);
            var length = random.Next(8, 34);
            DrawLine(texture, x, y, Mathf.Min(width - 1, x + length), Mathf.Clamp(y + random.Next(-4, 5), 0, height - 1), new Color(1, 1, 1, 0.14f), 1);
        }
    }
}
