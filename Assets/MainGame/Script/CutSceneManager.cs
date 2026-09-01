using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using FpsHorrorKit;

public sealed class CutSceneManager : MonoBehaviour
{
    private const string DefaultIntroWindowEntryCutSceneId = "intro_window_entry";

    [Header("Sequences")]
    [SerializeField] private string introCutSceneId = "intro";
    [SerializeField] private string introWindowEntryCutSceneId = DefaultIntroWindowEntryCutSceneId;
    [SerializeField] private bool autoResolveSequences = true;
    [SerializeField] private List<CutSceneSequence> sequences = new();

    [Header("Actors")]
    [SerializeField] private FpsController playerController;
    [SerializeField] private Transform playerRoot;

    [Header("Cameras")]
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private CinemachineCamera gameplayVirtualCamera;

    [Header("UI")]
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Audio")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Speech Typewriter")]
    [SerializeField] private bool showOnlyQuotedSpeech = true;
    [SerializeField, Min(1f)] private float typewriterCharactersPerSecond = 34f;
    [SerializeField] private AudioSource typewriterClickSource;
    [SerializeField] private AudioClip typewriterClickClip;
    [SerializeField, Range(0f, 1f)] private float typewriterClickVolume = 0.45f;
    [SerializeField, Min(1f)] private float typewriterClickVolumeDivider = 6f;
    [SerializeField, Min(0f)] private float skipInputCooldown = 0.45f;

    [Header("Navigation")]
    [SerializeField, Min(0.1f)] private float cutSceneMoveSpeed = 2.2f;
    [SerializeField, Min(1f)] private float cutSceneTurnSpeed = 150f;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2.5f;
    [SerializeField, Min(0.05f)] private float cornerReachDistance = 0.18f;

    [Header("Camera Timing")]
    [SerializeField, Min(0.1f)] private float cameraSmoothTime = 0.22f;
    [SerializeField] private bool teleportCameraOnCutSceneStart = true;

    [Header("Default Camera Heights")]
    [SerializeField] private Vector2 overheadHeight = new Vector2(10f, 5.2f);
    [SerializeField] private Vector2 descendBehindHeight = new Vector2(6.2f, 2f);
    [SerializeField] private float behindShoulderHeight = 2.1f;
    [SerializeField] private float signCloseHeight = 1.75f;
    [SerializeField] private float windowInspectHeight = 1.8f;
    [SerializeField] private float interiorSettleHeight = 1.65f;

    private readonly HashSet<string> playedSequenceIds = new();
    private Coroutine runningCutScene;
    private CutSceneSequence runningSequence;
    private bool previousBrainEnabled;
    private bool isPaused;
    private bool voicePausedByPauseMenu;
    private Vector3 cameraVelocity;
    private NavMeshPath navPath;
    private AudioClip generatedTypewriterClickClip;
    private float nextSkipAllowedTime;
    private bool waitForSkipRelease;
    private bool skipConsumedForPoint;

    public static bool SuppressSpaceSkipInput { get; set; }

    public bool IsPlaying => runningCutScene != null;
    public bool IsPlayingIntro => IsPlaying && runningSequence != null && IsIntro(runningSequence);

    public void PlayIntro(GameController gameController)
    {
        Play(introCutSceneId, gameController);
    }

    public void Play(string cutSceneId, GameController gameController = null)
    {
        if (runningCutScene != null)
            return;

        ResolveReferences();
        var sequence = FindSequence(cutSceneId);
        if (sequence == null)
        {
            Debug.LogWarning($"CutSceneManager cannot play '{cutSceneId}' because the sequence was not found.");
            gameController?.StartGameplay();
            return;
        }

        if (sequence.PlayOnce && playedSequenceIds.Contains(sequence.CutSceneId))
        {
            gameController?.StartGameplay();
            return;
        }

        if (playerRoot == null || playerController == null || cinematicCamera == null)
        {
            Debug.LogWarning("CutSceneManager cannot start because required player/camera references are missing.");
            gameController?.StartGameplay();
            return;
        }

        runningCutScene = StartCoroutine(PlayRoutine(sequence, gameController));
    }

    public void StopCurrent(GameController gameController = null)
    {
        if (runningCutScene != null)
            StopCoroutine(runningCutScene);

        ExitCutScene(runningSequence, gameController);
        runningSequence = null;
        runningCutScene = null;
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;

        if (voiceSource == null)
            return;

        if (paused)
        {
            voicePausedByPauseMenu = voiceSource.isPlaying;
            if (voicePausedByPauseMenu)
                voiceSource.Pause();
        }
        else if (voicePausedByPauseMenu)
        {
            voiceSource.UnPause();
            voicePausedByPauseMenu = false;
        }
    }

    private IEnumerator PlayRoutine(CutSceneSequence sequence, GameController gameController)
    {
        runningSequence = sequence;
        playedSequenceIds.Add(sequence.CutSceneId);
        cameraVelocity = Vector3.zero;
        EnterCutScene(gameController);

        if (sequence.TeleportToStartPoint && sequence.StartPoint != null)
            MovePlayerInstant(sequence.StartPoint);

        var firstPoint = FirstPlayablePoint(sequence);
        if (teleportCameraOnCutSceneStart && sequence.TeleportCameraToFirstShot && firstPoint != null)
            SetCameraInstant(firstPoint, 0f);

        foreach (var point in sequence.Points)
            yield return PlayPoint(sequence, point);

        yield return new WaitForSeconds(0.2f);
        ExitCutScene(sequence, gameController);
        runningSequence = null;
        runningCutScene = null;
    }

    private IEnumerator PlayPoint(CutSceneSequence sequence, CutScenePoint point)
    {
        if (point == null)
            yield break;

        skipConsumedForPoint = false;
        cameraVelocity = Vector3.zero;
        ResolveDialogue(point, out var text, out var audioClip, out var fallbackDuration);
        var speechText = ExtractSpeechText(text);
        var hasSpeechText = !string.IsNullOrWhiteSpace(speechText);
        SetSpeechVisible(hasSpeechText);
        SetSubtitle(string.Empty);
        PlayVoice(audioClip);

        var speechDuration = audioClip != null ? audioClip.length : fallbackDuration;
        speechDuration = Mathf.Max(0.5f, speechDuration);

        var pathCorners = point.moveToPoint && point.point != null ? BuildPath(point.point.position) : null;
        var cornerIndex = 1;
        var speechElapsed = 0f;
        var typewriterElapsed = 0f;
        var visibleCharacters = 0;
        var movementDone = pathCorners == null || pathCorners.Length < 2;
        var typewriterDone = !hasSpeechText;
        var speechDone = string.IsNullOrWhiteSpace(text) && audioClip == null;

        while (!speechDone || !movementDone)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            if (!speechDone && movementDone && !skipConsumedForPoint && TryConsumeSkipPressed())
            {
                skipConsumedForPoint = true;
                speechDone = true;
                typewriterDone = true;
                speechElapsed = speechDuration;
                if (hasSpeechText)
                    SetSubtitle(speechText);
                StopVoice();
            }

            if (!typewriterDone)
                typewriterDone = TickTypewriter(speechText, ref visibleCharacters, ref typewriterElapsed);

            if (!speechDone)
            {
                speechElapsed += Time.deltaTime;
                speechDone = speechElapsed >= speechDuration && typewriterDone;
            }

            if (!movementDone)
                movementDone = MoveAlongPath(pathCorners, ref cornerIndex, point);

            UpdateCamera(point, Mathf.Clamp01(speechElapsed / speechDuration));
            yield return null;
        }

        playerController.StopCutSceneMovement();

        if (point.point != null)
            yield return RotatePlayerToFlat(point.point.position + point.point.forward, point);

        UpdateCamera(point, 1f);
        StopVoice();
        SetSubtitle(string.Empty);
        SetSpeechVisible(false);

        if (point.waitAfter > 0f)
            yield return new WaitForSeconds(point.waitAfter);
    }

    private void EnterCutScene(GameController gameController)
    {
        gameController?.SetGameState(GameController.GameState.Cutscene);

        FpsAssetsInputs.Instance?.ClearGameplayInput();
        nextSkipAllowedTime = Time.unscaledTime + skipInputCooldown;
        waitForSkipRelease = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        skipConsumedForPoint = false;
        cameraVelocity = Vector3.zero;

        playerController.isCutScene = true;
        playerController.isInteracting = true;
        playerController.StopCutSceneMovement();
        ItemUsageSystem.Instance?.ForceFlashlightOn();

        if (cinemachineBrain != null)
        {
            previousBrainEnabled = cinemachineBrain.enabled;
            cinemachineBrain.enabled = false;
        }

        if (gameplayVirtualCamera != null)
            gameplayVirtualCamera.enabled = false;

        SetSubtitle(string.Empty);
        SetSpeechVisible(false);
        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(true);
    }

    private void ExitCutScene(CutSceneSequence sequence, GameController gameController)
    {
        StopVoice();
        SetSubtitle(string.Empty);

        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);

        if (gameplayVirtualCamera != null)
            gameplayVirtualCamera.enabled = true;

        if (cinemachineBrain != null)
            cinemachineBrain.enabled = previousBrainEnabled;

        if (playerController != null)
        {
            playerController.isCutScene = false;
            playerController.isInteracting = false;
            playerController.StopCutSceneMovement();
        }

        FpsAssetsInputs.Instance?.ClearGameplayInput();
        cameraVelocity = Vector3.zero;
        waitForSkipRelease = false;
        skipConsumedForPoint = false;
        if (sequence != null && IsIntroWindowEntry(sequence))
        {
            ChapterOneCheckpointManager.Instance?.MarkWindowCutsceneCompleted();
            gameController?.SetChapterPhase(GameController.ChapterPhase.EnterHouse);
        }

        gameController?.StartGameplay();
    }

    private void ResolveDialogue(
        CutScenePoint point,
        out string text,
        out AudioClip audioClip,
        out float fallbackDuration)
    {
        text = point.overrideText;
        audioClip = point.overrideAudioClip;
        if (audioClip == null)
            audioClip = AudioManager.EnsureInstance().GetIntroVoiceClip(point.dialogueId);
        fallbackDuration = point.overrideFallbackDuration;
    }

    private Vector3[] BuildPath(Vector3 targetPosition)
    {
        navPath ??= new NavMeshPath();

        bool sampledStart = TrySampleNavMesh(playerRoot.position, out var start);
        bool sampledDestination = TrySampleNavMesh(targetPosition, out var destination);
        if (!sampledDestination)
        {
            if (!TryFindNearestNavMeshPoint(targetPosition, out destination))
                return IsDirectCutSceneSegmentClear(playerRoot.position, targetPosition)
                    ? new[] { playerRoot.position, targetPosition }
                    : System.Array.Empty<Vector3>();
        }

        if (!sampledStart)
        {
            if (!TryFindNearestNavMeshPoint(playerRoot.position, out start)
                || !IsDirectCutSceneSegmentClear(playerRoot.position, start))
            {
                return System.Array.Empty<Vector3>();
            }
        }

        if (!NavMesh.CalculatePath(start, destination, NavMesh.AllAreas, navPath) || navPath.corners.Length < 2)
            return IsDirectCutSceneSegmentClear(playerRoot.position, destination)
                ? new[] { playerRoot.position, destination }
                : System.Array.Empty<Vector3>();

        if (sampledStart)
            return navPath.corners;

        var corners = new List<Vector3>(navPath.corners.Length + 1) { playerRoot.position };
        corners.AddRange(navPath.corners);
        return corners.ToArray();
    }

    private bool MoveAlongPath(Vector3[] corners, ref int cornerIndex, CutScenePoint point)
    {
        if (corners == null || cornerIndex >= corners.Length)
            return true;

        var toCorner = corners[cornerIndex] - playerRoot.position;
        toCorner.y = 0f;

        if (toCorner.magnitude <= cornerReachDistance)
        {
            cornerIndex++;
            if (cornerIndex >= corners.Length)
                return true;

            toCorner = corners[cornerIndex] - playerRoot.position;
            toCorner.y = 0f;
        }

        var moveSpeed = point != null && point.moveSpeedOverride > 0f ? point.moveSpeedOverride : cutSceneMoveSpeed;
        var turnSpeed = point != null && point.turnSpeedOverride > 0f ? point.turnSpeedOverride : cutSceneTurnSpeed;
        playerController.MoveCutScene(toCorner.normalized, moveSpeed, true, turnSpeed);
        return false;
    }

    private void MovePlayerInstant(Transform point)
    {
        if (point == null)
            return;

        playerController.TeleportCutScene(point);
    }

    private void UpdateCamera(CutScenePoint point, float t)
    {
        if (cinematicCamera == null || playerRoot == null || point == null)
            return;

        var targetPosition = GetCameraPosition(point, t);
        var lookAt = GetCameraLookAt(point, t);
        var smoothTime = point.cameraSmoothTimeOverride > 0f ? point.cameraSmoothTimeOverride : cameraSmoothTime;
        cinematicCamera.transform.position = Vector3.SmoothDamp(cinematicCamera.transform.position, targetPosition, ref cameraVelocity, smoothTime);

        var direction = lookAt - cinematicCamera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            cinematicCamera.transform.rotation = Quaternion.Slerp(cinematicCamera.transform.rotation, targetRotation, Time.deltaTime * 5.2f);
        }
    }

    private void SetCameraInstant(CutScenePoint point, float t)
    {
        if (cinematicCamera == null || playerRoot == null || point == null)
            return;

        var targetPosition = GetCameraPosition(point, t);
        var lookAt = GetCameraLookAt(point, t);
        cinematicCamera.transform.position = targetPosition;
        cameraVelocity = Vector3.zero;

        var direction = lookAt - cinematicCamera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
            cinematicCamera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Vector3 GetCameraPosition(CutScenePoint point, float t)
    {
        if (point.cameraPositionOverride != null)
            return point.cameraPositionOverride.position;

        var basePosition = playerRoot.position;
        var forward = playerRoot.forward;
        var right = playerRoot.right;

        if (point.useCustomCameraOffset)
            return basePosition + right * point.customCameraOffset.x + Vector3.up * point.customCameraOffset.y + forward * point.customCameraOffset.z;

        return point.cameraShot switch
        {
            CutSceneCameraShot.OverheadFollow => basePosition - forward * Mathf.Lerp(8f, 5f, t) + Vector3.up * Mathf.Lerp(overheadHeight.x, overheadHeight.y, t) + right * Mathf.Lerp(-2f, -0.8f, t),
            CutSceneCameraShot.DescendBehind => basePosition - forward * Mathf.Lerp(6.5f, 2.6f, t) + Vector3.up * Mathf.Lerp(descendBehindHeight.x, descendBehindHeight.y, t) + right * 0.5f,
            CutSceneCameraShot.SignClose => basePosition - forward * 2.3f + Vector3.up * signCloseHeight + right * -0.75f,
            CutSceneCameraShot.WindowInspect => basePosition - forward * 2.2f + Vector3.up * windowInspectHeight + right * 0.85f,
            CutSceneCameraShot.InteriorSettle => basePosition - forward * 2.0f + Vector3.up * interiorSettleHeight,
            _ => basePosition - forward * 3.2f + Vector3.up * behindShoulderHeight + right * 0.45f
        };
    }

    private Vector3 GetCameraLookAt(CutScenePoint point, float t)
    {
        if (point.cameraLookAtOverride != null)
            return point.cameraLookAtOverride.position + Vector3.up * Mathf.Lerp(1.15f, 1.55f, t);

        var shouldLookAtPlayer = point.cameraLookAtPlayer
            || point.cameraShot == CutSceneCameraShot.OverheadFollow
            || point.cameraShot == CutSceneCameraShot.DescendBehind
            || point.cameraShot == CutSceneCameraShot.BehindShoulder;

        var position = shouldLookAtPlayer || point.point == null ? playerRoot.position : point.point.position;
        return position + Vector3.up * point.cameraLookHeight;
    }

    private void SetSubtitle(string text)
    {
        if (subtitleText != null)
            subtitleText.text = text ?? string.Empty;
    }

    private void SetSpeechVisible(bool visible)
    {
        if (subtitlePanel != null && subtitlePanel.activeSelf != visible)
            subtitlePanel.SetActive(visible);
    }

    private bool TickTypewriter(string text, ref int visibleCharacters, ref float elapsed)
    {
        if (subtitleText == null || string.IsNullOrEmpty(text))
            return true;

        var interval = 1f / Mathf.Max(1f, typewriterCharactersPerSecond);
        elapsed += Time.deltaTime;

        while (elapsed >= interval && visibleCharacters < text.Length)
        {
            elapsed -= interval;
            visibleCharacters++;
            PlayTypewriterClick();
        }

        if (visibleCharacters > 0)
            subtitleText.text = text.Substring(0, visibleCharacters);

        return visibleCharacters >= text.Length;
    }

    private string ExtractSpeechText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (!showOnlyQuotedSpeech)
            return text.Trim();

        var result = new StringBuilder();
        var current = new StringBuilder();
        var insideQuote = false;

        foreach (var character in text)
        {
            if (IsQuote(character))
            {
                if (insideQuote && current.Length > 0)
                {
                    if (result.Length > 0)
                        result.AppendLine();
                    result.Append(current.ToString().Trim());
                    current.Clear();
                }

                insideQuote = !insideQuote;
                continue;
            }

            if (insideQuote)
                current.Append(character);
        }

        return result.ToString().Trim();
    }

    private static bool IsQuote(char character)
    {
        return character == '"' || character == '“' || character == '”';
    }

    private void PlayTypewriterClick()
    {
        var source = typewriterClickSource != null ? typewriterClickSource : voiceSource;
        if (source == null)
            return;

        var clip = typewriterClickClip != null ? typewriterClickClip : GetGeneratedTypewriterClickClip();
        if (clip != null)
            source.PlayOneShot(clip, typewriterClickVolume / Mathf.Max(1f, typewriterClickVolumeDivider));
    }

    private AudioClip GetGeneratedTypewriterClickClip()
    {
        if (generatedTypewriterClickClip != null)
            return generatedTypewriterClickClip;

        const int sampleRate = 22050;
        const float length = 0.028f;
        var sampleCount = Mathf.CeilToInt(sampleRate * length);
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)sampleRate;
            var envelope = Mathf.Exp(-t * 85f);
            var click = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.55f;
            var scrape = Mathf.Sin(2f * Mathf.PI * 760f * t) * 0.28f;
            samples[i] = (click + scrape) * envelope;
        }

        generatedTypewriterClickClip = AudioClip.Create("GeneratedTypewriterClick", sampleCount, 1, sampleRate, false);
        generatedTypewriterClickClip.SetData(samples, 0);
        return generatedTypewriterClickClip;
    }

    private void PlayVoice(AudioClip clip)
    {
        if (voiceSource == null)
            return;

        voiceSource.Stop();
        voiceSource.clip = clip;
        if (clip != null)
        {
            AudioManager.Instance?.BlockGameplayAmbience(clip.length);
            voiceSource.Play();
        }
    }

    private void StopVoice()
    {
        if (voiceSource != null)
            voiceSource.Stop();
        voicePausedByPauseMenu = false;
    }

    private bool TrySampleNavMesh(Vector3 position, out Vector3 sampledPosition)
    {
        if (NavMesh.SamplePosition(position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            sampledPosition = hit.position;
            return true;
        }

        sampledPosition = position;
        return false;
    }

    private bool TryFindNearestNavMeshPoint(Vector3 position, out Vector3 sampledPosition)
    {
        float baseRadius = Mathf.Max(navMeshSampleRadius, 0.5f);
        float maxRadius = baseRadius * 8f;

        for (float radius = baseRadius; radius <= maxRadius; radius += baseRadius)
        {
            if (NavMesh.SamplePosition(position, out var hit, radius, NavMesh.AllAreas))
            {
                sampledPosition = hit.position;
                return true;
            }
        }

        sampledPosition = position;
        return false;
    }

    private static bool IsDirectCutSceneSegmentClear(Vector3 from, Vector3 to)
    {
        Vector3 origin = from + Vector3.up * 0.85f;
        Vector3 target = to + Vector3.up * 0.85f;
        return !Physics.Linecast(origin, target, ~0, QueryTriggerInteraction.Ignore);
    }

    private CutSceneSequence FindSequence(string cutSceneId)
    {
        if (autoResolveSequences)
            ResolveSequences();

        foreach (var sequence in sequences)
        {
            if (sequence != null && string.Equals(sequence.CutSceneId, cutSceneId, System.StringComparison.OrdinalIgnoreCase))
                return sequence;
        }

        return null;
    }

    private void ResolveSequences()
    {
        sequences.RemoveAll(sequence => sequence == null);
        foreach (var sequence in GetComponentsInChildren<CutSceneSequence>(true))
        {
            if (!sequences.Contains(sequence))
                sequences.Add(sequence);
        }
    }

    private bool IsIntro(CutSceneSequence sequence)
    {
        return string.Equals(sequence.CutSceneId, introCutSceneId, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsIntroWindowEntry(CutSceneSequence sequence)
    {
        return string.Equals(sequence.CutSceneId, introWindowEntryCutSceneId, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(sequence.CutSceneId, DefaultIntroWindowEntryCutSceneId, System.StringComparison.OrdinalIgnoreCase);
    }

    private static CutScenePoint FirstPlayablePoint(CutSceneSequence sequence)
    {
        foreach (var point in sequence.Points)
        {
            if (point != null)
                return point;
        }

        return null;
    }

    private bool TryConsumeSkipPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (SuppressSpaceSkipInput)
        {
            if (keyboard.spaceKey.isPressed)
            {
                waitForSkipRelease = true;
                FpsAssetsInputs.Instance?.ClearGameplayInput();
            }

            return false;
        }

        if (waitForSkipRelease)
        {
            if (!keyboard.spaceKey.isPressed)
                waitForSkipRelease = false;
            return false;
        }

        if (Time.unscaledTime < nextSkipAllowedTime || !keyboard.spaceKey.wasPressedThisFrame)
            return false;

        nextSkipAllowedTime = Time.unscaledTime + skipInputCooldown;
        waitForSkipRelease = true;
        FpsAssetsInputs.Instance?.ClearGameplayInput();
        return true;
    }

    private IEnumerator RotatePlayerToFlat(Vector3 targetPosition, CutScenePoint cameraPoint)
    {
        var direction = targetPosition - playerRoot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
            yield break;

        while (true)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            if (playerController.RotateCutSceneTowards(direction, cutSceneTurnSpeed))
                yield break;

            UpdateCamera(cameraPoint, 1f);
            yield return null;
        }
    }

    private void ResolveReferences()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<FpsController>();
        if (playerRoot == null && playerController != null)
            playerRoot = playerController.transform;
        if (cinematicCamera == null)
            cinematicCamera = Camera.main;
        if (cinemachineBrain == null && cinematicCamera != null)
            cinemachineBrain = cinematicCamera.GetComponent<CinemachineBrain>();
        if (gameplayVirtualCamera == null && playerController != null)
            gameplayVirtualCamera = playerController.virtualCamera;
        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        if (typewriterClickSource == null)
            typewriterClickSource = voiceSource;
        if (subtitlePanel == null)
        {
            var narration = FindSceneObjectByName("NarrationPanel");
            if (narration != null)
                subtitlePanel = narration;
        }
        if (subtitleText == null && subtitlePanel != null)
            subtitleText = subtitlePanel.GetComponentInChildren<TMP_Text>(true);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName)
                return transform.gameObject;
        }

        return null;
    }
}
