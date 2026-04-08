using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerScheduler(
    Func<TimeSpan> uptimeGetter,
    Action<string> log) : IOpenGarrisonServerScheduler
{
    private readonly Dictionary<Guid, ScheduledTask> _tasksById = [];

    public TimeSpan Uptime => uptimeGetter();

    public Guid ScheduleOnce(TimeSpan delay, Action callback, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var normalizedDelay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        var timerId = Guid.NewGuid();
        _tasksById[timerId] = new ScheduledTask(
            timerId,
            description?.Trim() ?? string.Empty,
            callback,
            normalizedDelay,
            IsRepeating: false,
            Uptime + normalizedDelay);
        return timerId;
    }

    public Guid ScheduleRepeating(TimeSpan interval, Action callback, string? description = null, bool runImmediately = false)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Repeating interval must be greater than zero.");
        }

        var timerId = Guid.NewGuid();
        _tasksById[timerId] = new ScheduledTask(
            timerId,
            description?.Trim() ?? string.Empty,
            callback,
            interval,
            IsRepeating: true,
            runImmediately ? Uptime : Uptime + interval);
        return timerId;
    }

    public bool Cancel(Guid timerId)
    {
        return _tasksById.Remove(timerId);
    }

    public bool IsScheduled(Guid timerId)
    {
        return _tasksById.ContainsKey(timerId);
    }

    public IReadOnlyList<OpenGarrisonServerScheduledTaskInfo> GetScheduledTasks()
    {
        var now = Uptime;
        return _tasksById.Values
            .OrderBy(task => task.NextRunAt)
            .Select(task => new OpenGarrisonServerScheduledTaskInfo(
                task.TimerId,
                task.Description,
                task.IsRepeating,
                task.Interval,
                task.NextRunAt <= now ? TimeSpan.Zero : task.NextRunAt - now))
            .ToArray();
    }

    public void RunDueTasks()
    {
        if (_tasksById.Count == 0)
        {
            return;
        }

        var now = Uptime;
        var dueTasks = _tasksById.Values
            .Where(task => task.NextRunAt <= now)
            .OrderBy(task => task.NextRunAt)
            .ToArray();
        foreach (var task in dueTasks)
        {
            if (!_tasksById.ContainsKey(task.TimerId))
            {
                continue;
            }

            try
            {
                task.Callback();
            }
            catch (Exception ex)
            {
                log($"[server] scheduled task {task.TimerId} failed: {ex.Message}");
            }

            if (task.IsRepeating && _tasksById.ContainsKey(task.TimerId))
            {
                _tasksById[task.TimerId] = task with
                {
                    NextRunAt = now + task.Interval,
                };
            }
            else
            {
                _tasksById.Remove(task.TimerId);
            }
        }
    }

    private sealed record ScheduledTask(
        Guid TimerId,
        string Description,
        Action Callback,
        TimeSpan Interval,
        bool IsRepeating,
        TimeSpan NextRunAt);
}
