using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the play loop: death -> YOU DIED banner -> respawn at the
/// active checkpoint; goal -> LEVEL COMPLETE banner -> next scene (or scene
/// restart). The state machine makes overlapping trigger events no-ops by
/// construction: only Playing accepts kill/checkpoint/goal notifications.
/// </summary>
public class LevelManager : MonoBehaviour
{
    private enum State { Playing, Dying, Winning }

    [SerializeField] private PlayerMotor motor;
    [SerializeField] private CommandQueue queue;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private BannerUI banner;
    [SerializeField] private Checkpoint initialCheckpoint;
    [Tooltip("Scene to load on level complete. Empty = restart this scene.")]
    [SerializeField] private string nextSceneName = "";

    private static readonly Color DeathStripColor = new Color(0.55f, 0.05f, 0.05f, 0.8f);
    private static readonly Color WinStripColor = new Color(0.05f, 0.45f, 0.12f, 0.8f);

    private State state = State.Playing;
    private Checkpoint activeCheckpoint;

    private void Awake()
    {
        activeCheckpoint = initialCheckpoint;
    }

    public void PlayerDied()
    {
        if (state != State.Playing) return;
        state = State.Dying;
        CutControl();
        banner.Play("YOU DIED", DeathStripColor, RespawnAtCheckpoint, RestoreControl);
    }

    public void CheckpointReached(Checkpoint checkpoint)
    {
        if (state != State.Playing) return;
        activeCheckpoint = checkpoint; // last one touched wins
    }

    public void GoalReached()
    {
        if (state != State.Playing) return;
        state = State.Winning;
        CutControl();
        banner.Play("LEVEL COMPLETE", WinStripColor, null, LoadNextScene);
    }

    private void CutControl()
    {
        // Disabling the handler unsubscribes and disables the action map
        // (its OnDisable already does both). A death wipes your plan.
        inputHandler.enabled = false;
        motor.SetMoveInput(0f);
        queue.Clear();
    }

    private void RestoreControl()
    {
        inputHandler.enabled = true;
        state = State.Playing;
    }

    private void RespawnAtCheckpoint()
    {
        motor.Teleport(activeCheckpoint.transform.position);
        cameraFollow.SnapToTarget();
    }

    private void LoadNextScene()
    {
        string scene = string.IsNullOrEmpty(nextSceneName)
            ? SceneManager.GetActiveScene().name
            : nextSceneName;
        SceneManager.LoadScene(scene);
    }
}
