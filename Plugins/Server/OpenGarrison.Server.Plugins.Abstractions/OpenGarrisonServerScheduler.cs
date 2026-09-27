namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Snapshot of a scheduled server task.
/// </summary>
/// <param name="TimerId">The timer id.</param>
/// <param name="Description">The task description.</param>
/// <param name="IsRepeating">Whether the task repeats.</param>
/// <param name="Interval">The repeat interval.</param>
/// <param name="DueIn">The time until the next run, if known.</param>
public readonly record struct OpenGarrisonServerScheduledTaskInfo(
    Guid TimerId,
    string Description,
    bool IsRepeating,
    TimeSpan Interval,
    TimeSpan? DueIn);

/// <summary>
/// Scheduler plugins use to run one-shot and repeating callbacks.
/// </summary>
public interface IOpenGarrisonServerScheduler
{
    /// <summary>
    /// Gets the server uptime.
    /// </summary>
    TimeSpan Uptime { get; }

    /// <summary>
    /// Schedules a callback to run once after a delay.
    /// </summary>
    /// <param name="delay">The delay before the callback runs.</param>
    /// <param name="callback">The callback.</param>
    /// <param name="description">The task description.</param>
    /// <returns>The timer id.</returns>
    Guid ScheduleOnce(TimeSpan delay, Action callback, string? description = null);

    /// <summary>
    /// Schedules a callback to run repeatedly at an interval.
    /// </summary>
    /// <param name="interval">The repeat interval.</param>
    /// <param name="callback">The callback.</param>
    /// <param name="description">The task description.</param>
    /// <param name="runImmediately">Whether to run the callback immediately.</param>
    /// <returns>The timer id.</returns>
    Guid ScheduleRepeating(TimeSpan interval, Action callback, string? description = null, bool runImmediately = false);

    /// <summary>
    /// Cancels a scheduled task.
    /// </summary>
    /// <param name="timerId">The timer id.</param>
    /// <returns>True when a scheduled task was cancelled.</returns>
    bool Cancel(Guid timerId);

    /// <summary>
    /// Gets whether a timer id is currently scheduled.
    /// </summary>
    /// <param name="timerId">The timer id.</param>
    /// <returns>True when the timer is scheduled.</returns>
    bool IsScheduled(Guid timerId);

    /// <summary>
    /// Gets snapshots of all scheduled tasks.
    /// </summary>
    /// <returns>The scheduled tasks.</returns>
    IReadOnlyList<OpenGarrisonServerScheduledTaskInfo> GetScheduledTasks();
}
