using TMPro;
using UnityEngine;

public class PlayerTutorial : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private TMP_Text hintText;

    [SerializeField] private float moveRequiredTime = 2f;
    [SerializeField] private float runRequiredTime = 2f;
    [SerializeField] private float completedDisplayTime = 3f;

    private int stage;
    private float progress;

    private void Start()
    {
        if (player == null || hintText == null)
        {
            Debug.LogError(
                "PlayerTutorial：请连接 Player 和 Hint Text。",
                this
            );

            enabled = false;
            return;
        }

        stage = 0;
        progress = 0f;
        hintText.enabled = true;
        hintText.text = "MOVE: A / D or Arrow Keys";
    }

    private void Update()
    {
        // 暂停时不推进教学
        if (Time.timeScale == 0f)
        {
            return;
        }

        switch (stage)
        {
            case 0:
                if (player.IsMoving)
                {
                    progress += Time.deltaTime;
                }

                if (progress >= moveRequiredTime)
                {
                    stage = 1;
                    progress = 0f;
                    hintText.text = "RUN: Hold Left Shift while moving";
                }
                break;

            case 1:
                if (player.IsMoving && player.IsRunning)
                {
                    progress += Time.deltaTime;
                }

                if (progress >= runRequiredTime)
                {
                    stage = 2;
                    progress = 0f;
                    hintText.text = "Tutorial complete. Stay alert!";
                }
                break;

            case 2:
                progress += Time.deltaTime;

                if (progress >= completedDisplayTime)
                {
                    hintText.enabled = false;
                    enabled = false;
                }
                break;
        }
    }
}