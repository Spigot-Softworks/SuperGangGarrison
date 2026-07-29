#nullable enable

using System.IO;

namespace OpenGarrison.Core;

public sealed class BubbleWheelPluginConfig
{
    public const string DefaultFileName = "bubblewheel.json";

    public BubbleWheelBehavior Behavior { get; set; } = OpenGarrisonPreferencesDocument.DefaultBubbleWheelBehavior;

    public static BubbleWheelPluginConfig LoadOrCreate(string path, BubbleWheelBehavior? legacyBehavior = null)
    {
        var createdNew = !File.Exists(path);
        BubbleWheelPluginConfig config;
        if (createdNew)
        {
            config = new BubbleWheelPluginConfig
            {
                Behavior = OpenGarrisonPreferencesDocument.NormalizeBubbleWheelBehavior(
                    legacyBehavior ?? OpenGarrisonPreferencesDocument.DefaultBubbleWheelBehavior),
            };
        }
        else
        {
            config = Load(path);
        }

        var behaviorBeforeNormalize = config.Behavior;
        config.Normalize();
        if (createdNew || behaviorBeforeNormalize != config.Behavior)
        {
            Save(path, config);
        }

        return config;
    }

    public static BubbleWheelPluginConfig Load(string path)
    {
        BubbleWheelPluginConfig config;
        try
        {
            config = JsonConfigurationFile.LoadOrCreate(path, static () => new BubbleWheelPluginConfig());
        }
        catch (IOException)
        {
            config = new BubbleWheelPluginConfig();
        }
        catch (UnauthorizedAccessException)
        {
            config = new BubbleWheelPluginConfig();
        }

        config.Normalize();
        return config;
    }

    public static void Save(string path, BubbleWheelPluginConfig config)
    {
        config.Normalize();
        JsonConfigurationFile.Save(path, config);
    }

    private void Normalize()
    {
        Behavior = OpenGarrisonPreferencesDocument.NormalizeBubbleWheelBehavior(Behavior);
    }
}
