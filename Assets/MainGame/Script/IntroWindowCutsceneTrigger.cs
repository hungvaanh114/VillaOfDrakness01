using FpsHorrorKit;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class IntroWindowCutsceneTrigger : MonoBehaviour
{
    [SerializeField] private CutSceneManager cutSceneManager;
    [SerializeField] private string cutSceneId = "intro_window_entry";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool requireIntroPhase = true;

    private bool hasTriggered;
    private BoxCollider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        triggerCollider.isTrigger = true;

        if (cutSceneManager == null)
            cutSceneManager = FindFirstObjectByType<CutSceneManager>(FindObjectsInactive.Include);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPlayWindowCutscene(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPlayWindowCutscene(other);
    }

    private void TryPlayWindowCutscene(Collider other)
    {
        if (hasTriggered && triggerOnce)
            return;

        if (other.GetComponentInParent<FpsController>() == null)
            return;

        var controller = GameController.Instance;
        if (controller != null)
        {
            if (controller.currentGameState != GameController.GameState.Gameplay)
                return;
            if (requireIntroPhase && controller.currentChapterPhase != GameController.ChapterPhase.Intro)
                return;
        }

        if (cutSceneManager == null)
            cutSceneManager = FindFirstObjectByType<CutSceneManager>(FindObjectsInactive.Include);
        if (cutSceneManager == null || string.IsNullOrWhiteSpace(cutSceneId))
            return;

        hasTriggered = true;
        cutSceneManager.Play(cutSceneId, controller);
    }
}
