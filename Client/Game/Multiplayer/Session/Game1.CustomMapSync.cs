#nullable enable

using System;
using System.Threading.Tasks;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum NetworkMapSyncStatus
    {
        Available,
        Pending,
        Failed,
    }

    private Task<CustomMapSyncService.CustomMapSyncResult>? _pendingNetworkMapSyncTask;
    private string _pendingNetworkMapSyncKey = string.Empty;
    private WelcomeMessage? _pendingWelcomeAfterNetworkMapSync;
    private readonly object _pendingNetworkMapSyncProgressGate = new();
    private string _pendingNetworkMapSyncProgressMessage = "Downloading custom map...";
    private double? _pendingNetworkMapSyncProgressValue;

    private void UpdatePendingNetworkMapSync()
    {
        var task = _pendingNetworkMapSyncTask;
        if (task is null)
        {
            return;
        }

        ApplyPendingNetworkMapSyncProgress();
        if (!task.IsCompleted)
        {
            return;
        }

        var pendingWelcome = _pendingWelcomeAfterNetworkMapSync;
        var result = GetCompletedNetworkMapSyncResult(task);
        ClearPendingNetworkMapSync();
        if (!result.Success)
        {
            ReturnToMainMenuWithNetworkStatus(result.Error, $"custom map sync failed: {result.Error}");
            return;
        }

        if (pendingWelcome is not null)
        {
            HandleWelcomeMessage(pendingWelcome);
        }
    }

    private void ClearPendingNetworkMapSync()
    {
        _pendingNetworkMapSyncTask = null;
        _pendingNetworkMapSyncKey = string.Empty;
        _pendingWelcomeAfterNetworkMapSync = null;
        HideLoadingOverlay();
        lock (_pendingNetworkMapSyncProgressGate)
        {
            _pendingNetworkMapSyncProgressMessage = string.Empty;
            _pendingNetworkMapSyncProgressValue = null;
        }
    }

    private void QueueWelcomeAfterNetworkMapSync(WelcomeMessage welcome)
    {
        _pendingWelcomeAfterNetworkMapSync = welcome;
    }

    private NetworkMapSyncStatus TryEnsureNetworkMapAvailable(
        string levelName,
        bool isCustomMap,
        string mapDownloadUrl,
        string mapContentHash,
        out string error)
    {
        error = string.Empty;
        if (!isCustomMap)
        {
            return NetworkMapSyncStatus.Available;
        }

        var syncKey = CreateNetworkMapSyncKey(levelName, mapDownloadUrl, mapContentHash);
        var task = _pendingNetworkMapSyncTask;
        if (task is not null)
        {
            if (!string.Equals(_pendingNetworkMapSyncKey, syncKey, StringComparison.Ordinal))
            {
                if (!task.IsCompleted)
                {
                    ApplyPendingNetworkMapSyncProgress();
                    return NetworkMapSyncStatus.Pending;
                }

                ClearPendingNetworkMapSync();
            }
            else
            {
                if (!task.IsCompleted)
                {
                    ApplyPendingNetworkMapSyncProgress();
                    return NetworkMapSyncStatus.Pending;
                }

                var completed = GetCompletedNetworkMapSyncResult(task);
                ClearPendingNetworkMapSync();
                if (completed.Success)
                {
                    return NetworkMapSyncStatus.Available;
                }

                error = completed.Error;
                return NetworkMapSyncStatus.Failed;
            }
        }

        StorePendingNetworkMapSyncProgress(new CustomMapSyncService.CustomMapSyncProgress("Downloading custom map...", null));
        var progress = new Progress<CustomMapSyncService.CustomMapSyncProgress>(StorePendingNetworkMapSyncProgress);
        task = CustomMapSyncService.EnsureMapAvailableAsync(
            levelName,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash,
            _networkClient.MapDownloadBaseUri,
            progress);
        if (task.IsCompleted)
        {
            var completed = GetCompletedNetworkMapSyncResult(task);
            if (completed.Success)
            {
                return NetworkMapSyncStatus.Available;
            }

            error = completed.Error;
            return NetworkMapSyncStatus.Failed;
        }

        _pendingNetworkMapSyncTask = task;
        _pendingNetworkMapSyncKey = syncKey;
        ApplyPendingNetworkMapSyncProgress();
        return NetworkMapSyncStatus.Pending;
    }

    private void StorePendingNetworkMapSyncProgress(CustomMapSyncService.CustomMapSyncProgress progress)
    {
        lock (_pendingNetworkMapSyncProgressGate)
        {
            _pendingNetworkMapSyncProgressMessage = string.IsNullOrWhiteSpace(progress.Message)
                ? "Downloading custom map..."
                : progress.Message;
            _pendingNetworkMapSyncProgressValue = progress.Progress;
        }
    }

    private void ApplyPendingNetworkMapSyncProgress()
    {
        string message;
        double? progress;
        lock (_pendingNetworkMapSyncProgressGate)
        {
            message = string.IsNullOrWhiteSpace(_pendingNetworkMapSyncProgressMessage)
                ? "Downloading custom map..."
                : _pendingNetworkMapSyncProgressMessage;
            progress = _pendingNetworkMapSyncProgressValue;
        }

        SetNetworkStatus(message);
        ShowLoadingOverlay(message, progress);
    }

    private static string CreateNetworkMapSyncKey(string levelName, string mapDownloadUrl, string mapContentHash)
    {
        return string.Concat(
            levelName.Trim(),
            "\n",
            mapDownloadUrl.Trim(),
            "\n",
            mapContentHash.Trim());
    }

    private static CustomMapSyncService.CustomMapSyncResult GetCompletedNetworkMapSyncResult(
        Task<CustomMapSyncService.CustomMapSyncResult> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return task.Result;
        }

        var message = task.Exception?.GetBaseException().Message;
        return CustomMapSyncService.CustomMapSyncResult.Fail(
            string.IsNullOrWhiteSpace(message)
                ? "Map package download failed."
                : $"Map package download failed: {message}");
    }
}
