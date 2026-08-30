using System;
using System.Diagnostics;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>
/// Identifies a process by both PID and creation time so a reused PID cannot
/// be mistaken for the process that owns a hosted server session.
/// </summary>
public readonly record struct HostedServerProcessIdentity(int ProcessId, long StartTimeUtcTicks)
{
    public const string ParentProcessIdEnvironmentVariable = "OPENGARRISON_HOST_PARENT_PID";
    public const string ParentProcessStartTimeEnvironmentVariable = "OPENGARRISON_HOST_PARENT_START_TIME_UTC_TICKS";

    public bool IsValid => ProcessId > 0 && StartTimeUtcTicks > 0;

    public static bool TryCaptureCurrent(out HostedServerProcessIdentity identity)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return TryCapture(process, out identity);
        }
        catch
        {
            identity = default;
            return false;
        }
    }

    public static bool TryCapture(Process process, out HostedServerProcessIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            var startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
            identity = new HostedServerProcessIdentity(process.Id, startTimeUtcTicks);
            return identity.IsValid;
        }
        catch
        {
            identity = default;
            return false;
        }
    }

    public bool Matches(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return process.Id == ProcessId
            && TryCapture(process, out var actual)
            && actual.StartTimeUtcTicks == StartTimeUtcTicks;
    }

    public static bool TryReadParentFromEnvironment(out HostedServerProcessIdentity identity)
    {
        var pidText = Environment.GetEnvironmentVariable(ParentProcessIdEnvironmentVariable);
        var startTimeText = Environment.GetEnvironmentVariable(ParentProcessStartTimeEnvironmentVariable);
        if (!int.TryParse(pidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId)
            || !long.TryParse(startTimeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startTimeUtcTicks))
        {
            identity = default;
            return false;
        }

        identity = new HostedServerProcessIdentity(processId, startTimeUtcTicks);
        return identity.IsValid;
    }

    public void ApplyParentEnvironment(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        startInfo.Environment[ParentProcessIdEnvironmentVariable] =
            ProcessId.ToString(CultureInfo.InvariantCulture);
        startInfo.Environment[ParentProcessStartTimeEnvironmentVariable] =
            StartTimeUtcTicks.ToString(CultureInfo.InvariantCulture);
    }
}
