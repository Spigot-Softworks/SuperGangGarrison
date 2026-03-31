using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client.Plugins.RandomBackgrounds;

public sealed class RandomBackgroundsPlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientMainMenuHooks
{
    private string? _selectedBackgroundPath;
    private string _selectedAttributionText = string.Empty;

    public string Id => "randombackgrounds";

    public string DisplayName => "Random Backgrounds";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        var backgroundsDirectory = Path.Combine(context.PluginDirectory, "Resources", "PrOF", "Backgrounds");
        if (!Directory.Exists(backgroundsDirectory))
        {
            context.Log($"background directory missing at {backgroundsDirectory}");
            return;
        }

        var availableBackgrounds = Directory.EnumerateFiles(backgroundsDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (availableBackgrounds.Length == 0)
        {
            context.Log($"no background images found at {backgroundsDirectory}");
            return;
        }

        _selectedBackgroundPath = availableBackgrounds[Random.Shared.Next(availableBackgrounds.Length)];
        _selectedAttributionText = GetAttributionText(Path.GetFileName(_selectedBackgroundPath));
    }

    public void Shutdown()
    {
        _selectedBackgroundPath = null;
        _selectedAttributionText = string.Empty;
    }

    public ClientPluginMainMenuBackgroundOverride? GetMainMenuBackgroundOverride()
    {
        return string.IsNullOrWhiteSpace(_selectedBackgroundPath)
            ? null
            : new ClientPluginMainMenuBackgroundOverride(_selectedBackgroundPath, _selectedAttributionText);
    }

    private static string GetAttributionText(string? fileName)
    {
        return fileName switch
        {
            "Right_behind_you_Conan.png" => "\"Right Behind You\" by Conan",
            "The_red_gang_sy.png" => "\"The Red Gang\" by sy",
            "Just_within_reach_sven.png" => "\"Just Within Reach\" by Sven",
            "Soldier_finger_haxton.png" => "\"Soldier pointing his finger\" by Haxton Sale",
            "Outclasses_zaspai.png" => "\"Outclassed\" by ZaSpai",
            "Sentry_nest_conan.png" => "\"Sentry Nest\" by Conan",
            "archibaldes_no_melasos.png" => "\"Archibaldes, no!\" by Melasos",
            "hotline_garrison_poop.png" => "\"Hotline Garrison 2\" by Poop",
            "aftermath_natsu.png" => "\"Aftermath\" by Natsu",
            _ => string.Empty,
        };
    }
}
