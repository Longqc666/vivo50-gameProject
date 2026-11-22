using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using D225.Interact;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyDemoVerification
{
    private const string PendingKey = "D225.EnemyVerification.Pending";
    private const string LogPath = "Logs/D225EnemyVerification.txt";
    private static readonly List<string> Results = new List<string>();
    private static IEnumerator<Func<bool>> steps;
    private static Func<bool> ready;
    private static double deadline;
    private static string waitingFor;

    static EnemyDemoVerification()
    {
        if (SessionState.GetBool(PendingKey, false)) Hook();
    }

    [MenuItem("D225/Enemy/2. Verify Test Scene")]
    public static void RunMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        StartChecks();
    }

    public static void RunBatch() => StartChecks();

    private static void StartChecks()
    {
        EnemyDemoSetup.OpenOrCreate();
        Results.Clear();
        if (Application.isBatchMode)
        {
            VerifyLevelBinding("Scene_B_Corridor", 5);
            VerifyLevelBinding("Scene_C_Hall", 3);
            EnemyDemoSetup.OpenOrCreate();
            Directory.CreateDirectory("Logs");
            File.WriteAllLines(LogPath, Results);
        }
        SessionState.SetBool(PendingKey, true);
        Hook();
        EditorApplication.isPlaying = true;
    }

    private static void VerifyLevelBinding(string sceneName, int pointCount)
    {
        string path = "Assets/Scenes/" + sceneName + ".unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
        EditorSceneManager.OpenScene(path);
        EnemyDemoSetup.PlaceInActiveLevel();
        EnemyAI placed = UnityEngine.Object.FindObjectOfType<EnemyAI>();
        Check(placed != null, sceneName + " accepts enemy prefab");
        var serialized = new SerializedObject(placed);
        Check(serialized.FindProperty("target").objectReferenceValue is PlayerHiding
            && serialized.FindProperty("patrolPoints").arraySize == pointCount, sceneName + " binds player and " + pointCount + " patrol points");
    }

    private static void Hook()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(PendingKey, false)) return;
        Results.Clear();
        if (Application.isBatchMode && File.Exists(LogPath)) Results.AddRange(File.ReadAllLines(LogPath));
        SessionState.SetBool(PendingKey, false);
        Application.runInBackground = true;
        steps = CheckPlay().GetEnumerator();
        ready = null;
        EditorApplication.update += Advance;
    }

    private static void Advance()
    {
        try
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode stopped before verification finished.");
            if (ready != null && !ready())
            {
                if (EditorApplication.timeSinceStartup >= deadline) throw new TimeoutException(waitingFor);
                return;
            }
            if (steps.MoveNext()) ready = steps.Current;
            else Finish(null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static IEnumerable<Func<bool>> CheckPlay()
    {
        EnemyAI ai = UnityEngine.Object.FindObjectOfType<EnemyAI>();
        EnemyPerception sight = ai.GetComponent<EnemyPerception>();
        EnemyCatch catcher = ai.GetComponent<EnemyCatch>();
        EnemyDemoSession demo = UnityEngine.Object.FindObjectOfType<EnemyDemoSession>();
        PlayerHiding player = UnityEngine.Object.FindObjectOfType<PlayerHiding>();
        PlayerNoise noise = player.GetComponent<PlayerNoise>();
        PlayerController controller = player.GetComponent<PlayerController>();
        PlayerDetection detection = player.GetComponent<PlayerDetection>();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        HidingSpot locker = UnityEngine.Object.FindObjectOfType<HidingSpot>();
        GameObject wall = Array.Find(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(),
            root => root.name == "Sight_Blocker");
        int caughtCount = 0;
        int firstDetectionCount = 0;
        catcher.PlayerCaught += _ => caughtCount++;
        detection.FirstDetected += () => firstDetectionCount++;
        var context = InteractionContext.For(player.gameObject);

        yield return WaitFor(() => ai.transform.position.x > 5.5f, 8, "patrol reaches right waypoint");
        yield return WaitFor(() => ai.transform.position.x < -4.6f, 12, "patrol turns and reaches left waypoint");
        Check(ai.State == EnemyAI.EnemyState.Patrol, "T04 patrol completes both directions");
        Check(playerCollider.IsTouching(GameObject.Find("Ground").GetComponent<Collider2D>()),
            "Demo player spawn is supported by the corridor floor");

        demo.Retry();
        Vector2 point = ai.transform.position;
        Warp(player.transform, point + Vector2.right * 2.8f);
        Check(sight.CanSee(player, 1), "T05 clear front can be seen");
        Warp(player.transform, point - Vector2.right * 2.8f);
        Check(!sight.CanSee(player, 1), "T05 behind is rejected");
        Warp(player.transform, point + Vector2.right * 4.6f);
        Check(!sight.CanSee(player, 1), "T05 beyond 4.5 units is rejected");
        Warp(player.transform, point + new Vector2(2f, 1.5f));
        Check(!sight.CanSee(player, 1), "T05 other height is rejected");
        Warp(player.transform, point + Vector2.right * 2.8f);
        SetWall(wall, 1.5f, false, 0);
        Check(!sight.CanSee(player, 1), "T05 solid wall blocks sight");
        SetWall(wall, 1.5f, true, 0);
        Check(sight.CanSee(player, 1), "T05 ordinary trigger does not block sight");
        int curtainLayer = LayerMask.NameToLayer("VisionBlocker");
        Check(curtainLayer >= 0, "level VisionBlocker layer exists");
        SetWall(wall, 1.5f, true, curtainLayer);
        Check(!sight.CanSee(player, 1), "T05 VisionBlocker trigger cuts sight");
        wall.SetActive(false);
        Physics2D.SyncTransforms();

        float exposureStart = Time.fixedTime;
        yield return WaitFor(() => ai.State == EnemyAI.EnemyState.Chase, 3, "continuous exposure starts Chase");
        Check(Time.fixedTime - exposureStart >= 0.33f && firstDetectionCount == 1, "T05 recognition threshold and player detection callback");
        float playerX = player.transform.position.x;
        SetWall(wall, (ai.transform.position.x + playerX) * 0.5f, false, 0);
        yield return WaitFor(() => ai.State == EnemyAI.EnemyState.Investigate, 3, "occlusion starts last-sighting search");
        Vector2 remembered = ai.InvestigationPoint;
        Check(Mathf.Abs(remembered.x - playerX) < 0.1f, "T05 records last visible location");
        Warp(player.transform, new Vector2(-10f, body.position.y));
        float searchStart = Time.fixedTime;
        yield return WaitFor(() => ai.State == EnemyAI.EnemyState.Return, 8, "blocked last-sighting search still terminates");
        Check(Time.fixedTime - searchStart >= 2.9f && ai.InvestigationPoint == remembered, "T05 search remembers the location and times out");
        wall.SetActive(false);
        Physics2D.SyncTransforms();
        yield return WaitFor(() => ai.State == EnemyAI.EnemyState.Patrol, 8, "return reaches patrol waypoint");
        Check(true, "T05 Return resumes patrol");

        demo.Retry();
        TestNoise(ai, player, noise, body, controller);
        demo.Retry();
        Check(locker.Interact(context).Succeeded && player.IsHidden, "T06 actual locker enters hiding");
        Check(!sight.CanSee(player, -1), "T06 hidden target is excluded from vision");
        demo.Retry();
        Check(!locker.IsOccupied && !player.IsHidden && !controller.IsControlLocked, "T06 retry releases locker and restores player");

        point = ai.transform.position;
        Warp(player.transform, point + Vector2.right * 2.8f);
        yield return WaitFor(() => ai.State == EnemyAI.EnemyState.Chase, 3, "prepare contact check in Chase");
        BoxCollider2D wallCollider = wall.GetComponent<BoxCollider2D>();
        Collider2D ghostCollider = ai.GetComponent<Collider2D>();
        float edge = ghostCollider.bounds.max.x;
        float halfPlayer = playerCollider.bounds.extents.x;
        Warp(player.transform, new Vector2(edge + halfPlayer + 0.05f, ai.transform.position.y));
        wall.transform.localScale = new Vector3(0.04f, 3f, 1f);
        SetWall(wall, edge + 0.025f, false, 0);
        var testCatch = typeof(EnemyCatch).GetMethod("TryCatch", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(!wallCollider.isTrigger && ghostCollider.Distance(playerCollider).distance <= 0.12f
            && !(bool)testCatch.Invoke(catcher, new object[] { player, playerCollider }) && caughtCount == 0,
            "T09 nearby collider across solid wall is not caught");
        wall.SetActive(false);
        Physics2D.SyncTransforms();

        Time.timeScale = 0f;
        Vector2 pausedPosition = ai.transform.position;
        double pausedUntil = EditorApplication.timeSinceStartup + 0.35;
        yield return WaitFor(() => EditorApplication.timeSinceStartup > pausedUntil, 3, "paused update");
        Check(ai.State == EnemyAI.EnemyState.Chase && !catcher.HasCaught && (Vector2)ai.transform.position == pausedPosition,
            "T10 pause freezes Chase and prevents catch");
        Time.timeScale = 1f;
        yield return WaitFor(() => caughtCount == 1, 3, "actual FixedUpdate catches on resume");
        Check(player.IsCatchLocked && controller.IsControlLocked && !locker.Interact(context).Succeeded,
            "T06/T09 catch locks player before hiding or movement");
        float caughtUntil = Time.fixedTime + 0.2f;
        yield return WaitFor(() => Time.fixedTime >= caughtUntil, 3, "later physics steps after catch");
        Check(caughtCount == 1, "T09 catch callback fires once");
        demo.Retry();
        Check(ai.State == EnemyAI.EnemyState.Patrol && !catcher.HasCaught && !player.IsCatchLocked
            && !controller.IsControlLocked && Vector2.Distance(ai.transform.position, point) < 0.02f,
            "T09 demo retry restores ghost and player to spawn");
    }

    private static void TestNoise(EnemyAI ai, PlayerHiding player, PlayerNoise noise, Rigidbody2D body, PlayerController controller)
    {
        Vector2 source = (Vector2)ai.transform.position + Vector2.left * 3f;
        Warp(player.transform, source);
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo updateNoise = typeof(PlayerNoise).GetMethod("Update", privateInstance);
        FieldInfo running = typeof(PlayerController).GetField("isRunning", privateInstance);
        float radius = 0f;
        Vector2 emittedPosition = Vector2.zero;
        Action<GameObject, Vector2, float> collect = (actor, position, value) =>
        {
            if (actor != player.gameObject) return;
            radius = value;
            emittedPosition = position;
        };
        PlayerNoise.NoiseEmitted += collect;
        try
        {
            body.velocity = Vector2.right;
            running.SetValue(controller, false);
            // SendMessage("Update") also runs PlayerController.Update and reads the keyboard.
            // Invoke only PlayerNoise.Update to test the real event with a controlled movement state.
            updateNoise.Invoke(noise, null);
            Check(radius == 2f, $"T05 walk event radius is 2 (actual {radius})");
            Check(ai.State == EnemyAI.EnemyState.Patrol, $"T05 walk is out of range (actual {ai.State})");
            noise.enabled = false;
            noise.enabled = true;
            radius = 0f;
            running.SetValue(controller, true);
            updateNoise.Invoke(noise, null);
            Check(radius == 5f, $"T05 sprint event radius is 5 (actual {radius})");
            Check(ai.State == EnemyAI.EnemyState.Investigate, $"T05 sprint starts Investigate (actual {ai.State})");
            Check(Vector2.Distance(emittedPosition, source) < 0.01f && Vector2.Distance(ai.InvestigationPoint, emittedPosition) < 0.01f,
                $"T05 sprint records sound origin (expected {source}, emitted {emittedPosition}, investigated {ai.InvestigationPoint})");
        }
        finally
        {
            PlayerNoise.NoiseEmitted -= collect;
        }
    }

    private static void SetWall(GameObject wall, float x, bool trigger, int layer)
    {
        wall.transform.position = new Vector3(x, 0.7f, 0f);
        wall.layer = layer;
        wall.GetComponent<BoxCollider2D>().isTrigger = trigger;
        wall.SetActive(true);
        Physics2D.SyncTransforms();
    }

    private static void Warp(Transform actor, Vector2 position)
    {
        Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
        body.interpolation = RigidbodyInterpolation2D.None;
        actor.position = new Vector3(position.x, position.y, actor.position.z);
        body.position = position;
        body.velocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static Func<bool> WaitFor(Func<bool> condition, double seconds, string description)
    {
        deadline = EditorApplication.timeSinceStartup + seconds;
        waitingFor = description;
        return condition;
    }

    private static void Check(bool passed, string description)
    {
        if (!passed) throw new InvalidOperationException(description);
        Results.Add("PASS  " + description);
        Debug.Log("[Enemy Verify] PASS  " + description);
    }

    private static void Finish(Exception error)
    {
        Time.timeScale = 1f;
        EditorApplication.update -= Advance;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        steps?.Dispose();
        if (error != null)
        {
            Results.Add("FAIL  " + error.Message);
            Debug.LogException(error);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines(LogPath, Results);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}
