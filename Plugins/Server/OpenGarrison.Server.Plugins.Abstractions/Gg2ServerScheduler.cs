namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerScheduledTaskInfo(
    Guid TimerId,
    string Description,
    bool IsRepeating,
    TimeSpan Interval,
    TimeSpan? DueIn);

public interface IOpenGarrisonServerScheduler
{
    TimeSpan Uptime { get; }

    Guid ScheduleOnce(TimeSpan delay, Action callback, string? description = null);

    Guid ScheduleRepeating(TimeSpan interval, Action callback, string? description = null, bool runImmediately = false);

    bool Cancel(Guid timerId);

    bool IsScheduled(Guid timerId);

    IReadOnlyList<OpenGarrisonServerScheduledTaskInfo> GetScheduledTasks();
}
