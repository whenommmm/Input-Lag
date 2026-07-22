public class DashCommand : IPlayerCommand
{
    public CommandType Type => CommandType.Dash;
    public string DisplayLabel => "→";

    public void Execute(PlayerMotor motor)
    {
        // Direction resolves at execution time: the player queues the WHEN
        // and steers the WHERE with immediate movement (locked design decision).
        motor.Dash(motor.FacingDirection);
    }
}
