/// <summary>
/// A queueable player action. Validity is checked internally at execution
/// time: an invalid command simply does nothing (e.g. Jump while airborne
/// is wasted). The queue stays generic — it never knows why a command
/// succeeded or failed.
/// </summary>
public interface IPlayerCommand
{
    CommandType Type { get; }

    /// <summary>UI display only — never use for identity or logic.</summary>
    string DisplayLabel { get; }

    void Execute(PlayerMotor motor);
}
