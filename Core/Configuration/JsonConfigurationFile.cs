using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core;

public static class JsonConfigurationFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    static JsonConfigurationFile()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static T LoadOrCreate<T>(string path)
        where T : new()
    {
        return LoadOrCreate(path, static () => new T());
    }

    public static T LoadOrCreate<T>(string path, Func<T> defaultFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(defaultFactory);

        if (OperatingSystem.IsBrowser())
        {
            return defaultFactory();
        }

        if (!File.Exists(path))
        {
            var created = defaultFactory();
            Save(path, created);
            return created;
        }

        try
        {
            var json = ReadAllTextShared(path);
            var loaded = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            if (loaded is not null)
            {
                return loaded;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        var fallback = defaultFactory();
        Save(path, fallback);
        return fallback;
    }

    public static void Save<T>(string path, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            SaveText(path, json);
        }
        catch (IOException)
        {
            // Multiple client instances can share the same config path on one machine.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static bool TryReadText(string path, out string contents)
    {
        contents = string.Empty;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            contents = ReadAllTextShared(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void SaveText(string path, string contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            WriteAllTextAtomically(path, contents);
        }
        catch (IOException)
        {
            // Multiple client instances can share the same config path on one machine.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void WriteAllTextAtomically(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, contents);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
