using TMPro;
using UnityEngine;

public class PlayerThreatHint : MonoBehaviour
{
    [SerializeField] private PlayerDetection player;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private float displayDuration = 5f;

    private float timer;

    private void Awake()
    {
        if (player == null || hintText == null)
        {
            Debug.LogError(
                "PlayerThreatHint：请连接 Player 和 Hint Text。",
                this
            );

            enabled = false;
            return;
        }

        hintText.text = string.Empty;
    }

    private void OnEnable()
    {
        if (player != null)
            player.FirstDetected += ShowHint;
    }

    private void OnDisable()
    {
        if (player != null)
            player.FirstDetected -= ShowHint;

        if (hintText != null)
            hintText.text = string.Empty;

        timer = 0f;
    }

    private void ShowHint()
    {
        hintText.text =
            "被发现了！按住左 Shift 奔跑，" +
            "靠近储物柜按 E 躲藏。";

        timer = displayDuration;
    }

    private void Update()
    {
        if (timer <= 0f)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
            hintText.text = string.Empty;
    }
}