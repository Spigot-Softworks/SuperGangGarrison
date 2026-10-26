#nullable enable

using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void HandleWelcomeMessage(WelcomeMessage welcome)
    {
        _gameplayManager.Session.HandleWelcomeMessage(welcome);
    }

    private void HandleGameplayContentSyncMessage(GameplayContentSyncMessage contentSync)
    {
        if (!GameplayReplicatedContent.TryDeserialize(contentSync.ContentUtf8Json, out var content, out var error))
        {
            AddNetworkConsoleLine($"ignored gameplay content from server: {error}");
            return;
        }

        // A resent sync replaces what the server sent before.
        var registry = CharacterClassCatalog.RuntimeRegistry;
        registry.RemoveOwnedRegistrations(GameplayRuntimeRegistry.ReplicatedContentOwnerKey);
        registry.ApplyReplicatedContent(content, GameplayRuntimeRegistry.ReplicatedContentOwnerKey);
        QueuePluginPackArtDownloads(content);
        AddNetworkConsoleLine(
            $"received gameplay content: {content.Classes.Count} classes, {content.Items.Count} items, {content.Loadouts.Count} loadouts");
    }

    private void HandleConnectionDeniedMessage(ConnectionDeniedMessage denied)
    {
        ReturnToMainMenuWithNetworkStatus(denied.Reason, $"connect denied: {denied.Reason}");
    }

    private void HandlePasswordRequestMessage()
    {
        OpenNetworkPasswordPrompt("Server requires a password.");
        AddNetworkConsoleLine("server requires a password");
    }

    private void HandlePasswordResultMessage(PasswordResultMessage passwordResult)
    {
        if (passwordResult.Accepted)
        {
            CloseNetworkPasswordPrompt();
            if (!_networkClient.IsSpectator && !IsWatchOnlySession())
            {
                OpenOnlineTeamSelection(clearPendingSelections: false, statusMessage: string.Empty);
            }

            AddNetworkConsoleLine("password accepted");
            return;
        }

        var rejectionReason = string.IsNullOrWhiteSpace(passwordResult.Reason) ? "Password rejected." : passwordResult.Reason;
        ReturnToMainMenuWithNetworkStatus(rejectionReason, $"password rejected: {passwordResult.Reason}");
    }
}
