using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Translates device input into motor calls (movement — immediate) and
/// queued commands (specials — delayed). The only class that touches the
/// Input System.
/// </summary>
[RequireComponent(typeof(PlayerMotor), typeof(CommandQueue))]
public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private InputActionAsset actions;

    // Commands are stateless, so one shared instance per type is enough.
    private static readonly JumpCommand jumpCommand = new JumpCommand();
    private static readonly DashCommand dashCommand = new DashCommand();

    private PlayerMotor motor;
    private CommandQueue queue;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        queue = GetComponent<CommandQueue>();
        playerMap = actions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
        dashAction = playerMap.FindAction("Dash", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        jumpAction.performed += OnJump;
        dashAction.performed += OnDash;
        playerMap.Enable();
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJump;
        dashAction.performed -= OnDash;
        playerMap.Disable();
    }

    private void Update()
    {
        // Movement is immediate by design — it never goes through the queue.
        motor.SetMoveInput(moveAction.ReadValue<Vector2>().x);
    }

    private void OnJump(InputAction.CallbackContext context) => queue.Enqueue(jumpCommand);
    private void OnDash(InputAction.CallbackContext context) => queue.Enqueue(dashCommand);
}
