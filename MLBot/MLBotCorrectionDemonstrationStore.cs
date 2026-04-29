using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public static class MLBotCorrectionDemonstrationStore
{
    private const string DemonstrationDirectoryName = "mlbot-dagger-demos";

    public static string ResolveWritablePath(MLBotDemonstrationMetadata metadata)
    {
        return MLBotDemonstrationStoragePaths.ResolveWritablePath(DemonstrationDirectoryName, metadata);
    }

    public static void Save(MLBotDemonstrationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Metadata);

        JsonConfigurationFile.Save(ResolveWritablePath(document.Metadata), document);
    }
}
