using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class MainGameNavMeshAndMonsterSetup
{
    [MenuItem("Tools/MainGame/Apply NavMesh And Monster")]
    public static void Apply()
    {
        var player = FindInActiveScene("Player");
        if (player == null)
        {
            Debug.LogError("Cannot setup monster AI. Player was not found in the active scene.");
            return;
        }

        var surface = EnsureNavMeshSurface();
        var houseZone = EnsureHouseDetectionZone();
        var monster = EnsureMonsterPlaceholder(player, houseZone);

        surface.BuildNavMesh();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"NavMesh baked and monster AI setup completed. Monster: {monster.name}");
    }

    private static NavMeshSurface EnsureNavMeshSurface()
    {
        var root = FindInActiveScene("NavigationRoot");
        if (root == null)
            root = new GameObject("NavigationRoot").transform;

        var surface = root.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = root.gameObject.AddComponent<NavMeshSurface>();

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = false;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.12f;
        surface.overrideTileSize = true;
        surface.tileSize = 128;

        return surface;
    }

    private static BoxCollider EnsureHouseDetectionZone()
    {
        var zone = FindInActiveScene("HouseDetectionZone");
        if (zone == null)
            zone = new GameObject("HouseDetectionZone").transform;

        zone.position = new Vector3(-4f, 2.1f, -15f);
        zone.rotation = Quaternion.identity;

        var collider = zone.GetComponent<BoxCollider>();
        if (collider == null)
            collider = zone.gameObject.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        collider.size = new Vector3(24f, 5.5f, 26f);
        collider.center = Vector3.zero;
        return collider;
    }

    private static GameObject EnsureMonsterPlaceholder(Transform player, Collider houseZone)
    {
        var monster = FindInActiveScene("MonsterPlaceholder");
        if (monster == null)
        {
            monster = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            monster.name = "MonsterPlaceholder";
        }

        monster.position = new Vector3(-2f, 1.1f, -8.5f);
        monster.rotation = Quaternion.Euler(0f, 180f, 0f);
        monster.localScale = new Vector3(1.15f, 1.75f, 1.15f);

        var renderer = monster.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = EnsureMonsterMaterial();

        var agent = monster.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = monster.gameObject.AddComponent<NavMeshAgent>();

        agent.radius = 0.45f;
        agent.height = 2.15f;
        agent.baseOffset = 1.05f;
        agent.speed = 3.6f;
        agent.acceleration = 8f;
        agent.angularSpeed = 240f;
        agent.stoppingDistance = 1.35f;

        var visionOrigin = EnsureChild(monster, "VisionOrigin");
        visionOrigin.localPosition = new Vector3(0f, 1.55f, 0.2f);
        visionOrigin.localRotation = Quaternion.identity;

        var ai = monster.GetComponent<MonsterAI>();
        if (ai == null)
            ai = monster.gameObject.AddComponent<MonsterAI>();

        var serialized = new SerializedObject(ai);
        Set(serialized, "player", player);
        Set(serialized, "houseBounds", houseZone);
        Set(serialized, "requirePlayerInsideHouse", true);
        Set(serialized, "visionOrigin", visionOrigin);
        Set(serialized, "viewDistance", 16f);
        Set(serialized, "viewAngle", 90f);
        Set(serialized, "loseSightGraceTime", 1.5f);
        Set(serialized, "chaseSpeed", 3.6f);
        Set(serialized, "acceleration", 8f);
        Set(serialized, "stoppingDistance", 1.35f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return monster.gameObject;
    }

    private static Material EnsureMonsterMaterial()
    {
        const string folder = "Assets/MainGame/Materials";
        const string path = folder + "/MonsterPlaceholder.mat";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/MainGame", "Materials");

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = new Color(0.12f, 0.015f, 0.02f, 1f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child;

        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static Transform FindInActiveScene(string name)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == name && transform.gameObject.scene == activeScene)
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

    private static void Set(SerializedObject serialized, string propertyName, float value)
    {
        var property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }
}
