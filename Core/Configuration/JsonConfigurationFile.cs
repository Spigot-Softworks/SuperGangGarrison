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

        if (!File.Exists(path))
        {
            var created = defaultFactory();
            Save(path, created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(path);
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

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, SerializerOptions);
        File.WriteAllText(path, json);
    }
}
