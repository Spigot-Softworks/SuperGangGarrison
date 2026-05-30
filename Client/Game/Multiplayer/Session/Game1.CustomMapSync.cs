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

    private void UpdatePendingNetworkMapSync()
    {
        var task = _pendingNetworkMapSyncTask;
        if (task is null || !task.IsCompleted)
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
        if (!OperatingSystem.IsBrowser())
        {
            return CustomMapSyncService.EnsureMapAvailable(
                    levelName,
                    isCustomMap,
                    mapDownloadUrl,
                    mapContentHash,
                    _networkClient.MapDownloadBaseUri,
                    out error)
                ? NetworkMapSyncStatus.Available
                : NetworkMapSyncStatus.Failed;
        }

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
                    SetNetworkStatus("Downloading custom map...");
                    return NetworkMapSyncStatus.Pending;
                }

                ClearPendingNetworkMapSync();
            }
            else
            {
                if (!task.IsCompleted)
                {
                    SetNetworkStatus("Downloading custom map...");
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

        task = CustomMapSyncService.EnsureMapAvailableAsync(
            levelName,
            isCustomMap,
            mapDownloadUrl,
            mapContentHash,
            _networkClient.MapDownloadBaseUri);
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
        SetNetworkStatus("Downloading custom map...");
        return NetworkMapSyncStatus.Pending;
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
