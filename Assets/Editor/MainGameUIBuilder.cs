using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainGameUIBuilder
{
    private const string SpriteFolder = "Assets/MainGame/UI/Sprites";
    private const string FontPath = "Assets/FpsHorrorKit/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly Color PanelColor = new Color(0.03f, 0.08f, 0.11f, 0.62f);
    private static readonly Color DeepPanelColor = new Color(0.01f, 0.04f, 0.07f, 0.78f);
    private static readonly Color LineColor = new Color(0.68f, 0.86f, 0.96f, 0.72f);
    private static readonly Color HotLineColor = new Color(0.58f, 0.89f, 1f, 0.95f);
    private static readonly Color TextColor = new Color(0.86f, 0.92f, 0.96f, 1f);
    private static readonly Color MutedTextColor = new Color(0.66f, 0.75f, 0.82f, 1f);
    private static readonly Color AccentTextColor = new Color(0.59f, 0.86f, 1f, 1f);
    private const float SettingsRowStartY = -72f;
    private const float SettingsRowStepY = 39f;

    private static TMP_FontAsset font;
    private static Dictionary<string, Sprite> sprites;

    [MenuItem("Tools/MainGame/Rebuild Vietnamese UI")]
    public static void RebuildVietnameseUI()
    {
        EnsureSprites();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.layer = LayerMask.NameToLayer("UI");
        }

        SetupCanvas(canvas);

        var gameUI = EnsureChild(canvas.transform, "GameUI");
        var settingUI = EnsureChild(canvas.transform, "SettingUI");

        RebuildGameUI(gameUI.transform);
        RebuildSettingUI(settingUI.transform);
        ConnectGameController(gameUI, settingUI);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Vietnamese GameUI and SettingUI rebuilt.");
    }

    private static void SetupCanvas(GameObject canvas)
    {
        canvas.layer = LayerMask.NameToLayer("UI");

        var rect = canvas.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvasComponent = canvas.GetComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComponent.sortingOrder = 0;

        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void RebuildGameUI(Transform root)
    {
        PrepareRoot(root, true);

        AddVignette(root, "VignetteOverlay", new Color(0.01f, 0.04f, 0.08f, 0.18f));
        AddCrosshair(root);
        AddObjectivePanel(root);
        AddFlashlightPanel(root);
        AddInteractPrompt(root);
        AddNarrationPanel(root);
    }

    private static void RebuildSettingUI(Transform root)
    {
        PrepareRoot(root, false);

        AddVignette(root, "DimmingOverlay", new Color(0.01f, 0.04f, 0.08f, 0.78f));
        AddSettingsTitle(root);

        var body = AddImage(root, "SettingsBody", sprites["panel"], new Color(0.01f, 0.035f, 0.055f, 0.74f), Image.Type.Sliced);
        SetRect(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1160, 640), new Vector2(0, -10));

        AddTabList(body.transform);
        var controller = root.gameObject.GetComponent<SettingsUIController>();
        if (controller == null)
            controller = root.gameObject.AddComponent<SettingsUIController>();

        AddSettingsContent(body.transform, controller);
        AddSettingsButtons(root, controller);
        AddShortcutFooter(root);
    }

    private static void PrepareRoot(Transform root, bool active)
    {
        root.gameObject.layer = LayerMask.NameToLayer("UI");
        root.gameObject.SetActive(active);

        var rect = root.GetComponent<RectTransform>();
        if (rect == null)
            rect = root.gameObject.AddComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        for (var i = root.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
    }

    private static void AddChapterHeader(Transform root)
    {
        var title = AddText(root, "ChapterTitleText", "Chương 1: Biệt thự trong sương", 30, AccentTextColor, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(620, 48), new Vector2(0, -50));
        title.fontStyle = FontStyles.SmallCaps;

        var divider = AddImage(root, "TopDivider", sprites["divider"], LineColor, Image.Type.Simple);
        SetRect(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(470, 18), new Vector2(0, -91));
    }

    private static void AddObjectivePanel(Transform root)
    {
        var panel = AddImage(root, "ObjectivePanel", sprites["panel"], DeepPanelColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(380, 162), new Vector2(16, -18));

        var title = AddText(panel.transform, "ObjectiveTitleText", "Mục tiêu:", 28, AccentTextColor, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(260, 36), new Vector2(42, -34));

        var divider = AddImage(panel.transform, "ObjectiveDivider", sprites["line"], LineColor, Image.Type.Sliced);
        SetRect(divider.rectTransform, new Vector2(0f, 1f), new Vector2(250, 3), new Vector2(42, -78));

        var text = AddText(panel.transform, "ObjectiveText", "Tìm lối vào biệt thự", 27, MutedTextColor, TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(300, 38), new Vector2(42, -104));
    }

    private static void AddStatusPanels(Transform root)
    {
        var stack = AddObject(root, "StatusStack", typeof(RectTransform));
        var stackRect = stack.GetComponent<RectTransform>();
        SetRect(stackRect, new Vector2(0f, 0f), new Vector2(300, 230), new Vector2(38, 90));

        AddFlashlightPanel(stack.transform);
        AddCameraPanel(stack.transform);
    }

    private static void AddFlashlightPanel(Transform root)
    {
        var panel = AddImage(root, "FlashlightPanel", sprites["panel"], DeepPanelColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(332, 158), new Vector2(20, 20));

        var title = AddText(panel.transform, "FlashlightTitleText", "PIN ĐÈN PIN", 23, TextColor, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(210, 32), new Vector2(30, -28));

        var divider = AddImage(panel.transform, "FlashlightDivider", sprites["line"], LineColor, Image.Type.Sliced);
        SetRect(divider.rectTransform, new Vector2(0f, 1f), new Vector2(206, 3), new Vector2(30, -68));

        var batteryFrame = AddImage(panel.transform, "BatteryFrame", sprites["bar"], new Color(0.02f, 0.045f, 0.06f, 0.86f), Image.Type.Sliced);
        SetRect(batteryFrame.rectTransform, new Vector2(0f, 0f), new Vector2(168, 52), new Vector2(32, 26));

        var cap = AddImage(panel.transform, "BatteryCap", null, LineColor, Image.Type.Simple);
        SetRect(cap.rectTransform, new Vector2(0f, 0f), new Vector2(9, 24), new Vector2(200, 40));

        for (var i = 0; i < 6; i++)
        {
            var cellColor = i < 5 ? new Color(0.48f, 0.85f, 1f, 0.90f) : new Color(0.04f, 0.08f, 0.10f, 0.66f);
            var cell = AddImage(batteryFrame.transform, $"BatteryCell{i + 1}", sprites["bar_fill"], cellColor, Image.Type.Sliced);
            SetRect(cell.rectTransform, new Vector2(0f, 0.5f), new Vector2(22, 38), new Vector2(10 + i * 25, 0));
        }

        var percent = AddText(panel.transform, "BatteryPercentText", "78%", 28, TextColor, TextAlignmentOptions.Left);
        SetRect(percent.rectTransform, new Vector2(0f, 0f), new Vector2(90, 40), new Vector2(238, 34));
    }

    private static void AddCameraPanel(Transform root)
    {
        var panel = AddImage(root, "CameraPanel", sprites["panel"], DeepPanelColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(282, 104), new Vector2(0, -130));

        var icon = AddImage(panel.transform, "CameraIcon", sprites["icon_camera"], Color.white, Image.Type.Simple);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(74, 74), new Vector2(24, 0));

        var title = AddText(panel.transform, "CameraTitleText", "MÁY ẢNH", 18, AccentTextColor, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(130, 24), new Vector2(126, -28));

        var film = AddText(panel.transform, "FilmCountText", "Phim:  12", 24, TextColor, TextAlignmentOptions.Left);
        SetRect(film.rectTransform, new Vector2(0f, 0f), new Vector2(150, 30), new Vector2(126, 28));
    }

    private static void AddInteractPrompt(Transform root)
    {
        var panel = AddImage(root, "InteractPrompt", sprites["button"], new Color(0.03f, 0.09f, 0.1f, 0.44f), Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(322, 72), new Vector2(0, -206));

        var text = AddText(panel.transform, "InteractText", "Nhấn     để tương tác", 24, MutedTextColor, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);

        var keyBg = AddImage(panel.transform, "KeycapBackground", sprites["keycap"], new Color(0.02f, 0.04f, 0.06f, 0.78f), Image.Type.Sliced);
        SetRect(keyBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(36, 36), new Vector2(-34, 0));

        var key = AddText(keyBg.transform, "KeycapText", "E", 21, TextColor, TextAlignmentOptions.Center);
        SetStretch(key.rectTransform, Vector2.zero, Vector2.zero);

        panel.gameObject.SetActive(false);
    }

    private static void AddNarrationPanel(Transform root)
    {
        var panel = AddImage(root, "NarrationPanel", sprites["panel"], DeepPanelColor, Image.Type.Sliced);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(700, 94), new Vector2(0, 62));

        var text = AddText(panel.transform, "NarrationText", "", 26, AccentTextColor, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.Normal;
        SetStretch(text.rectTransform, new Vector2(34, 14), new Vector2(-34, -14));
        panel.gameObject.SetActive(false);
    }

    private static void AddInventoryBar(Transform root)
    {
        var bar = AddImage(root, "InventoryBar", sprites["panel"], new Color(0.01f, 0.05f, 0.08f, 0.72f), Image.Type.Sliced);
        SetRect(bar.rectTransform, new Vector2(1f, 0f), new Vector2(528, 154), new Vector2(-38, 46));

        var names = new[] { "Đèn pin", "Máy ảnh", "Chìa khóa", "Ghi chú" };
        var icons = new[] { "icon_flashlight", "icon_camera", "icon_key", "icon_note" };

        for (var i = 0; i < 4; i++)
        {
            var slot = AddImage(bar.transform, $"InventorySlot{i + 1}", sprites[i == 0 ? "slot_selected" : "slot"], i == 0 ? new Color(0.42f, 0.76f, 0.96f, 0.34f) : new Color(0.03f, 0.07f, 0.1f, 0.65f), Image.Type.Sliced);
            SetRect(slot.rectTransform, new Vector2(0f, 1f), new Vector2(118, 118), new Vector2(18 + i * 122, -18));

            var number = AddText(slot.transform, "SlotNumberText", (i + 1).ToString(), 18, TextColor, TextAlignmentOptions.Left);
            SetRect(number.rectTransform, new Vector2(0f, 1f), new Vector2(32, 24), new Vector2(12, -13));

            var icon = AddImage(slot.transform, "SlotIcon", sprites[icons[i]], Color.white, Image.Type.Simple);
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(64, 64), new Vector2(0, 9));

            var label = AddText(slot.transform, "SlotLabelText", names[i], 16, TextColor, TextAlignmentOptions.Center);
            SetRect(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(112, 26), new Vector2(0, 7));
        }
    }

    private static void AddCrosshair(Transform root)
    {
        var crosshair = AddImage(root, "Crosshair", sprites["crosshair"], new Color(0.88f, 0.94f, 1f, 0.82f), Image.Type.Simple);
        SetRect(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(34, 34), Vector2.zero);
    }

    private static void AddPulseLine(Transform root)
    {
        var pulse = AddImage(root, "PulseLine", sprites["pulse"], new Color(0.49f, 0.84f, 1f, 0.52f), Image.Type.Simple);
        SetRect(pulse.rectTransform, new Vector2(0.5f, 0f), new Vector2(280, 34), new Vector2(0, 30));
    }

    private static void AddSettingsTitle(Transform root)
    {
        var title = AddText(root, "SettingsTitleText", "CÀI ĐẶT", 52, TextColor, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(460, 68), new Vector2(0, -58));
        title.characterSpacing = 18;
        title.fontStyle = FontStyles.SmallCaps;

        var divider = AddImage(root, "SettingsHeaderDivider", sprites["divider"], LineColor, Image.Type.Simple);
        SetRect(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(470, 18), new Vector2(0, -116));
    }

    private static void AddTabList(Transform body)
    {
        var tabList = AddObject(body, "TabList", typeof(RectTransform));
        SetRect(tabList.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(250, 390), new Vector2(44, 20));

        AddTab(tabList.transform, "GeneralTab", "Chung", 0, false);
        AddTab(tabList.transform, "VisualTab", "Hình ảnh", 1, true);
        AddTab(tabList.transform, "AudioTab", "Âm thanh", 2, false);
    }

    private static void AddTab(Transform root, string objectName, string label, int index, bool selected)
    {
        if (objectName == "ControlsTab" || objectName == "GameplayTab")
            return;

        var tab = AddImage(root, objectName, sprites[selected ? "tab_selected" : "tab"], selected ? new Color(0.09f, 0.27f, 0.36f, 0.70f) : new Color(0.01f, 0.035f, 0.06f, 0.58f), Image.Type.Sliced);
        SetRect(tab.rectTransform, new Vector2(0f, 1f), new Vector2(238, 58), new Vector2(0, -index * 68));

        var text = AddText(tab.transform, "LabelText", label, 22, selected ? TextColor : new Color(0.70f, 0.76f, 0.80f, 0.95f), TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(180, 34), new Vector2(48, 0));
        text.characterSpacing = 2;

        if (selected)
        {
            var marker = AddImage(tab.transform, "SelectedDiamond", sprites["diamond"], HotLineColor, Image.Type.Simple);
            SetRect(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(22, 36), new Vector2(-3, 0));
        }
    }

    private static void AddSettingsContent(Transform body, SettingsUIController controller)
    {
        var content = AddObject(body, "ContentPanel", typeof(RectTransform));
        SetRect(content.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(740, 540), new Vector2(360, 20));

        var visualTitle = AddText(content.transform, "VisualSectionTitleText", "HÌNH ẢNH", 22, AccentTextColor, TextAlignmentOptions.Left);
        SetRect(visualTitle.rectTransform, new Vector2(0f, 1f), new Vector2(250, 32), new Vector2(0, -6));
        AddSeparator(content.transform, "VisualSeparator", -42);

        controller.resolutionDropdown = AddDropdown(content.transform, "ResolutionDropdown", "Độ phân giải", 0, new[] { "1920x1080", "1600x900", "1280x720" });
        controller.displayModeDropdown = AddDropdown(content.transform, "DisplayModeDropdown", "Chế độ hiển thị", 1, new[] { "Toàn màn hình", "Cửa sổ" });
        controller.brightnessSlider = AddSliderRow(content.transform, "BrightnessSlider", "Độ sáng", 2, 60, out controller.brightnessValueText);

        var audioTitle = AddText(content.transform, "AudioSectionTitleText", "ÂM THANH", 22, AccentTextColor, TextAlignmentOptions.Left);
        SetRect(audioTitle.rectTransform, new Vector2(0f, 1f), new Vector2(250, 32), new Vector2(0, -242));
        AddSeparator(content.transform, "AudioSeparator", -278);

        controller.masterVolumeSlider = AddSliderRow(content.transform, "MasterVolumeSlider", "Âm lượng tổng", 5, 80, out controller.masterVolumeValueText);
        controller.musicVolumeSlider = AddSliderRow(content.transform, "MusicVolumeSlider", "Âm nhạc", 6, 40, out controller.musicVolumeValueText);
        controller.sfxVolumeSlider = AddSliderRow(content.transform, "SfxVolumeSlider", "Hiệu ứng", 7, 70, out controller.sfxVolumeValueText);
    }

    private static TMP_Dropdown AddDropdown(Transform root, string objectName, string label, int row, string[] options, int value = 0)
    {
        AddRowLabel(root, $"{objectName}LabelText", label, row);

        var go = AddImage(root, objectName, sprites["button"], new Color(0.012f, 0.04f, 0.06f, 0.62f), Image.Type.Sliced);
        go.raycastTarget = true;
        SetRect(go.rectTransform, new Vector2(1f, 1f), new Vector2(300, 34), new Vector2(-6, SettingsRowStartY - row * SettingsRowStepY));

        var dropdown = go.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = go;
        dropdown.options.Clear();
        foreach (var option in options)
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        dropdown.value = Mathf.Clamp(value, 0, options.Length - 1);

        var labelText = AddText(go.transform, "CaptionText", options[dropdown.value], 18, TextColor, TextAlignmentOptions.Left);
        SetStretch(labelText.rectTransform, new Vector2(36, 0), new Vector2(-42, 0));
        dropdown.captionText = labelText;

        var arrow = AddText(go.transform, "ArrowText", "v", 24, AccentTextColor, TextAlignmentOptions.Center);
        SetRect(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(30, 30), new Vector2(-20, 0));

        BuildDropdownTemplate(go.transform, dropdown);
        return dropdown;
    }

    private static void BuildDropdownTemplate(Transform root, TMP_Dropdown dropdown)
    {
        var template = AddImage(root, "Template", sprites["panel"], new Color(0.01f, 0.04f, 0.06f, 0.96f), Image.Type.Sliced);
        template.gameObject.SetActive(false);
        SetRect(template.rectTransform, new Vector2(0.5f, 0f), new Vector2(305, 164), new Vector2(0, -84));

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

        var item = AddObject(content.transform, "Item", typeof(RectTransform), typeof(Toggle));
        var itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 0.5f);
        itemRect.anchorMax = new Vector2(1, 0.5f);
        itemRect.sizeDelta = new Vector2(0, 34);
        itemRect.anchoredPosition = Vector2.zero;

        var itemBg = item.AddComponent<Image>();
        itemBg.sprite = sprites["button"];
        itemBg.type = Image.Type.Sliced;
        itemBg.color = new Color(0.02f, 0.08f, 0.1f, 0.52f);

        var checkmark = AddImage(item.transform, "Item Checkmark", sprites["diamond"], HotLineColor, Image.Type.Simple);
        SetRect(checkmark.rectTransform, new Vector2(0f, 0.5f), new Vector2(16, 16), new Vector2(14, 0));

        var itemLabel = AddText(item.transform, "Item Label", "Option", 18, TextColor, TextAlignmentOptions.Left);
        SetStretch(itemLabel.rectTransform, new Vector2(38, 0), new Vector2(-14, 0));

        var toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBg;
        toggle.graphic = checkmark;

        scrollRect.viewport = viewport.rectTransform;
        scrollRect.content = contentRect;

        dropdown.template = template.rectTransform;
        dropdown.itemText = itemLabel;
        dropdown.itemImage = null;
    }

    private static Slider AddSliderRow(Transform root, string objectName, string label, int row, float value, out TMP_Text valueText)
    {
        AddRowLabel(root, $"{objectName}LabelText", label, row);

        var sliderGo = AddImage(root, objectName, sprites["smoke_line"], new Color(0.23f, 0.67f, 0.86f, 0.26f), Image.Type.Simple);
        sliderGo.raycastTarget = true;
        SetRect(sliderGo.rectTransform, new Vector2(1f, 1f), new Vector2(370, 28), new Vector2(-58, SettingsRowStartY - 9 - row * SettingsRowStepY));

        var slider = sliderGo.gameObject.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.wholeNumbers = true;
        slider.value = value;

        var mist = AddImage(sliderGo.transform, "MistGlow", sprites["smoke_line"], new Color(0.18f, 0.78f, 1f, 0.20f), Image.Type.Simple);
        SetStretch(mist.rectTransform, new Vector2(-10, -8), new Vector2(10, 8));

        var fill = AddImage(sliderGo.transform, "Fill", sprites["smoke_line"], new Color(0.35f, 0.90f, 1f, 0.90f), Image.Type.Filled);
        ConfigureHorizontalFill(fill, Mathf.InverseLerp(0f, 100f, value));
        SetStretch(fill.rectTransform, Vector2.zero, Vector2.zero);

        var handleArea = AddObject(sliderGo.transform, "Handle Slide Area", typeof(RectTransform));
        SetStretch(handleArea.GetComponent<RectTransform>(), new Vector2(0, -12), new Vector2(0, 12));

        var handleGlow = AddImage(handleArea.transform, "Handle", sprites["diamond"], new Color(0.36f, 0.88f, 1f, 0.30f), Image.Type.Simple);
        handleGlow.raycastTarget = true;
        SetRect(handleGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(38, 50), Vector2.zero);

        var handle = AddImage(handleGlow.transform, "HandleCore", sprites["diamond"], new Color(0.90f, 0.98f, 1f, 0.96f), Image.Type.Simple);
        handle.raycastTarget = true;
        SetRect(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(24, 38), Vector2.zero);

        slider.fillRect = null;
        slider.handleRect = handleGlow.rectTransform;
        slider.targetGraphic = handle;

        var filledGraphic = sliderGo.gameObject.AddComponent<FilledSliderGraphic>();
        filledGraphic.slider = slider;
        filledGraphic.fillImage = fill;

        valueText = AddText(root, $"{objectName}ValueText", Mathf.RoundToInt(value).ToString(), 18, TextColor, TextAlignmentOptions.Right);
        SetRect(valueText.rectTransform, new Vector2(1f, 1f), new Vector2(45, 28), new Vector2(0, SettingsRowStartY - 5 - row * SettingsRowStepY));

        return slider;
    }

    private static void AddRowLabel(Transform root, string objectName, string label, int row)
    {
        var text = AddText(root, objectName, label, 18, TextColor, TextAlignmentOptions.Left);
        SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(330, 28), new Vector2(0, SettingsRowStartY - 3 - row * SettingsRowStepY));
    }

    private static void AddSettingsButtons(Transform root, SettingsUIController controller)
    {
        controller.applyButton = AddButton(root, "ApplyButton", "ÁP DỤNG", new Vector2(0.5f, 0f), new Vector2(270, 58), new Vector2(-390, 78), controller.Apply);
        controller.resetButton = AddButton(root, "ResetButton", "KHÔI PHỤC MẶC ĐỊNH", new Vector2(0.5f, 0f), new Vector2(330, 58), new Vector2(0, 78), controller.ResetDefaults);
        controller.backButton = AddButton(root, "BackButton", "QUAY LẠI", new Vector2(0.5f, 0f), new Vector2(270, 58), new Vector2(390, 78), controller.Back);
    }

    private static Button AddButton(Transform root, string objectName, string label, Vector2 anchor, Vector2 size, Vector2 pos, UnityAction action)
    {
        var image = AddImage(root, objectName, sprites["button"], new Color(0.02f, 0.06f, 0.08f, 0.82f), Image.Type.Sliced);
        image.raycastTarget = true;
        SetRect(image.rectTransform, anchor, size, pos);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.72f, 0.95f, 1f, 1f),
            pressedColor = new Color(0.48f, 0.72f, 0.82f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };

        var text = AddText(image.transform, "LabelText", label, 22, TextColor, TextAlignmentOptions.Center);
        text.characterSpacing = 4;
        SetStretch(text.rectTransform, new Vector2(12, 0), new Vector2(-12, 0));
        return button;
    }

    private static void AddShortcutFooter(Transform root)
    {
        var line = AddImage(root, "FooterDivider", sprites["divider"], new Color(0.45f, 0.7f, 0.82f, 0.46f), Image.Type.Simple);
        SetRect(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(760, 14), new Vector2(0, 45));

        var text = AddText(root, "ShortcutText", "ESC  - Quay lại     |     R  - Khôi phục mặc định     |     Enter  - Áp dụng", 17, MutedTextColor, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(860, 30), new Vector2(0, 20));
    }

    private static void AddSeparator(Transform root, string objectName, float y)
    {
        var separator = AddImage(root, objectName, sprites["line"], new Color(0.54f, 0.75f, 0.86f, 0.38f), Image.Type.Sliced);
        SetRect(separator.rectTransform, new Vector2(0.5f, 1f), new Vector2(730, 4), new Vector2(0, y));
    }

    private static void AddVignette(Transform root, string objectName, Color color)
    {
        var overlay = AddImage(root, objectName, sprites["vignette"], color, Image.Type.Sliced);
        SetStretch(overlay.rectTransform, Vector2.zero, Vector2.zero);
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child.gameObject;

        return AddObject(parent, name, typeof(RectTransform));
    }

    private static GameObject AddObject(Transform parent, string name, params Type[] components)
    {
        var go = new GameObject(name, components);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
    {
        var go = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text AddText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var go = AddObject(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.fontStyle = FontStyles.SmallCaps;
        tmp.characterSpacing = 1.5f;
        tmp.outlineColor = new Color(0.01f, 0.04f, 0.06f, 0.92f);
        tmp.outlineWidth = size >= 22 ? 0.14f : 0.10f;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.55f, 0.72f, size >= 22 ? 0.34f : 0.20f);
        shadow.effectDistance = new Vector2(1.2f, -1.4f);
        shadow.useGraphicAlpha = true;
        return tmp;
    }

    private static void ConfigureHorizontalFill(Image image, float amount)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = Mathf.Clamp01(amount);
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
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

    private static void ConnectGameController(GameObject gameUI, GameObject settingUI)
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
        if (controller != null)
        {
            controller.gameUI = gameUI;
            controller.pauseUI = settingUI;
            EditorUtility.SetDirty(controller);
        }

        WirePlayerInteract(gameUI);
    }

    private static void WirePlayerInteract(GameObject gameUI)
    {
        var playerInteract = UnityEngine.Object.FindFirstObjectByType<FpsHorrorKit.PlayerInteract>();
        if (playerInteract == null)
        {
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player != null)
                playerInteract = player.GetComponent<FpsHorrorKit.PlayerInteract>() ?? player.AddComponent<FpsHorrorKit.PlayerInteract>();
        }

        if (playerInteract == null)
            return;

        var prompt = FindChildTransform(gameUI.transform, "InteractPrompt");
        var promptText = FindChildTransform(gameUI.transform, "InteractText");

        var serialized = new SerializedObject(playerInteract);
        Set(serialized, "higlightObject", prompt != null ? prompt.gameObject : null);
        Set(serialized, "interactTextUI", promptText != null ? promptText.GetComponent<TextMeshProUGUI>() : null);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void EnsureSprites()
    {
        if (!AssetDatabase.IsValidFolder("Assets/MainGame/UI"))
            AssetDatabase.CreateFolder("Assets/MainGame", "UI");
        if (!AssetDatabase.IsValidFolder(SpriteFolder))
            AssetDatabase.CreateFolder("Assets/MainGame/UI", "Sprites");

        CreatePanelSprite("panel", 256, 128, new Vector4(24, 24, 24, 24), true);
        CreatePanelSprite("button", 256, 72, new Vector4(18, 18, 18, 18), true);
        CreatePanelSprite("slot", 128, 128, new Vector4(18, 18, 18, 18), true);
        CreatePanelSprite("slot_selected", 128, 128, new Vector4(18, 18, 18, 18), true, true);
        CreatePanelSprite("tab", 256, 72, new Vector4(18, 18, 18, 18), true);
        CreatePanelSprite("tab_selected", 256, 72, new Vector4(18, 18, 18, 18), true, true);
        CreatePanelSprite("keycap", 64, 64, new Vector4(14, 14, 14, 14), true);
        CreateBarSprite("bar", 128, 18, new Vector4(8, 8, 8, 8));
        CreateBarFillSprite("bar_fill", 128, 18, new Vector4(8, 8, 8, 8));
        CreateSmokeLineSprite("smoke_line", 256, 32);
        CreateDiamondSprite("diamond", 64, 64);
        CreateLineSprite("line", 128, 4, new Vector4(2, 2, 0, 0));
        CreateDividerSprite("divider", 512, 32);
        CreateCrosshairSprite("crosshair", 64, 64);
        CreatePulseSprite("pulse", 512, 64);
        CreateVignetteSprite("vignette", 128, 128, new Vector4(40, 40, 40, 40));
        CreateIconSprites();

        AssetDatabase.Refresh();
        sprites = new Dictionary<string, Sprite>();
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites[Path.GetFileNameWithoutExtension(path)] = sprite;
        }
    }

    private static void CreatePanelSprite(string name, int width, int height, Vector4 border, bool scratches, bool glow = false)
    {
        var texture = NewTexture(width, height);
        Fill(texture, new Color(0.02f, 0.06f, 0.08f, 0.56f));
        DrawRect(texture, 2, 2, width - 3, height - 3, glow ? HotLineColor : LineColor, 2);
        DrawRect(texture, 8, 8, width - 9, height - 9, new Color(0.72f, 0.9f, 1f, glow ? 0.55f : 0.28f), 1);

        if (glow)
        {
            DrawRect(texture, 0, 0, width - 1, height - 1, new Color(0.28f, 0.8f, 1f, 0.35f), 5);
        }

        if (scratches)
            AddScratches(texture, width, height, 24);

        SaveSprite(texture, name, border);
    }

    private static void CreateBarSprite(string name, int width, int height, Vector4 border)
    {
        var texture = NewTexture(width, height);
        Fill(texture, new Color(0.03f, 0.07f, 0.09f, 0.82f));
        DrawRect(texture, 0, 0, width - 1, height - 1, LineColor, 2);
        SaveSprite(texture, name, border);
    }

    private static void CreateBarFillSprite(string name, int width, int height, Vector4 border)
    {
        var texture = NewTexture(width, height);
        Fill(texture, new Color(0.46f, 0.86f, 1f, 0.82f));
        DrawLine(texture, 2, height / 2, width - 3, height / 2, Color.white, 2);
        SaveSprite(texture, name, border);
    }

    private static void CreateSmokeLineSprite(string name, int width, int height)
    {
        var texture = NewTexture(width, height);
        Fill(texture, Color.clear);

        var center = height / 2f;
        for (var x = 0; x < width; x++)
        {
            var wave = Mathf.Sin(x * 0.07f) * 2.2f + Mathf.Sin(x * 0.17f) * 0.9f;
            for (var y = 0; y < height; y++)
            {
                var distance = Mathf.Abs(y - center - wave);
                var core = Mathf.Clamp01(1f - distance / 2.2f);
                var haze = Mathf.Clamp01(1f - distance / 9f);
                var alpha = core * 0.92f + haze * 0.22f;
                if (alpha <= 0.01f)
                    continue;

                var flicker = 0.78f + 0.22f * Mathf.Sin(x * 0.11f + y * 0.23f);
                var color = Color.Lerp(new Color(0.12f, 0.55f, 0.70f, 0f), new Color(0.60f, 0.95f, 1f, 0f), core);
                color.a = alpha * flicker;
                texture.SetPixel(x, y, color);
            }
        }

        DrawLine(texture, 8, Mathf.RoundToInt(center), width - 9, Mathf.RoundToInt(center), new Color(0.72f, 0.98f, 1f, 0.72f), 1);
        AddScratches(texture, width, height, 18);
        SaveSprite(texture, name, Vector4.zero);
    }

    private static void CreateDiamondSprite(string name, int size, int unused)
    {
        var texture = NewTexture(size, size);
        var center = size / 2;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                if (d < center - 5)
                    texture.SetPixel(x, y, new Color(0.58f, 0.89f, 1f, 0.95f));
                else if (d < center)
                    texture.SetPixel(x, y, Color.white);
                else
                    texture.SetPixel(x, y, Color.clear);
            }
        }
        SaveSprite(texture, name, Vector4.zero);
    }

    private static void CreateLineSprite(string name, int width, int height, Vector4 border)
    {
        var texture = NewTexture(width, height);
        Fill(texture, Color.clear);
        DrawLine(texture, 0, height / 2, width - 1, height / 2, LineColor, 2);
        SaveSprite(texture, name, border);
    }

    private static void CreateDividerSprite(string name, int width, int height)
    {
        var texture = NewTexture(width, height);
        Fill(texture, Color.clear);
        DrawLine(texture, 20, height / 2, width / 2 - 34, height / 2, LineColor, 2);
        DrawLine(texture, width / 2 + 34, height / 2, width - 20, height / 2, LineColor, 2);
        DrawDiamondOutline(texture, width / 2, height / 2, 13, LineColor);
        DrawDiamondOutline(texture, width / 2 - 24, height / 2, 7, LineColor);
        DrawDiamondOutline(texture, width / 2 + 24, height / 2, 7, LineColor);
        SaveSprite(texture, name, Vector4.zero);
    }

    private static void CreateCrosshairSprite(string name, int size, int unused)
    {
        var texture = NewTexture(size, size);
        Fill(texture, Color.clear);
        var c = size / 2;
        DrawLine(texture, c, c - 20, c, c - 7, Color.white, 1);
        DrawLine(texture, c, c + 7, c, c + 20, Color.white, 1);
        DrawLine(texture, c - 20, c, c - 7, c, Color.white, 1);
        DrawLine(texture, c + 7, c, c + 20, c, Color.white, 1);
        DrawRect(texture, c - 2, c - 2, c + 2, c + 2, new Color(0.6f, 0.85f, 1f, 0.6f), 1);
        SaveSprite(texture, name, Vector4.zero);
    }

    private static void CreatePulseSprite(string name, int width, int height)
    {
        var texture = NewTexture(width, height);
        Fill(texture, Color.clear);
        var mid = height / 2;
        var points = new[]
        {
            new Vector2Int(0, mid), new Vector2Int(170, mid), new Vector2Int(185, mid + 10),
            new Vector2Int(198, mid - 18), new Vector2Int(212, mid + 24), new Vector2Int(226, mid - 2),
            new Vector2Int(252, mid), new Vector2Int(256, mid - 26), new Vector2Int(267, mid + 20),
            new Vector2Int(280, mid - 7), new Vector2Int(300, mid), new Vector2Int(width, mid)
        };

        for (var i = 0; i < points.Length - 1; i++)
            DrawLine(texture, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, LineColor, 2);

        SaveSprite(texture, name, Vector4.zero);
    }

    private static void CreateVignetteSprite(string name, int width, int height, Vector4 border)
    {
        var texture = NewTexture(width, height);
        var center = new Vector2(width / 2f, height / 2f);
        var maxDistance = center.magnitude;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                var alpha = Mathf.SmoothStep(0.04f, 0.9f, distance);
                texture.SetPixel(x, y, new Color(0f, 0.02f, 0.05f, alpha));
            }
        }
        SaveSprite(texture, name, border);
    }

    private static void CreateIconSprites()
    {
        CreateIcon("icon_flashlight", texture =>
        {
            DrawThickLine(texture, 34, 25, 88, 76, new Color(0.55f, 0.62f, 0.65f, 1), 14);
            DrawThickLine(texture, 43, 20, 97, 70, new Color(0.18f, 0.22f, 0.25f, 1), 6);
            DrawCircle(texture, 91, 77, 18, new Color(0.84f, 0.92f, 0.96f, 1), true);
            DrawCircle(texture, 91, 77, 12, new Color(0.22f, 0.3f, 0.34f, 1), true);
            DrawCircle(texture, 87, 81, 5, Color.white, true);
        });

        CreateIcon("icon_camera", texture =>
        {
            FillRect(texture, 24, 42, 104, 90, new Color(0.21f, 0.22f, 0.22f, 1));
            DrawRect(texture, 24, 42, 104, 90, LineColor, 2);
            FillRect(texture, 40, 32, 62, 43, new Color(0.12f, 0.13f, 0.14f, 1));
            FillRect(texture, 76, 34, 98, 43, new Color(0.28f, 0.17f, 0.14f, 1));
            DrawCircle(texture, 64, 66, 25, new Color(0.08f, 0.1f, 0.11f, 1), true);
            DrawCircle(texture, 64, 66, 18, new Color(0.26f, 0.36f, 0.42f, 1), true);
            DrawCircle(texture, 59, 72, 7, new Color(0.9f, 0.96f, 1f, 0.92f), true);
        });

        CreateIcon("icon_key", texture =>
        {
            DrawCircle(texture, 43, 77, 22, new Color(0.67f, 0.58f, 0.42f, 1), false);
            DrawCircle(texture, 43, 77, 10, new Color(0f, 0f, 0f, 0f), true);
            DrawThickLine(texture, 57, 64, 98, 24, new Color(0.72f, 0.62f, 0.43f, 1), 12);
            DrawThickLine(texture, 82, 37, 100, 54, new Color(0.72f, 0.62f, 0.43f, 1), 8);
            DrawThickLine(texture, 93, 28, 111, 45, new Color(0.72f, 0.62f, 0.43f, 1), 8);
        });

        CreateIcon("icon_note", texture =>
        {
            FillRect(texture, 34, 18, 94, 104, new Color(0.73f, 0.64f, 0.49f, 1));
            DrawRect(texture, 34, 18, 94, 104, new Color(0.25f, 0.18f, 0.12f, 1), 2);
            DrawLine(texture, 45, 88, 83, 83, new Color(0.24f, 0.18f, 0.15f, 1), 2);
            DrawLine(texture, 45, 74, 86, 70, new Color(0.24f, 0.18f, 0.15f, 1), 2);
            DrawLine(texture, 45, 60, 83, 56, new Color(0.24f, 0.18f, 0.15f, 1), 2);
            DrawLine(texture, 45, 46, 78, 43, new Color(0.24f, 0.18f, 0.15f, 1), 2);
        });
    }

    private static void CreateIcon(string name, Action<Texture2D> draw)
    {
        var texture = NewTexture(128, 128);
        Fill(texture, Color.clear);
        draw(texture);
        SaveSprite(texture, name, Vector4.zero);
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
        UnityEngine.Object.DestroyImmediate(texture);

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

    private static void DrawThickLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int radius)
    {
        var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        for (var i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0f : i / (float)steps;
            DrawCircle(texture, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), radius, color, true);
        }
    }

    private static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color color, bool filled)
    {
        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                var d = x * x + y * y;
                if ((filled && d <= radius * radius) || (!filled && Mathf.Abs(d - radius * radius) <= radius * 3))
                    SetPixel(texture, cx + x, cy + y, color);
            }
        }
    }

    private static void DrawDiamondOutline(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        DrawLine(texture, cx, cy + radius, cx + radius, cy, color, 2);
        DrawLine(texture, cx + radius, cy, cx, cy - radius, color, 2);
        DrawLine(texture, cx, cy - radius, cx - radius, cy, color, 2);
        DrawLine(texture, cx - radius, cy, cx, cy + radius, color, 2);
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
        var random = new System.Random(width * 31 + height * 17 + count);
        for (var i = 0; i < count; i++)
        {
            var x = random.Next(0, width);
            var y = random.Next(0, height);
            var length = random.Next(6, 28);
            var color = new Color(1f, 1f, 1f, (float)(0.08 + random.NextDouble() * 0.22));
            DrawLine(texture, x, y, Mathf.Min(width - 1, x + length), Mathf.Clamp(y + random.Next(-4, 5), 0, height - 1), color, 1);
        }
    }
}
