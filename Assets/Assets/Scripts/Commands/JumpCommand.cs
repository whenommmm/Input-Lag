public class JumpCommand : IPlayerCommand
{
    public CommandType Type => CommandType.Jump;
    public string DisplayLabel => "↑";

    public void Execute(PlayerMotor motor)
    {
        // Airborne at fire-time -> the action is wasted (locked design decision).
        if (motor.IsGrounded)
            motor.Jump();
    }
}
