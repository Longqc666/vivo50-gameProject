using UnityEngine;
using TMPro;

public class PlayerInteractionHint : MonoBehaviour
{
    [SerializeField] private PlayerInteractor player;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private float feedbackDuration = 3f;

    private int seenFeedbackVersion;
    private float feedbackTimer;
    private string feedback;

    private void Start()
    {
        if (player == null || hintText == null)
        {
            Debug.LogError(
                "PlayerInteractionHint：请连接 Player 和 Hint Text。",
                this
            );

            enabled = false;
            return;
        }

        seenFeedbackVersion = player.FeedbackVersion;
        hintText.text = string.Empty;
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            hintText.text = string.Empty;
            return;
        }

        if (player.FeedbackVersion != seenFeedbackVersion)
        {
            seenFeedbackVersion = player.FeedbackVersion;
            feedback = player.LastFeedback;
            feedbackTimer = feedbackDuration;
        }

        if (feedbackTimer > 0f)
        {
            hintText.text = feedback;
            feedbackTimer = Mathf.Max(
                0f,
                feedbackTimer - Time.deltaTime
            );
        }
        else
        {
            hintText.text = player.CurrentPrompt;
        }
    }
}