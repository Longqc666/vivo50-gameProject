using UnityEngine;

// 只挂在独立测试场景。正式场景由流程模块接收抓捕通知。
public sealed class EnemyDemoSession : MonoBehaviour
{
    [SerializeField] private EnemyAI enemy;
    [SerializeField] private PlayerHiding player;

    private EnemyCatch catcher;
    private Rigidbody2D playerBody;
    private Vector2 spawn;
    private bool failed;
    private bool paused;

    private void Awake()
    {
        catcher = enemy.GetComponent<EnemyCatch>();
        playerBody = player.GetComponent<Rigidbody2D>();
        spawn = playerBody.position;
    }

    private void OnEnable() => catcher.PlayerCaught += OnCaught;

    private void OnDisable()
    {
        catcher.PlayerCaught -= OnCaught;
        if (paused) Time.timeScale = 1f;
    }

    private void OnCaught(PlayerHiding caughtPlayer) => failed = true;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) Retry();
        if (!failed && Input.GetKeyDown(KeyCode.Escape))
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
        }
    }

    public void Retry()
    {
        player.ResetAfterFailure();
        player.transform.position = new Vector3(spawn.x, spawn.y, player.transform.position.z);
        playerBody.position = spawn;
        playerBody.velocity = Vector2.zero;
        enemy.ResetToSpawn();
        Physics2D.SyncTransforms();
        failed = false;
        paused = false;
        Time.timeScale = 1f;
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(12f, 12f, 345f, 88f), "D225  |  Enemy AI Test");
        GUI.Label(new Rect(24f, 40f, 321f, 22f), "A/D Move   Shift Sprint   E Hide   Esc Pause   R Retry");
        string status = failed ? "Caught - press R to retry" : paused ? "Paused" : player.IsHidden ? "Hidden" : enemy.State.ToString();
        GUI.Label(new Rect(24f, 66f, 321f, 22f), "State: " + status);
    }
}
