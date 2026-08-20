using FpsHorrorKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainGamePianoPuzzleSceneUpdater
{
    private const string ScenePath = "Assets/MainGame/Game.unity";

    [MenuItem("Tools/MainGame/Apply Piano Puzzle Notes")]
    [MenuItem("Assets/MainGame/Apply Piano Puzzle Notes")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var puzzle in Object.FindObjectsByType<PianoPuzzle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(puzzle);
            var melody = serialized.FindProperty("requiredMelody");
            if (melody == null || !melody.isArray)
                continue;

            string[] notes = { "C", "D", "E", "F", "G" };
            melody.arraySize = notes.Length;
            for (int i = 0; i < notes.Length; i++)
                melody.GetArrayElementAtIndex(i).stringValue = notes[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(puzzle);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Piano puzzle notes applied: C D E F G.");
    }
}
