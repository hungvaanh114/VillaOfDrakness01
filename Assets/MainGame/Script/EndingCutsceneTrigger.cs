using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EndingCutsceneTrigger : MonoBehaviour
{
    [Header("Testing")]
    [SerializeField] private bool testTrigger;

    [Header("Ending")]
    [SerializeField] private WellEndingTrigger ending;
    [SerializeField] private FpsHorrorKit.GramophoneTapePlayer gramophoneTapePlayer;
    [SerializeField] private bool armedOnStart;

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
        if (!isArmed || hasTriggered)
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
        ending.BeginExitDoorEnding(gramophoneTapePlayer != null ? gramophoneTapePlayer.transform : null);
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

        if (gramophoneTapePlayer == null)
            gramophoneTapePlayer = FindFirstObjectByType<FpsHorrorKit.GramophoneTapePlayer>(FindObjectsInactive.Include);
    }
}
