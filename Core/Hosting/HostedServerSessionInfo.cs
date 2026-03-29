using System;
using System.IO;
using System.Text.Json;

namespace OpenGarrison.Core;

public sealed class HostedServerSessionInfo
{
    public const string DefaultFileName = "hosted-server-session.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public int ProcessId { get; set; }

    public int Port { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public string PipeName { get; set; } = string.Empty;

    public string ConfigPath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string LaunchMode { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public static string GetDefaultPath()
    {
        return RuntimePaths.GetConfigPath(DefaultFileName);
    }

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
        File.WriteAllText(resolvedPath, JsonSerializer.Serialize(this, SerializerOptions));
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
