using UnityEngine;

/// <summary>Trigger below the map: falling in kills the player.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class KillZone : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMotor>() == null) return;
        levelManager.PlayerDied();
    }
}
