using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class GameP2MirrorGhostSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";

    [MenuItem("MainGame/P2/Apply P2 Mirror And Ghost Systems")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        var ghost = ConfigureGhostDirector();
        ConfigureMirror(ghost);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("P2 mirror and ghost systems applied.");
    }

    private static P2GhostDoorApparitionDirector ConfigureGhostDirector()
    {
        var ghostRoot = FindSceneTransform("MonsterPlaceholder");
        if (ghostRoot == null)
        {
            var obj = new GameObject("P2_Ghost");
            ghostRoot = obj.transform;
        }

        var agent = ghostRoot.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = ghostRoot.gameObject.AddComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.speed = 1.6f;
        agent.acceleration = 8f;
        agent.angularSpeed = 360f;
        EditorUtility.SetDirty(agent);

        var director = ghostRoot.GetComponent<P2GhostDoorApparitionDirector>();
        if (director == null)
            director = ghostRoot.gameObject.AddComponent<P2GhostDoorApparitionDirector>();

        var patrolRoot = EnsureRoot("P2_GhostPatrolPoints");
        SeedPatrolPointsIfEmpty(patrolRoot);

        var apparitionRoot = EnsureRoot("P2_GhostDoorApparitionPoints");
        SeedDoorApparitionPointsIfEmpty(apparitionRoot);

        var serialized = new SerializedObject(director);
        Set(serialized, "player", FindPlayer());
        Set(serialized, "visualRoot", FindChildRecursive(ghostRoot, "mada2") ?? ghostRoot);
        Set(serialized, "patrolRoot", patrolRoot);
        Set(serialized, "doorApparitionRoot", apparitionRoot);
        Set(serialized, "agent", agent);
        Set(serialized, "autoCollectPointsFromChildren", true);
        Set(serialized, "pingPongPatrol", true);
        Set(serialized, "beginAwakened", true);
        Set(serialized, "enableDoorApparitions", true);
        Set(serialized, "useDoorApparitionPoints", true);
        Set(serialized, "hideVisualBetweenApparitions", false);
        Set(serialized, "disableOtherGhostAiOnStart", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(director);
        return director;
    }

    private static void ConfigureMirror(P2GhostDoorApparitionDirector ghost)
    {
        var mirror = FindSceneTransform("MirrorJumpscare");
        if (mirror == null)
        {
            var obj = new GameObject("P2_StoryMirror");
            mirror = obj.transform;
        }

        var oldMirror = mirror.GetComponent<MainGame.MirrorJumpscare>();
        if (oldMirror != null)
        {
            oldMirror.enabled = false;
            EditorUtility.SetDirty(oldMirror);
        }

        var collider = mirror.GetComponent<BoxCollider>();
        if (collider == null)
            collider = mirror.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        if (collider.size == Vector3.zero)
            collider.size = new Vector3(2f, 2.6f, 0.8f);
        EditorUtility.SetDirty(collider);

        var p2Mirror = mirror.GetComponent<P2MirrorJumpscare>();
        if (p2Mirror == null)
            p2Mirror = mirror.gameObject.AddComponent<P2MirrorJumpscare>();

        var revealPoint = EnsureChild(mirror, "P2_MirrorGhostRevealPoint");
        if (revealPoint.localPosition == Vector3.zero)
            revealPoint.localPosition = new Vector3(0f, 1.15f, -0.7f);

        var serialized = new SerializedObject(p2Mirror);
        Set(serialized, "reflection", mirror.GetComponent<MirrorReflectionCamera>());
        Set(serialized, "mirrorRaycastTarget", FindChildRecursive(mirror, "MirrorSurface") ?? mirror);
        Set(serialized, "ghostDirector", ghost);
        Set(serialized, "ghostController", Object.FindFirstObjectByType<P2GhostController>(FindObjectsInactive.Include));
        Set(serialized, "ghostRevealPoint", revealPoint);
        Set(serialized, "triggerP2MirrorBreakEvent", true);
        Set(serialized, "awakenGhostAfterEvent", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(p2Mirror);
    }

    private static Transform EnsureRoot(string name)
    {
        var root = FindSceneTransform(name);
        if (root != null)
            return root;

        return new GameObject(name).transform;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child;

        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static void SeedPatrolPointsIfEmpty(Transform patrolRoot)
    {
        if (patrolRoot == null || patrolRoot.childCount > 0)
            return;

        int created = 0;
        foreach (var marker in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (marker == null || !marker.name.StartsWith("MonsterPatrolPoint_", System.StringComparison.OrdinalIgnoreCase))
                continue;

            created++;
            var point = EnsureChild(patrolRoot, $"Patrol_{created:00}");
            point.position = marker.position;
            point.rotation = marker.rotation;
        }
    }

    private static void SeedDoorApparitionPointsIfEmpty(Transform root)
    {
        if (root == null || root.childCount > 0)
            return;

        var doors = Object.FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            var door = doors[i];
            if (door == null)
                continue;

            var point = EnsureChild(root, $"DoorApparition_{i + 1:00}_{door.name}");
            Vector3 position = door.transform.position + door.transform.forward * 1.15f;
            if (NavMesh.SamplePosition(position, out var hit, 2.5f, NavMesh.AllAreas))
                position = hit.position;
            point.position = position;
            point.rotation = Quaternion.LookRotation(-door.transform.forward, Vector3.up);
        }
    }

    private static Transform FindPlayer()
    {
        var fps = Object.FindFirstObjectByType<FpsHorrorKit.FpsController>(FindObjectsInactive.Include);
        if (fps != null)
            return fps.transform;

        return FindSceneTransform("Player") ?? FindSceneTransform("PlayerP2");
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;

        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == name)
                return child;
        }

        return null;
    }

    private static Transform FindSceneTransform(string objectName)
    {
        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform != null && transform.name == objectName)
                return transform;
        }

        return null;
    }

    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, bool value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }
}
