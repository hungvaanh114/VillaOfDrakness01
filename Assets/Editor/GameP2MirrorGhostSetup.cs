using MainGame.P2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class GameP2MirrorGhostSetup
{
    private const string ScenePath = "Assets/MainGame/GameP2.unity";
    private const float UpperFloorMinY = 2.4f;
    private const string MirrorClothMaterialPath = "Assets/MainGame/Materials/InventoryProgression/P2_MirrorClothCover.mat";
    private const string ScreenJumpscareTexturePath = "Assets/MainGame/UI/anhHuMa.png";

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
        var downstairsPatrolRoot = EnsureRoot("P2_GhostPatrol_Downstairs");
        var upstairsPatrolRoot = EnsureRoot("P2_GhostPatrol_Upstairs");
        SeedPatrolPointsIfEmpty(downstairsPatrolRoot, upstairsPatrolRoot, patrolRoot);

        var serialized = new SerializedObject(director);
        Set(serialized, "player", FindPlayer());
        Set(serialized, "visualRoot", FindChildRecursive(ghostRoot, "mada2") ?? ghostRoot);
        Set(serialized, "patrolRoot", patrolRoot);
        Set(serialized, "doorApparitionRoot", (Object)null);
        Set(serialized, "downstairsPatrolRoot", downstairsPatrolRoot);
        Set(serialized, "upstairsPatrolRoot", upstairsPatrolRoot);
        Set(serialized, "downstairsDoorApparitionRoot", (Object)null);
        Set(serialized, "upstairsDoorApparitionRoot", (Object)null);
        Set(serialized, "agent", agent);
        Set(serialized, "autoCollectPointsFromChildren", true);
        Set(serialized, "pingPongPatrol", true);
        Set(serialized, "beginAwakened", false);
        Set(serialized, "lockUpperFloorUntilAwakened", true);
        Set(serialized, "showUpperFloorAfterAwakened", true);
        Set(serialized, "enableDoorApparitions", true);
        Set(serialized, "useDoorApparitionPoints", false);
        Set(serialized, "useSceneDoorsForApparitions", true);
        Set(serialized, "useDoorApparitionPointsForWalkAway", false);
        Set(serialized, "avoidPlayerAfterTeleportSeconds", 3f);
        Set(serialized, "lampOffDirectApproachSeconds", 10f);
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

        RemoveGeneratedMirrorFrame(mirror);
        RemoveRuntimeMirrorFrame(mirror);

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

        var mirrorSurface = FindChildRecursive(mirror, "MirrorSurface");
        var revealPoint = EnsureChild(mirror, "P2_MirrorGhostRevealPoint");
        AlignMirrorRevealPoint(revealPoint);

        var clothCover = EnsureMirrorClothCover(mirror);
        var audioData = Resources.Load<AudioData>("Audio/AudioData");

        var serialized = new SerializedObject(p2Mirror);
        var reflection = mirror.GetComponent<MirrorReflectionCamera>();
        ConfigureP2Reflection(reflection);
        Set(serialized, "reflection", reflection);
        Set(serialized, "mirrorRaycastTarget", mirrorSurface ?? mirror);
        Set(serialized, "clothCover", clothCover);
        Set(serialized, "requiredMirrorLookSeconds", 0f);
        Set(serialized, "requiredPlayerDistance", 8f);
        Set(serialized, "requirePlayerInsideTrigger", true);
        Set(serialized, "requirePlayerInFront", false);
        Set(serialized, "requireMirrorRaycast", false);
        Set(serialized, "invertMirrorFrontDirection", true);
        Set(serialized, "acceptEitherMirrorSide", true);
        Set(serialized, "startCovered", true);
        Set(serialized, "triggerImmediatelyAfterClothRemoved", true);
        Set(serialized, "coveredInteractText", "[E] Kéo tấm vải");
        Set(serialized, "uncoveredInteractText", "Nhìn vào gương");
        Set(serialized, "clothPullSeconds", 0.8f);
        Set(serialized, "clothPulledLocalOffset", new Vector3(0f, -1.7f, 0.08f));
        Set(serialized, "clothPulledLocalEulerOffset", new Vector3(0f, 0f, -12f));
        Set(serialized, "clothPullClip", audioData != null ? audioData.clothTearOff : null);
        Set(serialized, "ghostDirector", ghost);
        Set(serialized, "ghostController", Object.FindFirstObjectByType<P2GhostController>(FindObjectsInactive.Include));
        Set(serialized, "ghostRevealPoint", revealPoint);
        Set(serialized, "triggerP2MirrorBreakEvent", true);
        Set(serialized, "awakenGhostAfterEvent", true);
        Set(serialized, "mirrorEventClip", audioData != null ? audioData.ghostJumpscare : null);
        Set(serialized, "screenJumpscareTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(ScreenJumpscareTexturePath));
        Set(serialized, "screenImagePopDuration", 0.22f);
        Set(serialized, "screenImageHoldDuration", 2.5f);
        Set(serialized, "screenImageStartScale", 0.18f);
        Set(serialized, "screenImageImpactScale", 2f);
        Set(serialized, "screenImageOpacity", 1f);
        Set(serialized, "screenDarkBackdropOpacity", 0.906f);
        Set(serialized, "fallRoll", 62f);
        Set(serialized, "fallPitch", 28f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(p2Mirror);
    }

    private static void RemoveGeneratedMirrorFrame(Transform mirror)
    {
        var frame = mirror != null ? mirror.Find("MirrorFrame") : null;
        if (frame == null)
            return;

        Object.DestroyImmediate(frame.gameObject, true);
    }

    private static void RemoveRuntimeMirrorFrame(Transform mirror)
    {
        RemoveChild(mirror, "FrameTop");
        RemoveChild(mirror, "FrameBottom");
        RemoveChild(mirror, "FrameLeft");
        RemoveChild(mirror, "FrameRight");
    }

    private static void RemoveChild(Transform parent, string childName)
    {
        var child = parent != null ? parent.Find(childName) : null;
        if (child != null)
            Object.DestroyImmediate(child.gameObject, true);
    }

    private static void ConfigureP2Reflection(MirrorReflectionCamera reflection)
    {
        if (reflection == null)
            return;

        var serialized = new SerializedObject(reflection);
        Set(serialized, "createRuntimeFrame", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(reflection);
    }

    private static void AlignMirrorRevealPoint(Transform revealPoint)
    {
        if (revealPoint == null)
            return;

        revealPoint.localPosition = new Vector3(0f, 1.15f, 0.7f);
        revealPoint.localRotation = Quaternion.identity;
        EditorUtility.SetDirty(revealPoint);
    }

    private static GameObject EnsureMirrorClothCover(Transform mirror)
    {
        var child = mirror.Find("P2_MirrorClothCover");
        GameObject cloth;
        if (child != null)
        {
            cloth = child.gameObject;
        }
        else
        {
            cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "P2_MirrorClothCover";
            cloth.transform.SetParent(mirror, false);
        }

        var surface = FindChildRecursive(mirror, "MirrorSurface");
        if (surface != null)
        {
            cloth.transform.localPosition = surface.localPosition + new Vector3(0f, 0f, -0.035f);
            cloth.transform.localRotation = surface.localRotation;
            Vector3 surfaceScale = surface.localScale;
            cloth.transform.localScale = new Vector3(
                Mathf.Max(1.35f, Mathf.Abs(surfaceScale.x) * 1.08f),
                Mathf.Max(2.15f, Mathf.Abs(surfaceScale.y) * 1.08f),
                0.035f);
        }
        else
        {
            cloth.transform.localPosition = new Vector3(0f, 1.25f, -0.08f);
            cloth.transform.localRotation = Quaternion.identity;
            cloth.transform.localScale = new Vector3(1.55f, 2.35f, 0.035f);
        }

        cloth.SetActive(true);
        SetMaterial(cloth, EnsureMaterial(MirrorClothMaterialPath, new Color(0.36f, 0.035f, 0.035f, 1f)));

        var collider = cloth.GetComponent<BoxCollider>();
        if (collider == null)
            collider = cloth.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        EditorUtility.SetDirty(collider);

        EditorUtility.SetDirty(cloth);
        return cloth;
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

    private static void SeedPatrolPointsIfEmpty(Transform downstairsRoot, Transform upstairsRoot, Transform fallbackRoot)
    {
        if (downstairsRoot == null || upstairsRoot == null)
            return;

        if (downstairsRoot.childCount == 0)
            SeedPatrolFloor(downstairsRoot, fallbackRoot, false);
        if (upstairsRoot.childCount == 0)
            SeedPatrolFloor(upstairsRoot, fallbackRoot, true);
    }

    private static void SeedPatrolFloor(Transform root, Transform fallbackRoot, bool upstairs)
    {
        int created = 0;
        foreach (var marker in EnumeratePatrolSourcePoints(fallbackRoot))
        {
            if (marker == null || IsUpperFloor(marker.position) != upstairs)
                continue;

            created++;
            var point = EnsureChild(root, $"Patrol_{created:00}");
            point.position = marker.position;
            point.rotation = marker.rotation;
        }
    }

    private static System.Collections.Generic.IEnumerable<Transform> EnumeratePatrolSourcePoints(Transform fallbackRoot)
    {
        foreach (var marker in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (marker != null && marker.name.StartsWith("MonsterPatrolPoint_", System.StringComparison.OrdinalIgnoreCase))
                yield return marker;
        }

        if (fallbackRoot == null)
            yield break;

        for (int i = 0; i < fallbackRoot.childCount; i++)
            yield return fallbackRoot.GetChild(i);
    }

    private static void SeedDoorApparitionPointsIfEmpty(Transform downstairsRoot, Transform upstairsRoot)
    {
        if (downstairsRoot == null || upstairsRoot == null)
            return;

        var doors = Object.FindObjectsByType<FpsHorrorKit.DoorSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int downstairsCount = downstairsRoot.childCount;
        int upstairsCount = upstairsRoot.childCount;
        bool seedDownstairs = downstairsCount == 0;
        bool seedUpstairs = upstairsCount == 0;
        for (int i = 0; i < doors.Length; i++)
        {
            var door = doors[i];
            if (door == null)
                continue;

            bool upstairs = IsUpperFloor(door.transform.position);
            var root = upstairs ? upstairsRoot : downstairsRoot;
            if ((!upstairs && !seedDownstairs) || (upstairs && !seedUpstairs))
                continue;

            int index = upstairs ? ++upstairsCount : ++downstairsCount;
            var point = EnsureChild(root, $"DoorApparition_{index:00}_{door.name}");
            Vector3 position = door.transform.position + door.transform.forward * 1.15f;
            if (NavMesh.SamplePosition(position, out var hit, 2.5f, NavMesh.AllAreas))
                position = hit.position;
            point.position = position;
            point.rotation = Quaternion.LookRotation(-door.transform.forward, Vector3.up);
        }
    }

    private static bool IsUpperFloor(Vector3 position)
    {
        return position.y >= UpperFloorMinY;
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

    private static void Set(SerializedObject serialized, string propertyName, string value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void Set(SerializedObject serialized, string propertyName, Vector3 value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    private static void SetMaterial(GameObject obj, Material material)
    {
        if (obj == null || material == null)
            return;

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static Material EnsureMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }
}
