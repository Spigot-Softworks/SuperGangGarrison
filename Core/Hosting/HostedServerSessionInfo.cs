using System;
using System.IO;
using System.Text.Json;

namespace OpenGarrison.Core;

public sealed class HostedServerSessionInfo
{
    public const string DefaultFileName = "hosted-server-session.json";
    public const string SessionPathEnvironmentVariable = "OPENGARRISON_HOST_SESSION_PATH";
    public const string InstanceEnvironmentVariable = "OPENGARRISON_HOST_INSTANCE_ID";
    private static readonly string DirectInstanceId = Guid.NewGuid().ToString("N");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public int ProcessId { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public int OwnerProcessId { get; set; }
    public long OwnerStartTimeUtcTicks { get; set; }
    public bool IsReady { get; set; }
    public string DiagnosticsDirectory { get; set; } = string.Empty;

    public long ProcessStartTimeUtcTicks { get; set; }

    public int Port { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public string PipeName { get; set; } = string.Empty;

    public string ConfigPath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string LaunchMode { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public static string GetDefaultPath()
    {
        var configured = Environment.GetEnvironmentVariable(SessionPathEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configured) ? GetInstancePath(GetCurrentInstanceId()) : configured;
    }

    public static string GetCurrentInstanceId()
        => Environment.GetEnvironmentVariable(InstanceEnvironmentVariable) is { Length: > 0 } value
            && Guid.TryParseExact(value, "N", out _) ? value : DirectInstanceId;

    public static string GetInstancePath(string instanceId)
    {
        if (!Guid.TryParseExact(instanceId, "N", out _)) throw new ArgumentException("Invalid host instance ID.", nameof(instanceId));
        return RuntimePaths.GetConfigPath(Path.Combine("servers", instanceId, DefaultFileName));
    }

    public bool IsOwnedBy(string instanceId, HostedServerProcessIdentity owner)
        => owner.IsValid && InstanceId == instanceId
            && OwnerProcessId == owner.ProcessId && OwnerStartTimeUtcTicks == owner.StartTimeUtcTicks
            && string.Equals(LaunchMode, "launcher", StringComparison.OrdinalIgnoreCase);

    public static HostedServerSessionInfo? Load(string? path = null)
    {
        var resolvedPath = path ?? GetDefaultPath();
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(resolvedPath);
            return JsonSerializer.Deserialize<HostedServerSessionInfo>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(string? path = null)
    {
        var resolvedPath = path ?? GetDefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath) ?? RuntimePaths.ConfigDirectory);
        var temporaryPath = resolvedPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, SerializerOptions));
            File.Move(temporaryPath, resolvedPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static void DeleteIfMatching(HostedServerSessionInfo expected, string? path = null)
    {
        var current = Load(path);
        if (current is not null && current.InstanceId == expected.InstanceId
            && current.ProcessId == expected.ProcessId
            && current.ProcessStartTimeUtcTicks == expected.ProcessStartTimeUtcTicks)
            Delete(path);
    }

    public static void Delete(string? path = null)
    {
        var resolvedPath = path ?? GetDefaultPath();
        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
        }
    }
}
