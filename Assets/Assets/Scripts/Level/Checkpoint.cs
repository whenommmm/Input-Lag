using UnityEngine;

/// <summary>
/// Invisible respawn point. Walking through it makes it the active respawn
/// (last one touched wins). Visible only as an editor gizmo — players never
/// see it.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMotor>() == null) return;
        levelManager.CheckpointReached(this);
    }

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
    }
}
