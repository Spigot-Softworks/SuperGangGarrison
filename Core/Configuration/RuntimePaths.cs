using System;
using System.IO;

namespace OpenGarrison.Core;

public static class RuntimePaths
{
    public static string ApplicationRoot => AppContext.BaseDirectory;

    public static string AssetsDirectory => Path.Combine(ApplicationRoot, "Assets");

    public static string ConfigDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "config");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string MapsDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "Maps");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string LogsDirectory
    {
        get
        {
            var path = Path.Combine(ApplicationRoot, "logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string GetConfigPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(ConfigDirectory, fileName);
    }

    public static string GetLogPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(LogsDirectory, fileName);
    }
}
