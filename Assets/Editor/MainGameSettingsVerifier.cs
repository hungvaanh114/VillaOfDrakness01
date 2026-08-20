using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainGameSettingsVerifier
{
    [MenuItem("Tools/MainGame/Verify Game Settings UI")]
    public static void Verify()
    {
        var names = new[] { "QualityDropdown", "ShadowsDropdown", "FogDropdown", "FpsDropdown" };
        var found = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene == EditorSceneManager.GetActiveScene())
            .Where(t => names.Contains(t.name))
            .Select(t => t.name)
            .ToArray();

        if (found.Length > 0)
        {
            Debug.LogError("Game settings UI still has removed controls: " + string.Join(", ", found));
            return;
        }

        Debug.Log("Game settings UI verified. Removed controls are not present.");
    }

    [MenuItem("Tools/MainGame/Verify Main Game HUD")]
    public static void VerifyHud()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        var forbidden = new[] { "ChapterTitleText", "CameraPanel", "InventoryBar", "PulseLine", "CornerTopLeft", "CornerTopRight", "CornerBottomLeft", "CornerBottomRight" };
        var required = new[] { "ObjectivePanel", "FlashlightPanel", "InteractPrompt", "NarrationPanel", "NarrationText" };

        var transforms = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene == activeScene)
            .ToArray();

        var foundForbidden = transforms
            .Where(t => forbidden.Contains(t.name))
            .Select(t => t.name)
            .Distinct()
            .ToArray();

        if (foundForbidden.Length > 0)
        {
            Debug.LogError("Main game HUD still has old controls: " + string.Join(", ", foundForbidden));
            return;
        }

        var missing = required
            .Where(name => transforms.All(t => t.name != name))
            .ToArray();

        if (missing.Length > 0)
        {
            Debug.LogError("Main game HUD is missing required controls: " + string.Join(", ", missing));
            return;
        }

        var interactPrompt = transforms.FirstOrDefault(t => t.name == "InteractPrompt");
        if (interactPrompt != null && interactPrompt.gameObject.activeSelf)
        {
            Debug.LogError("InteractPrompt must be inactive by default.");
            return;
        }

        var playerInteract = Resources.FindObjectsOfTypeAll<FpsHorrorKit.PlayerInteract>()
            .FirstOrDefault(i => i.gameObject.scene == activeScene);
        if (playerInteract == null)
        {
            Debug.LogError("PlayerInteract was not found in the active scene.");
            return;
        }

        var serialized = new SerializedObject(playerInteract);
        var promptReference = serialized.FindProperty("higlightObject")?.objectReferenceValue as GameObject;
        var textReference = serialized.FindProperty("interactTextUI")?.objectReferenceValue as TMP_Text;
        if (promptReference == null || promptReference.name != "InteractPrompt" || textReference == null || textReference.name != "InteractText")
        {
            Debug.LogError("PlayerInteract is not wired to InteractPrompt/InteractText.");
            return;
        }

        Debug.Log("Main game HUD verified. Requested HUD controls are present and old controls are removed.");
    }
}
