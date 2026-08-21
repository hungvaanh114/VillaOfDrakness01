using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FpsHorrorKit
{
    public sealed class PianoPuzzleUI : MonoBehaviour
    {
        public static PianoPuzzleUI Instance { get; private set; }
        public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite pianoBackgroundSprite;
        [SerializeField] private Sprite whiteKeySprite;
        [SerializeField] private Sprite dividerSprite;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private AudioClip noteClip;
        [SerializeField] private AudioClip noteClipC;
        [SerializeField] private AudioClip noteClipD;
        [SerializeField] private AudioClip noteClipE;
        [SerializeField] private AudioClip noteClipF;
        [SerializeField] private AudioClip noteClipG;
        [SerializeField] private AudioClip noteClipA;
        [SerializeField] private AudioClip noteClipB;
        [SerializeField] private AudioClip noteClipHighC;
        [SerializeField] private AudioClip noteClipCSharp;
        [SerializeField] private AudioClip noteClipDSharp;
        [SerializeField] private AudioClip noteClipFSharp;
        [SerializeField] private AudioClip noteClipGSharp;
        [SerializeField] private AudioClip noteClipASharp;

        private GameObject root;
        private AudioSource audioSource;
        private bool completed;
        private static readonly string[] WhiteNotes = { "C", "D", "E", "F", "G", "A", "B", "C2" };
        private static readonly string[] WhiteLabels = { "C", "D", "E", "F", "G", "A", "B", "C" };
        private static readonly string[] BlackNotes = { "C#", "D#", "F#", "G#", "A#" };
        private static readonly string[] KeyboardNotes = { "C", "D", "E", "F", "G", "A", "B", "C#", "D#", "F#", "G#", "A#" };
        private static readonly float[] KeyboardPitches = { 1f, 1.122f, 1.26f, 1.335f, 1.498f, 1.682f, 1.888f, 1.059f, 1.189f, 1.414f, 1.587f, 1.782f };

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            ConfigureAudioSource();
#if UNITY_EDITOR
            AutoAssignMissingNoteClipsInEditor();
#endif
        }

        private void Start()
        {
            Build();
            Close();
            if (PianoPuzzle.Instance != null)
            {
                PianoPuzzle.Instance.OnPianoCompleted += OnCompleted;
                PianoPuzzle.Instance.OnPianoFailed += OnFailed;
            }
        }

        private void OnDestroy()
        {
            if (PianoPuzzle.Instance == null)
                return;

            PianoPuzzle.Instance.OnPianoCompleted -= OnCompleted;
            PianoPuzzle.Instance.OnPianoFailed -= OnFailed;
        }

        private void Update()
        {
            if (GameController.IsCutsceneOrEndInputLocked())
            {
                if (IsOpen)
                    Close();
                return;
            }

            if (!IsOpen || Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();

            for (int i = 0; i < KeyboardNotes.Length; i++)
            {
                if (WasNoteKeyPressed(KeyboardNotes[i]))
                    Play(KeyboardNotes[i], KeyboardPitches[i]);
            }
        }

        public void Open()
        {
            if (GameController.Instance != null && !GameController.Instance.CanUseGameplayInput())
                return;

            Build();
            root.SetActive(true);
            completed = false;
            PianoPuzzle.Instance?.ResetPuzzle();
            SetGameplayBlocked(true);
        }

        public void Close()
        {
            if (root != null)
                root.SetActive(false);
            SetGameplayBlocked(false);
        }

        private void Play(string note, float pitch)
        {
            if (completed)
                return;

            PlayNoteAudio(note, pitch);
            PianoPuzzle.Instance?.PlayNote(note);
        }

        private void PlayNoteAudio(string note, float pitch)
        {
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            ConfigureAudioSource();

            if (audioSource != null)
            {
                var clip = ClipForNote(note);
                if (clip != null)
                {
                    bool usingFallbackClip = clip == noteClip && ClipForNoteWithoutFallback(note) == null;
                    audioSource.pitch = usingFallbackClip ? pitch : 1f;
                    audioSource.PlayOneShot(clip);
                }
            }
        }

        private AudioClip ClipForNote(string note)
        {
            return ClipForNoteWithoutFallback(note) ?? noteClip;
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
                return;

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
            audioSource.ignoreListenerPause = true;
        }

        private AudioClip ClipForNoteWithoutFallback(string note)
        {
            return note switch
            {
                "C" => noteClipC,
                "D" => noteClipD,
                "E" => noteClipE,
                "F" => noteClipF,
                "G" => noteClipG,
                "A" => noteClipA,
                "B" => noteClipB,
                "C2" => noteClipHighC,
                "C#" => noteClipCSharp,
                "D#" => noteClipDSharp,
                "F#" => noteClipFSharp,
                "G#" => noteClipGSharp,
                "A#" => noteClipASharp,
                _ => null
            };
        }

        private static bool WasNoteKeyPressed(string note)
        {
            var keyboard = Keyboard.current;
            return note switch
            {
                "C" => keyboard.cKey.wasPressedThisFrame,
                "D" => keyboard.dKey.wasPressedThisFrame,
                "E" => keyboard.eKey.wasPressedThisFrame,
                "F" => keyboard.fKey.wasPressedThisFrame,
                "G" => keyboard.gKey.wasPressedThisFrame,
                "A" => keyboard.aKey.wasPressedThisFrame,
                "B" => keyboard.bKey.wasPressedThisFrame,
                "C#" => WasNumberKeyPressed(1),
                "D#" => WasNumberKeyPressed(2),
                "F#" => WasNumberKeyPressed(3),
                "G#" => WasNumberKeyPressed(4),
                "A#" => WasNumberKeyPressed(5),
                _ => false
            };
        }

        private static bool WasNumberKeyPressed(int number)
        {
            var keyboard = Keyboard.current;
            return number switch
            {
                1 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                2 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                3 => keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
                4 => keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame,
                5 => keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame,
                _ => false
            };
        }

        private void OnCompleted()
        {
            completed = true;
            Close();
        }

        private void OnFailed()
        {
            if (!IsOpen)
                return;

            completed = false;
            Close();
        }

        private void SetGameplayBlocked(bool blocked)
        {
            if (blocked)
            {
                if (PlayerInteract.Instance != null) PlayerInteract.Instance.sendRaycast = false;
                if (GameController.Instance != null) GameController.Instance.SetGameState(GameController.GameState.Puzzle);
                else InteractCameraSettings.Instance?.ShowCursor();
                return;
            }

            if (GameController.Instance != null && GameController.Instance.currentGameState == GameController.GameState.Puzzle && !(InventoryUI.Instance != null && InventoryUI.Instance.IsOpen))
                GameController.Instance.SetGameState(GameController.GameState.Gameplay);
            if (PlayerInteract.Instance != null) PlayerInteract.Instance.sendRaycast = true;
            if (GameController.Instance == null)
                InteractCameraSettings.Instance?.HideCursor();
        }

        private void Build()
        {
            if (root != null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            root = CreateUIObject(transform, "PianoPuzzleOverlay").gameObject;
            Stretch(root.GetComponent<RectTransform>());
            AddImage(root.transform, "DimBackground", null, new Color(0.01f, 0.02f, 0.03f, 0.72f), Image.Type.Simple, true);

            var panel = AddImage(root.transform, "PianoPanel", panelSprite, new Color(0.018f, 0.055f, 0.078f, 0.92f), Image.Type.Sliced, false);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.21f, 0.19f);
            panelRect.anchorMax = new Vector2(0.79f, 0.76f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            AddText(panel.transform, "Title", "ĐÀN PIANO", 38, new Color(0.86f, 0.96f, 1f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.83f), new Vector2(0.9f, 0.94f));
            AddImage(panel.transform, "Divider", dividerSprite, new Color(0.7f, 0.85f, 0.9f, 0.72f), Image.Type.Sliced, false).rectTransform.sizeDelta = new Vector2(300f, 12f);

            var piano = AddImage(panel.transform, "PianoArt", pianoBackgroundSprite, new Color(1f, 1f, 1f, 0.9f), Image.Type.Sliced, false);
            var pianoRect = piano.rectTransform;
            pianoRect.anchorMin = new Vector2(0.12f, 0.16f);
            pianoRect.anchorMax = new Vector2(0.88f, 0.64f);
            pianoRect.offsetMin = Vector2.zero;
            pianoRect.offsetMax = Vector2.zero;

            for (int i = 0; i < WhiteNotes.Length; i++)
            {
                var key = CreateButton(piano.transform, $"WhiteKey_{WhiteNotes[i]}", whiteKeySprite, WhiteLabels[i], 30);
                var rect = key.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(i / 8f, 0f);
                rect.anchorMax = new Vector2((i + 1f) / 8f, 1f);
                rect.offsetMin = new Vector2(5f, 8f);
                rect.offsetMax = new Vector2(-5f, -8f);
                string note = WhiteNotes[i];
                float pitch = FallbackPitchForNote(note);
                AddKeyPressHandler(key.gameObject, note, pitch);
            }

            float[] blackPositions = { 0.0975f, 0.2225f, 0.4725f, 0.5975f, 0.7225f };
            for (int i = 0; i < blackPositions.Length; i++)
            {
                var black = AddImage(piano.transform, $"BlackKey_{i + 1}", null, new Color(0.01f, 0.012f, 0.014f, 0.96f), Image.Type.Simple, false);
                var button = black.gameObject.AddComponent<Button>();
                button.targetGraphic = black;
                var rect = black.rectTransform;
                rect.anchorMin = new Vector2(blackPositions[i], 0.42f);
                rect.anchorMax = new Vector2(blackPositions[i] + 0.055f, 0.98f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                string note = BlackNotes[i];
                float pitch = FallbackPitchForNote(note);
                AddKeyPressHandler(button.gameObject, note, pitch);
                black.transform.SetAsLastSibling();
            }

            var close = CreateButton(panel.transform, "CloseButton", null, "X", 28);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.91f, 0.84f);
            closeRect.anchorMax = new Vector2(0.97f, 0.94f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            close.onClick.AddListener(Close);
        }

        private Button CreateButton(Transform parent, string name, Sprite sprite, string label, int fontSize)
        {
            var image = AddImage(parent, name, sprite, Color.white, Image.Type.Sliced, false);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddText(image.transform, "Text", label, fontSize, new Color(0.07f, 0.09f, 0.1f), TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            return button;
        }

        private void AddKeyPressHandler(GameObject keyObject, string note, float pitch)
        {
            var handler = keyObject.GetComponent<PianoKeyPressHandler>() ?? keyObject.AddComponent<PianoKeyPressHandler>();
            handler.Setup(this, note, pitch);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static float FallbackPitchForNote(string note)
        {
            return note switch
            {
                "C" => 1f,
                "C#" => 1.059f,
                "D" => 1.122f,
                "D#" => 1.189f,
                "E" => 1.26f,
                "F" => 1.335f,
                "F#" => 1.414f,
                "G" => 1.498f,
                "G#" => 1.587f,
                "A" => 1.682f,
                "A#" => 1.782f,
                "B" => 1.888f,
                "C2" => 2f,
                _ => 1f
            };
        }

        private TextMeshProUGUI AddText(Transform parent, string name, string text, int size, Color color, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var rect = CreateUIObject(parent, name);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            if (font != null) label.font = font;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return label;
        }

        private Image AddImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type, bool stretch)
        {
            var rect = CreateUIObject(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null ? type : Image.Type.Simple;
            if (stretch) Stretch(rect);
            return image;
        }

        private static RectTransform CreateUIObject(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private sealed class PianoKeyPressHandler : MonoBehaviour, IPointerDownHandler
        {
            private PianoPuzzleUI owner;
            private string note;
            private float pitch = 1f;

            public void Setup(PianoPuzzleUI pianoOwner, string pianoNote, float fallbackPitch)
            {
                owner = pianoOwner;
                note = pianoNote;
                pitch = fallbackPitch;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                owner?.Play(note, pitch);
            }
        }

#if UNITY_EDITOR
        private void AutoAssignMissingNoteClipsInEditor()
        {
            noteClipC ??= LoadEditorClip("c1");
            noteClipD ??= LoadEditorClip("d1");
            noteClipE ??= LoadEditorClip("e1");
            noteClipF ??= LoadEditorClip("f1");
            noteClipG ??= LoadEditorClip("g1");
            noteClipA ??= LoadEditorClip("a1");
            noteClipB ??= LoadEditorClip("b1");
            noteClipHighC ??= LoadEditorClip("c2");
            noteClipCSharp ??= LoadEditorClip("c1s");
            noteClipDSharp ??= LoadEditorClip("d1s");
            noteClipFSharp ??= LoadEditorClip("f1s");
            noteClipGSharp ??= LoadEditorClip("g1s");
            noteClipASharp ??= LoadEditorClip("a1s");
            noteClip ??= noteClipC;
        }

        private static AudioClip LoadEditorClip(string fileName)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/MainGame/Audio/wav/{fileName}.wav");
        }
#endif
    }
}
