using System;
using System.Linq;
using D225.Interact;
using D225.Quest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyDemoSetup
{
    public const string ScenePath = "Assets/Scenes/D225_EnemyTest.unity";
    private const string PrefabPath = "Assets/Prefabs/Enemy/D225_Enemy.prefab";
    private const string MaterialPath = "Assets/Settings/EnemyDemoUnlit.mat";
    private const string PlayerPath = "Assets/Prefabs/Player.prefab";
    private const string SpritePath = "Assets/Art/Placeholder/D225_WhiteSquare.png";

    [MenuItem("D225/Enemy/1. Open Test Scene")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        OpenOrCreate();
    }

    public static void OpenOrCreate()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (playerPrefab == null || playerPrefab.GetComponent<PlayerHiding>() == null || sprite == null)
            throw new InvalidOperationException("The current main branch is required for the Player prefab and white square sprite.");

        Material material = GetMaterial();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 6.5f;
        camera.transform.position = new Vector3(0f, 0.8f, -10f);
        camera.backgroundColor = new Color(0.045f, 0.053f, 0.085f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        Color concrete = new Color(0.22f, 0.26f, 0.35f);
        Square("Ground", new Vector2(0f, -1.3f), new Vector2(23f, 1f), concrete, sprite, material).AddComponent<BoxCollider2D>();
        Square("Wall_Left", new Vector2(-11.5f, 0.7f), new Vector2(0.6f, 3f), concrete, sprite, material).AddComponent<BoxCollider2D>();
        Square("Wall_Right", new Vector2(11.5f, 0.7f), new Vector2(0.6f, 3f), concrete, sprite, material).AddComponent<BoxCollider2D>();

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.transform.position = new Vector3(-10.2f, 0.05f, 0f);
        foreach (SpriteRenderer renderer in player.GetComponentsInChildren<SpriteRenderer>())
            renderer.sharedMaterial = material;
        PlayerHiding hiding = player.GetComponent<PlayerHiding>();

        GameObject ghost = (GameObject)PrefabUtility.InstantiatePrefab(GetPrefab(sprite, material), scene);
        ghost.transform.position = new Vector3(0f, 0.05f, 0f);
        EnemyAI ai = ghost.GetComponent<EnemyAI>();
        Transform routeRoot = new GameObject("Patrol Route").transform;
        Transform right = Waypoint("Patrol_Right", 6f, routeRoot);
        Transform left = Waypoint("Patrol_Left", -5f, routeRoot);
        Assign(ai, hiding, new[] { right, left });

        GameObject cabinet = Square("Test_Locker", new Vector2(-7f, 0.2f), new Vector2(1.1f, 2f), new Color(0.15f, 0.48f, 0.51f), sprite, material);
        cabinet.GetComponent<SpriteRenderer>().sortingOrder = -1;
        cabinet.AddComponent<BoxCollider2D>().isTrigger = true;
        cabinet.AddComponent<HidingSpot>().Configure(cabinet.transform, false, PuzzleFlag.None);
        // QuestLogProvider 供正式玩家的交互入口使用。
        new GameObject("Quest Log").AddComponent<QuestLogProvider>();

        GameObject blocker = Square("Sight_Blocker", new Vector2(1.5f, 0.7f), new Vector2(0.3f, 3f), concrete, sprite, material);
        blocker.AddComponent<BoxCollider2D>();
        blocker.SetActive(false);

        EnemyDemoSession demo = new GameObject("Demo Session").AddComponent<EnemyDemoSession>();
        var session = new SerializedObject(demo);
        session.FindProperty("enemy").objectReferenceValue = ai;
        session.FindProperty("player").objectReferenceValue = hiding;
        session.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = ghost;
    }

    [MenuItem("D225/Enemy/3. Place Enemy in Active B or C Scene")]
    public static void PlaceInActiveLevel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Scene_B_Corridor" && scene.name != "Scene_C_Hall")
        {
            Debug.LogWarning("[Enemy] Open Scene_B_Corridor or Scene_C_Hall first.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var roots = scene.GetRootGameObjects();
        if (roots.Any(root => root.GetComponentInChildren<EnemyAI>(true) != null))
        {
            Debug.LogWarning("[Enemy] This scene already contains an EnemyAI instance.");
            return;
        }
        var players = roots.SelectMany(root => root.GetComponentsInChildren<PlayerHiding>()).ToArray();
        var route = roots.SelectMany(root => root.GetComponentsInChildren<Transform>())
            .Where(point => point.name.StartsWith("Patrol_WP_", StringComparison.Ordinal))
            .OrderBy(point => point.position.x).ToArray();
        if (prefab == null || players.Length != 1 || route.Length < 2)
        {
            Debug.LogWarning("[Enemy] Expected the enemy prefab, one active PlayerHiding and two or more Patrol_WP points.");
            return;
        }

        GameObject ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(ghost, "Place D225 Enemy");
        // B 从中段起步，C 从中间路点向门厅移动，给入口留出观察空间。
        int start = route.Length / 2;
        Vector3 point = route[start].position;
        ghost.transform.position = point + Vector3.up * 0.9f;
        Assign(ghost.GetComponent<EnemyAI>(), players[0], route, Mathf.Min(start + 1, route.Length - 1));
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = ghost;
    }

    private static void Assign(EnemyAI enemy, PlayerHiding player, Transform[] route, int initialPatrolIndex = 0)
    {
        var setup = new SerializedObject(enemy);
        setup.FindProperty("target").objectReferenceValue = player;
        setup.FindProperty("initialPatrolIndex").intValue = initialPatrolIndex;
        SerializedProperty points = setup.FindProperty("patrolPoints");
        points.arraySize = route.Length;
        for (int index = 0; index < route.Length; index++)
            points.GetArrayElementAtIndex(index).objectReferenceValue = route[index];
        setup.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material GetMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) throw new InvalidOperationException("Missing URP 2D Sprite-Unlit shader.");
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static GameObject GetPrefab(Sprite sprite, Material material)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;

        GameObject ghost = Square("D225_Enemy", Vector2.zero, new Vector2(0.8f, 1.6f), new Color(0.67f, 0.42f, 1f), sprite, material);
        ghost.AddComponent<BoxCollider2D>();
        var body = ghost.AddComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        EnemyAI ai = ghost.AddComponent<EnemyAI>();
        GameObject marker = Square("Facing", Vector2.zero, new Vector2(0.2f, 0.14f), Color.white, sprite, material);
        marker.GetComponent<SpriteRenderer>().sortingOrder = 1;
        marker.transform.SetParent(ghost.transform, false);
        marker.transform.localPosition = new Vector3(0.65f, 0.22f, -0.1f);
        var setup = new SerializedObject(ai);
        setup.FindProperty("facingMarker").objectReferenceValue = marker.transform;
        setup.ApplyModifiedPropertiesWithoutUndo();
        prefab = PrefabUtility.SaveAsPrefabAsset(ghost, PrefabPath);
        UnityEngine.Object.DestroyImmediate(ghost);
        return prefab;
    }

    private static Transform Waypoint(string name, float x, Transform parent)
    {
        Transform point = new GameObject(name).transform;
        point.SetParent(parent);
        point.position = new Vector3(x, 0f, 0f);
        return point;
    }

    private static GameObject Square(string name, Vector2 position, Vector2 size, Color color, Sprite sprite, Material material)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        return obj;
    }
}
