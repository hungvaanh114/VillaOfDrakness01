using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EndingCutsceneTrigger : MonoBehaviour
{
    [Header("Testing")]
    [SerializeField] private bool testTrigger;

    [Header("Ending")]
    [SerializeField] private WellEndingTrigger ending;
    [SerializeField] private bool armedOnStart;
    [SerializeField] private bool allowDuringEscapePhase = true;
    [SerializeField] private bool hideMonstersBeforeEnding = true;

    private bool isArmed;
    private bool hasTriggered;
    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        if (armedOnStart)
            Arm();
    }

    private void Update()
    {
        if (!testTrigger)
            return;

        testTrigger = false;
        Arm();
        BeginEnding();
    }

    public void Arm()
    {
        ResolveReferences();
        isArmed = true;
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBeginEnding(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryBeginEnding(other);
    }

    private void TryBeginEnding(Collider other)
    {
        if (!CanTriggerEnding() || hasTriggered)
            return;

        var player = other.GetComponentInParent<FpsHorrorKit.FpsController>();
        if (player == null)
            return;

        ResolveReferences();
        if (ending == null)
            return;

        BeginEnding();
    }

    private void BeginEnding()
    {
        ResolveReferences();
        if (ending == null || hasTriggered)
            return;

        hasTriggered = true;
        HideMonstersForEnding();
        ending.BeginExitDoorEnding(null);
    }

    private bool CanTriggerEnding()
    {
        if (isArmed)
            return true;

        if (!allowDuringEscapePhase)
            return false;

        var controller = GameController.Instance;
        return controller != null && controller.currentChapterPhase >= GameController.ChapterPhase.Escape;
    }

    private void HideMonstersForEnding()
    {
        if (!hideMonstersBeforeEnding)
            return;

        var monsters = FindObjectsByType<MonsterAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] == null)
                continue;

            monsters[i].DisableHunt(true);
            monsters[i].SetMeshVisible(false);
        }
    }

    private void ResolveReferences()
    {
        if (ending == null)
        {
            var wellObject = GameObject.Find("Gieng") ?? GameObject.Find("Well");
            ending = wellObject != null
                ? wellObject.GetComponent<WellEndingTrigger>()
                : FindFirstObjectByType<WellEndingTrigger>(FindObjectsInactive.Include);
        }
    }
}
