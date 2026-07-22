using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The countdown heart of the game. Accepts commands instantly, waits
/// delaySeconds, then executes them FIFO. Fully generic: its only job is
/// wait -> execute -> remove -> fire events. It never knows whether a
/// command's internal validity check passed.
/// </summary>
public class CommandQueue : MonoBehaviour
{
    [Tooltip("Level-wide execution delay in seconds. This is the per-level difficulty knob.")]
    [SerializeField] private float delaySeconds = 2f;
    [SerializeField] private int maxQueueSize = 3;
    [SerializeField] private PlayerMotor motor;

    private readonly List<QueuedCommand> entries = new List<QueuedCommand>();

    public IReadOnlyList<QueuedCommand> Entries => entries;
    public int MaxQueueSize => maxQueueSize;

    public event Action<QueuedCommand> CommandQueued;
    public event Action<QueuedCommand> CommandExecuted;
    public event Action<IPlayerCommand> CommandRejected;

    /// <summary>
    /// Returns false (and fires CommandRejected) when the queue is full.
    /// Existing entries are never overwritten.
    /// </summary>
    public bool Enqueue(IPlayerCommand command)
    {
        if (entries.Count >= maxQueueSize)
        {
            CommandRejected?.Invoke(command);
            return false;
        }

        var entry = new QueuedCommand(command, Time.time + delaySeconds);
        entries.Add(entry);
        CommandQueued?.Invoke(entry);
        return true;
    }

    private void Update()
    {
        // The delay is constant per level, so entries are always in ExecuteAt
        // order and FIFO falls out for free. Frame-granularity firing (~16 ms
        // late worst case) is acceptable per spec.
        while (entries.Count > 0 && entries[0].ExecuteAt <= Time.time)
        {
            QueuedCommand entry = entries[0];
            entries.RemoveAt(0);
            entry.Command.Execute(motor);
            CommandExecuted?.Invoke(entry);
        }
    }
}

/// <summary>A command paired with its execution timestamp.</summary>
public readonly struct QueuedCommand
{
    public IPlayerCommand Command { get; }
    public float ExecuteAt { get; }
    public float Remaining => Mathf.Max(0f, ExecuteAt - Time.time);

    public QueuedCommand(IPlayerCommand command, float executeAt)
    {
        Command = command;
        ExecuteAt = executeAt;
    }
}
