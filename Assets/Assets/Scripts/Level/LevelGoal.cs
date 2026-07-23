using UnityEngine;

/// <summary>Level-complete trigger (the visible green block).</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LevelGoal : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMotor>() == null) return;
        levelManager.GoalReached();
    }
}
