using System;
using UnityEngine;

[RequireComponent(typeof(PlayerAnimation))]
[RequireComponent(typeof(PlayerController))]
public class PlayerDetection : MonoBehaviour
{
    public event Action FirstDetected;

    private PlayerAnimation playerAnimation;
    private PlayerController controller;
    private bool hasBeenDetected;

    private void Awake()
    {
        playerAnimation = GetComponent<PlayerAnimation>();
        controller = GetComponent<PlayerController>();
    }

    // 鬼怪确认发现玩家时调用
    public void NotifyDetected()
    {
        if (Time.timeScale == 0f || controller.IsControlLocked)
            return;

        playerAnimation.PlayScared();

        if (hasBeenDetected)
            return;

        hasBeenDetected = true;
        FirstDetected?.Invoke();
    }

    [ContextMenu("测试被鬼怪发现")]
    private void TestDetection()
    {
        if (Application.isPlaying)
            NotifyDetected();
    }
}