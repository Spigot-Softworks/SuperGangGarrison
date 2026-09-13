using System.Text.Json;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public sealed class FirstPlayHintsDocument
{
    public const string FileName = "first-play-hints.json";
    public const string BrowserKey = "first-play-hints-v1";
    public bool HasShown { get; set; }

    public static FirstPlayHintsDocument Load(string? path = null) => OperatingSystem.IsBrowser()
        ? LoadBrowser()
        : JsonConfigurationFile.LoadOrCreate<FirstPlayHintsDocument>(path ?? RuntimePaths.GetConfigPath(FileName));

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser()) SaveBrowser();
        else JsonConfigurationFile.Save(path ?? RuntimePaths.GetConfigPath(FileName), this);
    }

    internal static FirstPlayHintsDocument LoadBrowser()
    {
        try
        {
            if (BrowserPreferenceStore.Read(BrowserKey) is { } json)
                return JsonSerializer.Deserialize(json, BrowserPreferencesJsonContext.Default.FirstPlayHintsDocument) ?? new();
        }
        catch (JsonException) { }
        return new();
    }

    internal void SaveBrowser() => BrowserPreferenceStore.Write(BrowserKey,
        JsonSerializer.Serialize(this, BrowserPreferencesJsonContext.Default.FirstPlayHintsDocument));
}
