using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MainGame.P2
{
    public enum P2Stage
    {
        Arrival,
        Lobby,
        JournalRead,
        HallReached,
        PaintingSeen,
        HasKey05,
        CabinetOpened,
        BathPassed,
        LinhRoomReached,
        DollHeard,
        ChalkSeen,
        RecorderHeard,
        WallOpened,
        MirrorTaken,
        MirrorBroken,
        Backyard,
        Ending
    }

    public enum P2InteractableKind
    {
        CoveredLobbyMirror,
        BaLanJournal,
        UpsideDownPainting,
        Key05,
        JewelryCabinet,
        AudioLogBL02,
        BathWater,
        Candle,
        Doll,
        ChalkNotes,
        AudioLogBL03,
        WallPanel,
        SilverMirror,
        BackyardMoonMirror
    }

    public enum P2TriggerKind
    {
        EnterLobby,
        HallAutoLog,
        EnterLinhRoom,
        EscapeShardRoute,
        BackyardDeath
    }

    public sealed class P2GameController : MonoBehaviour
    {
        public static P2GameController Instance { get; private set; }

        [Header("Scene")]
        [SerializeField] private P2FirstPersonController player;
        [SerializeField] private P2OilLamp oilLamp;
        [SerializeField] private P2GhostController ghost;
        [SerializeField] private Transform deathPullTarget;
        [SerializeField] private Transform silverMirrorProp;
        [SerializeField] private GameObject hiddenWallCavity;

        [Header("UI")]
        [SerializeField] private CanvasGroup hudGroup;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private GameObject deathCard;
        [SerializeField] private TMP_Text deathCardText;

        [Header("Runtime Rules")]
        [SerializeField] private bool runOpeningWhenNoChapterCutscene;
        [SerializeField] private bool enableP2VoiceLines;
        [SerializeField] private bool showP2DebugStage;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource ambienceSource;

        [Header("Chapter 2 Voice")]
        [SerializeField] private AudioClip ngocIntro01;
        [SerializeField] private AudioClip ngocIntro02;
        [SerializeField] private AudioClip linhHallAutoLog;
        [SerializeField] private AudioClip ngocAfterJournal;
        [SerializeField] private AudioClip ngocBathWarning;
        [SerializeField] private AudioClip linhDollLog;
        [SerializeField] private AudioClip ngocCandleReaction;
        [SerializeField] private AudioClip ngocChalkReaction;
        [SerializeField] private AudioClip ngocHollowWall;
        [SerializeField] private AudioClip ngocMirrorFound;
        [SerializeField] private AudioClip ngocMirrorBreak;
        [SerializeField] private AudioClip maVuDaiLine01;
        [SerializeField] private AudioClip maVuDaiLine02;
        [SerializeField] private AudioClip maDaMirror01;
        [SerializeField] private AudioClip maDaMirror02;
        [SerializeField] private AudioClip ngocFinalLine;
        [SerializeField] private AudioClip audioLogBL02;
        [SerializeField] private AudioClip audioLogBL03;

        [Header("SFX")]
        [SerializeField] private AudioClip knockSolidClip;
        [SerializeField] private AudioClip knockHollowClip;
        [SerializeField] private AudioClip glassBreakClip;
        [SerializeField] private AudioClip glassStepClip;
        [SerializeField] private AudioClip cabinetUnlockClip;
        [SerializeField] private AudioClip pickupClip;

        public P2Stage CurrentStage { get; private set; } = P2Stage.Arrival;
        public bool IsInputLocked { get; private set; }
        public bool HasKey05 { get; private set; }
        public bool HasSilverMirror { get; private set; }
        public bool MirrorEventTriggered { get; private set; }
        public bool CanReadLore => oilLamp == null || oilLamp.IsLit;

        private readonly WaitForSeconds shortBeat = new WaitForSeconds(0.8f);
        private Coroutine subtitleRoutine;
        private float lastShardNoiseTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (player == null)
                player = FindFirstObjectByType<P2FirstPersonController>();
            if (oilLamp == null)
                oilLamp = FindGameplayOilLamp();
            if (ghost == null)
                ghost = FindFirstObjectByType<P2GhostController>();

            ApplyAudioDataFallbacks();

            if (deathCard != null)
                deathCard.SetActive(false);
            if (hiddenWallCavity != null)
                hiddenWallCavity.SetActive(false);

            SetStage(P2Stage.Arrival);
            LockInput(false);

            var chapterIntroWillPlay = GameController.Instance != null
                && GameController.Instance.playIntroOnStart
                && GameController.Instance.cutSceneManager != null
                && GameController.Instance.currentChapterPhase == GameController.ChapterPhase.Intro;

            SetHudVisible(!chapterIntroWillPlay);
            if (!chapterIntroWillPlay && runOpeningWhenNoChapterCutscene)
                StartCoroutine(PlayOpening());
            else if (!chapterIntroWillPlay)
                SetObjective(string.Empty);
        }

        public void LockInput(bool locked)
        {
            IsInputLocked = locked;
            if (player != null)
                player.SetInputLocked(locked);
        }

        public void SetHudVisible(bool visible)
        {
            if (hudGroup == null)
                return;

            hudGroup.alpha = visible ? 1f : 0f;
            hudGroup.interactable = visible;
            hudGroup.blocksRaycasts = visible;
        }

        public void ShowPrompt(string message)
        {
            if (promptText != null)
                promptText.text = message;
        }

        public void ClearPrompt()
        {
            if (promptText != null)
                promptText.text = string.Empty;
        }

        public bool CanInteract(P2Interactable interactable)
        {
            if (interactable == null || interactable.HasInteracted && interactable.OneShot)
                return false;

            if (!CanReadLore && interactable.RequiresLight)
            {
                ShowPrompt("Cần thắp đèn dầu để đọc/tương tác.");
                return false;
            }

            return true;
        }

        public void Interact(P2Interactable interactable)
        {
            if (!CanInteract(interactable))
                return;

            interactable.MarkInteracted();

            switch (interactable.Kind)
            {
                case P2InteractableKind.CoveredLobbyMirror:
                    ShowSubtitle("Bà dặn đừng nhìn vào mặt nước. Cái này... để nguyên.", 3.5f);
                    break;

                case P2InteractableKind.BaLanJournal:
                    ShowReadable(
                        "Nhật ký Bà Lan",
                        "Mọi mặt nước trong nhà này, hễ để yên quá lâu không ai động tới, đều bắt đầu nhìn lại mình.\n\n" +
                        "Tôi phủ vải lên hết những tấm gương tôi tìm được - trừ một tấm tôi giấu kỹ.\n\n" +
                        "Xin tìm tới phòng con Linh trước. Gió không đổi hướng ở đó là vì có thứ gì che chắn nó khỏi mọi luồng khí trong nhà.");
                    PlayVoice(ngocAfterJournal, "Gió không đổi hướng. Phòng bé Linh. Tường phía tây.");
                    SetStage(P2Stage.JournalRead);
                    break;

                case P2InteractableKind.UpsideDownPainting:
                    ShowSubtitle("Năm người... nhà này chỉ có bốn người thôi mà.", 3f);
                    SetStage(P2Stage.PaintingSeen);
                    break;

                case P2InteractableKind.Key05:
                    HasKey05 = true;
                    PlaySfx(pickupClip);
                    ShowSubtitle("Đã lấy KEY_05 - chìa khóa tủ trang sức phòng Bà Lan.", 3f);
                    SetStage(P2Stage.HasKey05);
                    interactable.gameObject.SetActive(false);
                    break;

                case P2InteractableKind.JewelryCabinet:
                    if (!HasKey05)
                    {
                        ShowSubtitle("Tủ trang sức khóa. Cần KEY_05.", 2.5f);
                        interactable.ResetOneShot();
                        return;
                    }

                    PlaySfx(cabinetUnlockClip);
                    ShowSubtitle("Ổ khóa mở khẽ. Bên trong có một hộp ghi âm cơ học.", 3f);
                    SetStage(P2Stage.CabinetOpened);
                    interactable.OpenLinkedObject();
                    break;

                case P2InteractableKind.AudioLogBL02:
                    PlayVoice(audioLogBL02,
                        "Mấy đêm nay tôi không sao ngủ được. Có tiếng gõ vào mặt gương lúc nửa đêm... " +
                        "Bài nhạc... tôi mới ghi được năm nốt đầu. Hai nốt cuối... tôi không biết mình còn đủ can đảm để tìm ra hay không.",
                        9f);
                    break;

                case P2InteractableKind.BathWater:
                    PlayVoice(ngocBathWarning, "Bà dặn đừng nhìn vào mặt nước. Đừng nhìn vào.", 3f);
                    SetStage(P2Stage.BathPassed);
                    break;

                case P2InteractableKind.Candle:
                    PlayVoice(ngocCandleReaction, "Gió không vào được đây. Đúng như trong nhật ký.", 3f);
                    break;

                case P2InteractableKind.Doll:
                    PlayVoice(linhDollLog, "Cái người trong giếng... nó nói nó ở đây lâu lắm rồi. Nó muốn con xuống chơi với nó.", 5f);
                    SetStage(P2Stage.DollHeard);
                    break;

                case P2InteractableKind.ChalkNotes:
                    ShowReadable("Nét phấn trên tường", "E - C - F - D - G\n\nNăm nốt đầu. Hai nốt cuối vẫn còn thiếu.");
                    PlayVoice(ngocChalkReaction, "Năm nốt... đúng như bà kể. Nhưng còn thiếu.", 3f);
                    SetStage(P2Stage.ChalkSeen);
                    break;

                case P2InteractableKind.AudioLogBL03:
                    PlayVoice(audioLogBL03,
                        "Đây là chỗ duy nhất trong nhà tôi còn thấy yên tâm để viết. Năm nốt... tôi khắc bằng phấn để nhớ, vì tôi sợ trí nhớ của mình không còn đáng tin nữa.",
                        7f);
                    SetStage(P2Stage.RecorderHeard);
                    break;

                case P2InteractableKind.WallPanel:
                    var puzzle = interactable.GetComponentInParent<P2WallKnockPuzzle>();
                    if (puzzle != null)
                        puzzle.Knock(interactable);
                    break;

                case P2InteractableKind.SilverMirror:
                    HasSilverMirror = true;
                    PlaySfx(pickupClip);
                    PlayVoice(ngocMirrorFound, "Đây rồi... tấm gương bạc bà dặn.", 3f);
                    SetStage(P2Stage.MirrorTaken);
                    interactable.gameObject.SetActive(false);
                    TriggerMirrorBreakEvent();
                    break;

                case P2InteractableKind.BackyardMoonMirror:
                    StartEndingSequence();
                    break;
            }
        }

        public void HandleTrigger(P2TriggerKind kind)
        {
            switch (kind)
            {
                case P2TriggerKind.EnterLobby:
                    SetHudVisible(true);
                    LockInput(false);
                    SetStage(P2Stage.Lobby);
                    break;

                case P2TriggerKind.HallAutoLog:
                    if (CurrentStage < P2Stage.HallReached)
                    {
                        SetStage(P2Stage.HallReached);
                        PlayVoice(linhHallAutoLog, "Má ơi... con thấy nó lại rồi. Trong cái gương ở phòng tắm...", 4f);
                    }
                    break;

                case P2TriggerKind.EnterLinhRoom:
                    if (CurrentStage < P2Stage.LinhRoomReached)
                        SetStage(P2Stage.LinhRoomReached);
                    break;

                case P2TriggerKind.EscapeShardRoute:
                    if (MirrorEventTriggered && CurrentStage < P2Stage.Backyard)
                        SetObjective("Chạy ra sân sau. Đừng dừng lại.");
                    break;

                case P2TriggerKind.BackyardDeath:
                    if (MirrorEventTriggered)
                        StartEndingSequence();
                    break;
            }
        }

        public void RegisterWallOpened(GameObject cavity)
        {
            if (cavity != null)
                cavity.SetActive(true);
            if (hiddenWallCavity != null)
                hiddenWallCavity.SetActive(true);

            PlayVoice(ngocHollowWall, "Rỗng. Ngay chỗ này.", 2.5f);
            SetStage(P2Stage.WallOpened);
        }

        public void PlayKnock(bool hollow)
        {
            PlaySfx(hollow ? knockHollowClip : knockSolidClip);
        }

        public void PlayShardNoise(Vector3 position)
        {
            if (Time.time - lastShardNoiseTime < 0.7f)
                return;

            lastShardNoiseTime = Time.time;
            PlaySfx(glassStepClip);
            ghost?.Investigate(position);
            ShowSubtitle("Mảnh gương vỡ lạo xạo dưới chân.", 1.8f);
        }

        public void TriggerMirrorBreakEvent()
        {
            if (MirrorEventTriggered)
                return;

            MirrorEventTriggered = true;
            PlaySfx(glassBreakClip);
            P2MirrorBreakable.BreakAll();
            ghost?.Awaken();
            FindFirstObjectByType<P2GhostDoorApparitionDirector>(FindObjectsInactive.Include)?.Awaken();
            if (ambienceSource != null)
                ambienceSource.pitch = 0.82f;

            StartCoroutine(MirrorBreakBeat());
        }

        public float DistanceToPlayer(Vector3 worldPosition)
        {
            return player == null ? 999f : Vector3.Distance(player.transform.position, worldPosition);
        }

        private static P2OilLamp FindGameplayOilLamp()
        {
            P2OilLamp fallback = null;
            foreach (var lamp in FindObjectsByType<P2OilLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lamp == null)
                    continue;

                fallback ??= lamp;
                if (lamp.ControlsGameplaySystems)
                    return lamp;
            }

            return fallback;
        }

        public void PlayGhostLine()
        {
            bool firstLine = UnityEngine.Random.value > 0.5f;
            PlayVoice(firstLine ? maVuDaiLine01 : maVuDaiLine02,
                firstLine ? "Ngọc ơi... con ở đâu vậy..." : "Sao mấy đứa cứ chạy trốn má hoài... má có làm gì đâu...",
                4f);
        }

        private void ApplyAudioDataFallbacks()
        {
            var data = Resources.Load<AudioData>("Audio/AudioData");
            if (data == null)
                return;

            ngocIntro01 ??= data.p2Ngoc01;
            ngocIntro02 ??= data.p2Ngoc02;
            linhHallAutoLog ??= data.p2Linh01;
            ngocAfterJournal ??= data.p2Ngoc03;
            ngocBathWarning ??= data.p2Ngoc04;
            linhDollLog ??= data.p2Linh02;
            ngocCandleReaction ??= data.p2Ngoc05;
            ngocChalkReaction ??= data.p2Ngoc06;
            ngocHollowWall ??= data.p2Ngoc07;
            ngocMirrorFound ??= data.p2Ngoc08;
            ngocMirrorBreak ??= data.p2Ngoc09;
            maVuDaiLine01 ??= data.p2Ma01;
            maVuDaiLine02 ??= data.p2Ma02;
            maDaMirror01 ??= data.p2MaDa02;
            maDaMirror02 ??= data.p2MaDa03;
            ngocFinalLine ??= data.p2Ngoc10;
            audioLogBL02 ??= data.p2AudioLogBL02;
            audioLogBL03 ??= data.p2AudioLogBL03;
        }

        public void StartEndingSequence()
        {
            if (CurrentStage == P2Stage.Ending)
                return;

            StartCoroutine(EndingRoutine());
        }

        private IEnumerator PlayOpening()
        {
            LockInput(true);
            SetHudVisible(false);
            PlayVoice(ngocIntro01, "Bà ơi, con đã đến rồi. Bà nói cái nhà này giữ thứ có thể cứu họ. Con không hiểu hết - nhưng con tin bà.", 6f);
            yield return new WaitForSeconds(5.5f);
            PlayVoice(ngocIntro02, "Tấm gương bạc. Tìm được thì đem về. Bà dặn... đừng nhìn vào mặt nước trong nhà. Tuyệt đối không.", 5f);
            yield return new WaitForSeconds(4f);
            SetHudVisible(true);
            LockInput(false);
            SetObjective("Vào biệt thự và tìm manh mối về tấm gương bạc.");
        }

        private IEnumerator MirrorBreakBeat()
        {
            LockInput(true);
            SetHudVisible(false);
            PlayVoice(ngocMirrorBreak, "...Cái gì vậy?! Khắp nhà... tất cả cùng lúc- ...Con phải đi thôi.", 5f);
            yield return new WaitForSeconds(3.5f);
            PlayGhostLine();
            yield return shortBeat;
            LockInput(false);
            SetHudVisible(true);
            SetStage(P2Stage.MirrorBroken);
        }

        private IEnumerator EndingRoutine()
        {
            SetStage(P2Stage.Ending);
            LockInput(true);
            SetHudVisible(false);
            if (ghost != null)
                ghost.gameObject.SetActive(false);

            PlayVoice(ngocFinalLine, "Gương bạc... bà nói soi vào trăng thì có thể... nhưng bà không nói phải làm gì tiếp theo. Con còn thiếu gì đó.", 6f);
            yield return new WaitForSeconds(4.5f);
            PlayVoice(maDaMirror01, "...Bà ơi.", 3f);
            yield return new WaitForSeconds(1.2f);
            PlayVoice(maDaMirror02, string.Empty, 2f);

            var start = player != null ? player.transform.position : transform.position;
            var end = deathPullTarget != null ? deathPullTarget.position : start + Vector3.forward * 2f;
            var timer = 0f;
            const float duration = 2.2f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, timer / duration);
                if (player != null)
                    player.Teleport(Vector3.Lerp(start, end, t));
                if (silverMirrorProp != null)
                    silverMirrorProp.localRotation = Quaternion.Euler(70f + 50f * t, 0f, 35f * t);
                yield return null;
            }

            if (deathCardText != null)
                deathCardText.text = "NGUYỄN THỊ BÍCH NGỌC · 1951 - 1970";
            if (deathCard != null)
                deathCard.SetActive(true);
        }

        private void SetStage(P2Stage stage)
        {
            if (stage < CurrentStage && CurrentStage != P2Stage.Ending)
                return;

            CurrentStage = stage;
            if (stageText != null && showP2DebugStage)
                stageText.text = $"P2 · {stage}";
            else if (stageText != null)
                stageText.text = string.Empty;

            switch (stage)
            {
                case P2Stage.Arrival:
                    SetObjective("Đi theo đường mòn tới cổng biệt thự.");
                    break;
                case P2Stage.Lobby:
                    SetObjective("Đừng chạm tấm gương phủ vải. Tìm thư phòng tầng trệt.");
                    break;
                case P2Stage.JournalRead:
                    SetObjective("Tìm phòng bé Linh ở tầng một. Ghi nhớ: tường phía tây.");
                    break;
                case P2Stage.HallReached:
                    SetObjective("Khám phá các phòng tầng một và tìm manh mối còn lại.");
                    break;
                case P2Stage.HasKey05:
                    SetObjective("Dùng KEY_05 mở tủ trang sức trong phòng Bà Lan.");
                    break;
                case P2Stage.CabinetOpened:
                    SetObjective("Nghe hộp ghi âm trong tủ trang sức.");
                    break;
                case P2Stage.LinhRoomReached:
                    SetObjective("Kiểm tra nến, búp bê, nét phấn và bức tường phía tây.");
                    break;
                case P2Stage.WallOpened:
                    SetObjective("Lấy tấm gương bạc trong hốc tường.");
                    break;
                case P2Stage.MirrorBroken:
                    SetObjective("Ma Vú Dài đã thức dậy. Thoát xuống sân sau.");
                    break;
                case P2Stage.Ending:
                    SetObjective(string.Empty);
                    break;
            }
        }

        private void SetObjective(string message)
        {
            if (objectiveText != null)
                objectiveText.text = message;
        }

        private void ShowReadable(string title, string body)
        {
            ShowSubtitle($"{title}\n{body}", 8f);
        }

        private void ShowSubtitle(string message, float seconds)
        {
            if (subtitleRoutine != null)
                StopCoroutine(subtitleRoutine);
            subtitleRoutine = StartCoroutine(SubtitleRoutine(message, seconds));
        }

        private void PlayVoice(AudioClip clip, string fallbackSubtitle, float fallbackSeconds = 4f)
        {
            if (!enableP2VoiceLines)
                return;

            if (clip != null && voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.clip = clip;
                voiceSource.Play();
                ShowSubtitle(string.IsNullOrWhiteSpace(fallbackSubtitle) ? clip.name : fallbackSubtitle, Mathf.Max(fallbackSeconds, clip.length));
                return;
            }

            if (!string.IsNullOrWhiteSpace(fallbackSubtitle))
                ShowSubtitle(fallbackSubtitle, fallbackSeconds);
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
                sfxSource.PlayOneShot(clip);
        }

        private IEnumerator SubtitleRoutine(string message, float seconds)
        {
            if (subtitleText != null)
                subtitleText.text = message;
            yield return new WaitForSeconds(seconds);
            if (subtitleText != null)
                subtitleText.text = string.Empty;
            subtitleRoutine = null;
        }
    }

    public sealed class P2FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private bool driveInput = true;
        [SerializeField] private MonoBehaviour[] externalMovementBehaviours = Array.Empty<MonoBehaviour>();
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 5.4f;
        [SerializeField] private float mouseSensitivity = 0.08f;
        [SerializeField] private float gravity = -18f;

        private float pitch;
        private float verticalVelocity;
        private bool inputLocked;
        private Vector3 lastPosition;

        public bool IsRunning { get; private set; }
        public float MoveSpeed { get; private set; }
        public Camera PlayerCamera => playerCamera;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!driveInput)
            {
                MoveSpeed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                IsRunning = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && MoveSpeed > 0.4f;
                lastPosition = transform.position;
                return;
            }

            if (inputLocked)
            {
                MoveSpeed = 0f;
                IsRunning = false;
                return;
            }

            Look();
            Move();
        }

        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            if (externalMovementBehaviours == null)
                return;

            foreach (var behaviour in externalMovementBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = !locked;
            }
        }

        public void Teleport(Vector3 position)
        {
            if (characterController != null)
                characterController.enabled = false;
            transform.position = position;
            if (characterController != null)
                characterController.enabled = true;
        }

        private void Look()
        {
            var mouse = Mouse.current;
            if (mouse == null || playerCamera == null)
                return;

            var delta = mouse.delta.ReadValue() * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
            playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
            transform.Rotate(Vector3.up * delta.x);
        }

        private void Move()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || characterController == null)
                return;

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            IsRunning = keyboard.leftShiftKey.isPressed && input.sqrMagnitude > 0.01f;
            var speed = IsRunning ? runSpeed : walkSpeed;
            var direction = transform.right * input.x + transform.forward * input.y;

            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;
            verticalVelocity += gravity * Time.deltaTime;

            characterController.Move((direction * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
            MoveSpeed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = transform.position;
        }
    }

    public sealed class P2Interactor : MonoBehaviour
    {
        [SerializeField] private P2GameController controller;
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask interactMask = ~0;

        private P2Interactable focused;

        private void Awake()
        {
            if (controller == null)
                controller = P2GameController.Instance;
            if (sourceCamera == null)
                sourceCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            if (controller == null || controller.IsInputLocked)
                return;

            focused = FindFocused();
            if (focused == null)
            {
                controller.ClearPrompt();
                return;
            }

            controller.ShowPrompt(focused.GetPrompt());
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                controller.Interact(focused);
        }

        private P2Interactable FindFocused()
        {
            if (sourceCamera == null)
                return null;

            var ray = new Ray(sourceCamera.transform.position, sourceCamera.transform.forward);
            return Physics.Raycast(ray, out var hit, interactDistance, interactMask, QueryTriggerInteraction.Collide)
                ? hit.collider.GetComponentInParent<P2Interactable>()
                : null;
        }
    }

    public sealed class P2Interactable : MonoBehaviour
    {
        [SerializeField] private P2InteractableKind kind;
        [SerializeField] private string displayName = "Tương tác";
        [SerializeField] private bool oneShot = true;
        [SerializeField] private bool requiresLight = true;
        [SerializeField] private GameObject linkedObject;

        public P2InteractableKind Kind => kind;
        public bool HasInteracted { get; private set; }
        public bool OneShot => oneShot;
        public bool RequiresLight => requiresLight;

        public string GetPrompt()
        {
            return $"E · {displayName}";
        }

        public void Configure(P2InteractableKind newKind, string newName, bool needsLight = true, bool singleUse = true, GameObject link = null)
        {
            kind = newKind;
            displayName = newName;
            requiresLight = needsLight;
            oneShot = singleUse;
            linkedObject = link;
        }

        public void MarkInteracted()
        {
            HasInteracted = true;
        }

        public void ResetOneShot()
        {
            HasInteracted = false;
        }

        public void OpenLinkedObject()
        {
            if (linkedObject != null)
                linkedObject.SetActive(true);
        }
    }

    public sealed class P2StageTrigger : MonoBehaviour
    {
        [SerializeField] private P2TriggerKind kind;
        [SerializeField] private bool oneShot = true;
        private bool triggered;

        public void Configure(P2TriggerKind triggerKind, bool singleUse = true)
        {
            kind = triggerKind;
            oneShot = singleUse;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered && oneShot)
                return;
            if (other.GetComponentInParent<P2FirstPersonController>() == null)
                return;

            triggered = true;
            P2GameController.Instance?.HandleTrigger(kind);
        }
    }

    internal sealed class P2OilLampOld : MonoBehaviour
    {
        [SerializeField] private Light flameLight;
        [SerializeField] private Renderer flameRenderer;
        [SerializeField] private ParticleSystem[] flameParticles = Array.Empty<ParticleSystem>();
        [SerializeField] private P2GhostController ghost;
        [SerializeField] private Transform shakeRoot;
        [SerializeField] private Image oilFillImage;
        [SerializeField, Range(0f, 100f)] private float oilPercent = 100f;
        [SerializeField, Min(0f)] private float oilDrainPerSecond = 0.15f;
        [SerializeField, Min(0f)] private float flameBaseIntensity = 1.15f;
        [SerializeField, Min(0f)] private float flamePulseIntensity = 0.45f;
        [SerializeField, Min(0f)] private float flameBaseRange = 2.4f;
        [SerializeField, Min(0f)] private float flameDangerRange = 1.8f;
        [SerializeField, Min(0.1f)] private float nearGhostEffectStartDistance = 12f;
        [SerializeField, Min(0.1f)] private float nearGhostFullEffectDistance = 2f;
        [SerializeField, Min(0f)] private float dangerShakePosition = 0.045f;
        [SerializeField, Min(0f)] private float dangerShakeRotation = 5f;
        [SerializeField] private Color normalFlameColor = new Color(1f, 0.48f, 0.16f);
        [SerializeField] private Color dangerFlameColor = new Color(0.35f, 0.85f, 1f);
        [SerializeField] private bool debugNearGhostEffectZone = true;
        [SerializeField] private float directAttackSeconds = 10f;
        [SerializeField] private float dangerDistance = 7f;

        public bool IsLit { get; private set; } = true;

        private float unlitNearGhostTimer;
        private Material flameMaterial;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;

        private void Awake()
        {
            if (shakeRoot == null)
                shakeRoot = transform;
            if (flameLight == null)
                flameLight = GetComponentInChildren<Light>();
            if (flameRenderer != null)
                flameMaterial = flameRenderer.material;
            baseLocalPosition = shakeRoot.localPosition;
            baseLocalRotation = shakeRoot.localRotation;
            ResolveOilFillImage();
            UpdateOilUi();
            SetFlameParticles(IsLit);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && !(P2GameController.Instance?.IsInputLocked ?? false))
                SetLit(!IsLit);

            DrainOil();
            ApplyFlicker();
            ApplyNearGhostReaction();
            TrackUnlitDanger();
            UpdateOilUi();
        }

        public void SetLit(bool lit)
        {
            if (lit && oilPercent <= 0f)
            {
                IsLit = false;
                if (flameLight != null)
                    flameLight.enabled = false;
                if (flameRenderer != null)
                    flameRenderer.enabled = false;
                SetFlameParticles(false);
                P2GameController.Instance?.ShowPrompt("Đèn dầu đã hết dầu.");
                UpdateOilUi();
                return;
            }

            IsLit = lit;
            if (flameLight != null)
                flameLight.enabled = lit;
            if (flameRenderer != null)
                flameRenderer.enabled = lit;
            SetFlameParticles(lit);
            P2GameController.Instance?.ShowPrompt(lit ? "Đèn dầu đã cháy lại." : "Đèn dầu tắt. Không thể đọc chữ.");
        }

        private void SetFlameParticles(bool lit)
        {
            if (flameParticles == null)
                return;

            foreach (var particles in flameParticles)
            {
                if (particles == null)
                    continue;

                if (lit)
                {
                    if (!particles.isPlaying)
                        particles.Play(true);
                }
                else
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void DrainOil()
        {
            if (!IsLit || oilDrainPerSecond <= 0f)
                return;

            oilPercent = Mathf.Max(0f, oilPercent - oilDrainPerSecond * Time.deltaTime);
            if (oilPercent <= 0f)
                SetLit(false);
        }

        private void UpdateOilUi()
        {
            ResolveOilFillImage();
            if (oilFillImage != null)
                oilFillImage.fillAmount = Mathf.Clamp01(oilPercent / 100f);
        }

        private void ResolveOilFillImage()
        {
            if (oilFillImage != null)
                return;

            var fillObject = GameObject.Find("LanternFuelFill");
            if (fillObject != null)
                oilFillImage = fillObject.GetComponent<Image>();
        }

        private void ApplyFlicker()
        {
            if (!IsLit || flameLight == null)
                return;

            var danger = GetNearGhostEffect01();
            flameLight.intensity = flameBaseIntensity + Mathf.Sin(Time.time * Mathf.Lerp(5f, 18f, danger)) * Mathf.Lerp(0.06f, flamePulseIntensity, danger);
            flameLight.range = Mathf.Lerp(flameBaseRange, flameDangerRange, danger);
            flameLight.color = Color.Lerp(normalFlameColor, dangerFlameColor, danger);

            if (flameMaterial != null)
                flameMaterial.color = Color.Lerp(new Color(1f, 0.55f, 0.18f), dangerFlameColor, danger);

            ApplyParticleDangerColor(danger);
        }

        private void ApplyNearGhostReaction()
        {
            if (shakeRoot == null)
                return;

            var danger = IsLit ? GetNearGhostEffect01() : 0f;
            if (danger <= 0.001f)
            {
                shakeRoot.localPosition = Vector3.Lerp(shakeRoot.localPosition, baseLocalPosition, Time.deltaTime * 12f);
                shakeRoot.localRotation = Quaternion.Slerp(shakeRoot.localRotation, baseLocalRotation, Time.deltaTime * 12f);
                return;
            }

            var shakeSpeed = Mathf.Lerp(16f, 42f, danger);
            var positionAmount = dangerShakePosition * danger;
            var rotationAmount = dangerShakeRotation * danger;
            var shakeOffset = new Vector3(
                Mathf.PerlinNoise(Time.time * shakeSpeed, 0.13f) - 0.5f,
                Mathf.PerlinNoise(1.71f, Time.time * shakeSpeed) - 0.5f,
                Mathf.PerlinNoise(Time.time * shakeSpeed, 3.29f) - 0.5f) * positionAmount;

            var shakeRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(5.17f, Time.time * shakeSpeed) - 0.5f) * rotationAmount,
                (Mathf.PerlinNoise(Time.time * shakeSpeed, 8.33f) - 0.5f) * rotationAmount,
                (Mathf.PerlinNoise(11.9f, Time.time * shakeSpeed) - 0.5f) * rotationAmount);

            shakeRoot.localPosition = baseLocalPosition + shakeOffset;
            shakeRoot.localRotation = baseLocalRotation * shakeRotation;
        }

        private void ApplyParticleDangerColor(float danger)
        {
            if (flameParticles == null)
                return;

            var warmStart = new Color(1f, 0.86f, 0.25f, 0.95f);
            var warmEnd = new Color(1f, 0.28f, 0.04f, 0.75f);
            var blueStart = new Color(0.5f, 0.95f, 1f, 0.98f);
            var blueEnd = new Color(0.08f, 0.42f, 1f, 0.8f);

            foreach (var particles in flameParticles)
            {
                if (particles == null)
                    continue;

                var main = particles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    Color.Lerp(warmStart, blueStart, danger),
                    Color.Lerp(warmEnd, blueEnd, danger));

                var emission = particles.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                    Mathf.Lerp(18f, 36f, danger),
                    Mathf.Lerp(28f, 52f, danger));

                var noise = particles.noise;
                noise.strength = new ParticleSystem.MinMaxCurve(
                    Mathf.Lerp(0.04f, 0.16f, danger),
                    Mathf.Lerp(0.1f, 0.32f, danger));
                noise.frequency = Mathf.Lerp(7f, 16f, danger);
            }
        }

        private float GetNearGhostEffect01()
        {
            if (ghost == null)
                return 0f;

            var controller = P2GameController.Instance;
            var distance = controller != null
                ? controller.DistanceToPlayer(ghost.transform.position)
                : Vector3.Distance(transform.position, ghost.transform.position);
            var startDistance = Mathf.Max(nearGhostEffectStartDistance, nearGhostFullEffectDistance + 0.01f);
            return Mathf.InverseLerp(startDistance, nearGhostFullEffectDistance, distance);
        }

        private void TrackUnlitDanger()
        {
            if (IsLit || ghost == null || !ghost.IsAwakened)
            {
                unlitNearGhostTimer = 0f;
                return;
            }

            if (P2GameController.Instance.DistanceToPlayer(ghost.transform.position) > dangerDistance)
            {
                unlitNearGhostTimer = 0f;
                return;
            }

            unlitNearGhostTimer += Time.deltaTime;
            if (unlitNearGhostTimer >= directAttackSeconds)
            {
                unlitNearGhostTimer = -999f;
                ghost.ForceChase();
                P2GameController.Instance.ShowPrompt("Bóng tối không che được lâu nữa.");
            }
        }

        private void OnDrawGizmos()
        {
            if (!debugNearGhostEffectZone)
                return;

            var origin = transform.position;
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.55f);
            Gizmos.DrawWireSphere(origin, nearGhostEffectStartDistance);
            Gizmos.color = new Color(0.05f, 0.35f, 1f, 0.8f);
            Gizmos.DrawWireSphere(origin, nearGhostFullEffectDistance);
            if (ghost != null)
            {
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.25f);
                Gizmos.DrawLine(origin, ghost.transform.position);
            }
        }
    }

    public sealed class P2GhostController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform quietPatrolRoot;
        [SerializeField] private Transform awakenedPatrolRoot;
        [SerializeField] private bool autoCollectWaypointsFromChildren = true;
        [SerializeField] private bool pingPongPatrol = true;
        [SerializeField] private Transform[] quietPatrolWaypoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] awakenedWaypoints = Array.Empty<Transform>();
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private float quietSpeed = 1.5f;
        [SerializeField] private float awakenedSpeed = 2f;
        [SerializeField] private float chaseSpeed = 3.4f;
        [SerializeField] private float catchDistance = 1.25f;
        [SerializeField, Min(0.05f)] private float waypointReachDistance = 0.35f;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2.5f;
        [SerializeField] private float lineCooldown = 10f;

        public bool IsAwakened { get; private set; }

        private int waypointIndex;
        private int waypointDirection = 1;
        private bool chase;
        private float nextLineTime;
        private Vector3 investigationPoint;
        private bool hasInvestigationPoint;
        private Vector3 lastDestination;
        private bool hasDestination;
        private int quietPatrolChildCount = -1;
        private int awakenedPatrolChildCount = -1;

        private void Awake()
        {
            ResolveAgent();
            RefreshWaypointsFromRoots(true);
        }

        private void Start()
        {
            if (player == null)
            {
                var controller = FindFirstObjectByType<P2FirstPersonController>();
                if (controller != null)
                    player = controller.transform;
            }

            ResolveAgent();
            RefreshWaypointsFromRoots(true);
        }

        private void Update()
        {
            RefreshWaypointsFromRoots(false);

            if (player == null)
                return;

            if (chase)
            {
                MoveTowards(player.position, chaseSpeed, catchDistance);
                if (Vector3.Distance(transform.position, player.position) <= catchDistance)
                    P2GameController.Instance?.StartEndingSequence();
                return;
            }

            var waypoints = IsAwakened ? awakenedWaypoints : quietPatrolWaypoints;
            if (hasInvestigationPoint)
            {
                if (MoveTowards(investigationPoint, awakenedSpeed, waypointReachDistance))
                    hasInvestigationPoint = false;
                return;
            }

            if (waypoints == null || waypoints.Length == 0)
                return;

            waypointIndex = Mathf.Clamp(waypointIndex, 0, waypoints.Length - 1);
            var target = waypoints[waypointIndex];
            if (target == null)
            {
                AdvanceWaypoint(waypoints);
                return;
            }

            if (MoveTowards(target.position, IsAwakened ? awakenedSpeed : quietSpeed, waypointReachDistance))
                AdvanceWaypoint(waypoints);

            if (IsAwakened && Time.time >= nextLineTime && Vector3.Distance(transform.position, player.position) < 10f)
            {
                nextLineTime = Time.time + lineCooldown;
                P2GameController.Instance?.PlayGhostLine();
            }
        }

        public void Configure(Transform targetPlayer, Transform[] quietWaypoints, Transform[] fullWaypoints)
        {
            player = targetPlayer;
            quietPatrolWaypoints = quietWaypoints;
            awakenedWaypoints = fullWaypoints;
        }

        public void Awaken()
        {
            IsAwakened = true;
            waypointIndex = 0;
            waypointDirection = 1;
            ClearDestination();
            nextLineTime = Time.time + 1f;
        }

        public void Investigate(Vector3 position)
        {
            if (!IsAwakened)
                return;

            investigationPoint = position;
            hasInvestigationPoint = true;
        }

        public void ForceChase()
        {
            if (!IsAwakened)
                Awaken();
            chase = true;
            ClearDestination();
        }

        private void RefreshWaypointsFromRoots(bool force)
        {
            if (!autoCollectWaypointsFromChildren)
                return;

            if (quietPatrolRoot != null && (force || quietPatrolChildCount != quietPatrolRoot.childCount))
            {
                quietPatrolWaypoints = CollectDirectChildren(quietPatrolRoot);
                quietPatrolChildCount = quietPatrolRoot.childCount;
                waypointIndex = Mathf.Clamp(waypointIndex, 0, Mathf.Max(0, quietPatrolWaypoints.Length - 1));
            }

            if (awakenedPatrolRoot != null && (force || awakenedPatrolChildCount != awakenedPatrolRoot.childCount))
            {
                awakenedWaypoints = CollectDirectChildren(awakenedPatrolRoot);
                awakenedPatrolChildCount = awakenedPatrolRoot.childCount;
                waypointIndex = Mathf.Clamp(waypointIndex, 0, Mathf.Max(0, awakenedWaypoints.Length - 1));
            }
        }

        private static Transform[] CollectDirectChildren(Transform root)
        {
            if (root == null || root.childCount == 0)
                return Array.Empty<Transform>();

            var waypoints = new List<Transform>(root.childCount);
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    waypoints.Add(child);
            }

            return waypoints.ToArray();
        }

        private void AdvanceWaypoint(Transform[] waypoints)
        {
            if (waypoints == null || waypoints.Length <= 1)
                return;

            if (!pingPongPatrol)
            {
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                ClearDestination();
                return;
            }

            if (waypointIndex >= waypoints.Length - 1)
                waypointDirection = -1;
            else if (waypointIndex <= 0)
                waypointDirection = 1;

            waypointIndex = Mathf.Clamp(waypointIndex + waypointDirection, 0, waypoints.Length - 1);
            ClearDestination();
        }

        private bool MoveTowards(Vector3 target, float speed, float reachDistance)
        {
            ResolveAgent();
            if (agent == null || !agent.enabled)
                return false;

            if (!agent.isOnNavMesh && !TryWarpAgentToNavMesh())
                return false;

            if (!NavMesh.SamplePosition(target, out var sampledTarget, navMeshSampleRadius, NavMesh.AllAreas))
                return false;

            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = Mathf.Min(0.1f, reachDistance * 0.5f);

            var destination = sampledTarget.position;
            if (!hasDestination || Vector3.Distance(lastDestination, destination) > 0.1f)
            {
                if (!agent.SetDestination(destination))
                    return false;

                lastDestination = destination;
                hasDestination = true;
            }

            var velocity = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity;
            if (velocity.sqrMagnitude > 0.01f)
            {
                var flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatVelocity.normalized, Vector3.up), Time.deltaTime * 5f);
            }

            return !agent.pathPending && agent.remainingDistance <= reachDistance;
        }

        private void ResolveAgent()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();

            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();

            agent.updateRotation = false;
        }

        private bool TryWarpAgentToNavMesh()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                return agent.Warp(hit.position);

            return false;
        }

        private void ClearDestination()
        {
            hasDestination = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
        }
    }

    public sealed class P2MirrorBreakable : MonoBehaviour
    {
        private static readonly List<P2MirrorBreakable> Mirrors = new List<P2MirrorBreakable>();

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material brokenMaterial;
        [SerializeField] private GameObject brokenShardCluster;

        private void OnEnable()
        {
            Mirrors.Add(this);
        }

        private void OnDisable()
        {
            Mirrors.Remove(this);
        }

        public void Configure(Renderer rendererToBreak, Material crackedMaterial, GameObject shards = null)
        {
            targetRenderer = rendererToBreak;
            brokenMaterial = crackedMaterial;
            brokenShardCluster = shards;
            if (brokenShardCluster != null)
                brokenShardCluster.SetActive(false);
        }

        public static void BreakAll()
        {
            for (var i = Mirrors.Count - 1; i >= 0; i--)
            {
                if (Mirrors[i] != null)
                    Mirrors[i].Break();
            }
        }

        private void Break()
        {
            if (targetRenderer != null && brokenMaterial != null)
                targetRenderer.sharedMaterial = brokenMaterial;
            if (brokenShardCluster != null)
                brokenShardCluster.SetActive(true);
        }
    }

    public sealed class P2WallKnockPuzzle : MonoBehaviour
    {
        [SerializeField] private int hollowPanelIndex = 2;
        [SerializeField] private GameObject hiddenCavity;
        [SerializeField] private P2Interactable[] panels = Array.Empty<P2Interactable>();
        private bool opened;

        public void Configure(int targetPanel, GameObject cavity, P2Interactable[] wallPanels)
        {
            hollowPanelIndex = targetPanel;
            hiddenCavity = cavity;
            panels = wallPanels;
        }

        public void Knock(P2Interactable panel)
        {
            if (opened)
                return;

            var index = Array.IndexOf(panels, panel);
            var hollow = index == hollowPanelIndex;
            P2GameController.Instance?.PlayKnock(hollow);

            if (!hollow)
            {
                P2GameController.Instance?.ShowPrompt("Tiếng gõ đặc, không phải chỗ này.");
                panel.ResetOneShot();
                return;
            }

            opened = true;
            if (hiddenCavity != null)
                hiddenCavity.SetActive(true);
            P2GameController.Instance?.RegisterWallOpened(hiddenCavity);
        }
    }

    public sealed class P2GlassShardField : MonoBehaviour
    {
        private void OnTriggerStay(Collider other)
        {
            var player = other.GetComponentInParent<P2FirstPersonController>();
            if (player == null || !player.IsRunning)
                return;

            P2GameController.Instance?.PlayShardNoise(transform.position);
        }
    }
}
