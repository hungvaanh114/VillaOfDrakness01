using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerAvatarRigFixer
{
    private static readonly string[] ScenePaths =
    {
        "Assets/MainGame/Game.unity",
        "Assets/_Project/Scenes/MainGame.unity"
    };

    private static readonly HashSet<string> HumanoidBoneNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Root",
        "Hips",
        "Spine",
        "Spine1",
        "Spine2",
        "Neck",
        "Head",
        "LeftShoulder",
        "LeftArm",
        "LeftForeArm",
        "LeftHand",
        "RightShoulder",
        "RightArm",
        "RightForeArm",
        "RightHand",
        "LeftUpLeg",
        "LeftLeg",
        "LeftFoot",
        "LeftToeBase",
        "RightUpLeg",
        "RightLeg",
        "RightFoot",
        "RightToeBase"
    };

    [MenuItem("Tools/MainGame/Fix Player Hair Avatar Rig")]
    public static void Fix()
    {
        var fixedCount = 0;
        foreach (var scenePath in ScenePaths.Where(System.IO.File.Exists))
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            fixedCount += FixSceneHairBones(scene.GetRootGameObjects());

            if (fixedCount > 0)
                EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Debug.Log($"PlayerAvatarRigFixer renamed {fixedCount} duplicate hair rig bones.");
    }

    private static int FixSceneHairBones(IEnumerable<GameObject> roots)
    {
        var renamed = 0;
        foreach (var hairRoot in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                     .Where(transform => transform.name == "npc_haircut_a_02"))
        {
            foreach (var child in hairRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == hairRoot || !HumanoidBoneNames.Contains(child.name) || child.name.StartsWith("HairRig_", StringComparison.Ordinal))
                    continue;

                Undo.RecordObject(child, "Rename duplicate hair rig bone");
                child.name = "HairRig_" + child.name;
                EditorUtility.SetDirty(child);
                renamed++;
            }
        }

        return renamed;
    }
}
