using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public sealed record PendingRunUpload(string Id, string ClientId, string FriendCode, string Ruleset,
    byte[] Recording, string JobId = "", string Status = "pending", string Reason = "", string AttemptId = "");

public static class BrowserRunUploadStore
{
    public static Func<Task<string>>? LoadJson { get; set; }
    public static Func<string, string, Task>? SaveJson { get; set; }
    public static Func<string, Task>? Delete { get; set; }
}

/// <summary>Persists recordings before sending them and keeps rejected/failed proofs for recovery.</summary>
public sealed class RunUploadQueue
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly List<PendingRunUpload> _entries = new();
    private bool _loaded;
    private int _synchronizing;
    public string StatusText { get; private set; } = "";
    private readonly string _directoryPath;

    public RunUploadQueue(string? directoryPath = null)
    {
        _directoryPath = directoryPath ?? (OperatingSystem.IsBrowser() ? "" : Path.Combine(RuntimePaths.UserDataRoot, "pending-runs"));
    }

    private async Task LoadAsync()
    {
        if (_loaded) return;
        if (OperatingSystem.IsBrowser())
        {
            var load = BrowserRunUploadStore.LoadJson ?? throw new IOException("Browser run storage is unavailable.");
            foreach (var item in JsonSerializer.Deserialize(await load(), RunUploadJsonContext.Default.PendingRunUploadArray) ?? [])
                if (!_entries.Any(existing => existing.Id == item.Id)) _entries.Add(item);
        }
        else
        {
            Directory.CreateDirectory(_directoryPath);
            foreach (var path in Directory.EnumerateFiles(_directoryPath, "*.json"))
            {
                try
                {
                    if (new FileInfo(path).Length > 24 * 1024 * 1024) continue;
                    var item = JsonSerializer.Deserialize(await File.ReadAllTextAsync(path), RunUploadJsonContext.Default.PendingRunUpload);
                    if (item is not null && Guid.TryParseExact(item.Id, "N", out _)
                        && !_entries.Any(existing => existing.Id == item.Id)) _entries.Add(item);
                }
                catch (JsonException) { StatusText = "A saved run could not be read; its file has been kept."; }
            }
        }
        _loaded = true;
    }

    private async Task SaveAsync(PendingRunUpload item)
    {
        var json = JsonSerializer.Serialize(item, RunUploadJsonContext.Default.PendingRunUpload);
        if (OperatingSystem.IsBrowser())
        {
            var save = BrowserRunUploadStore.SaveJson ?? throw new IOException("Browser run storage is unavailable.");
            await save(item.Id, json);
            return;
        }
        Directory.CreateDirectory(_directoryPath);
        var path = Path.Combine(_directoryPath, item.Id + ".json");
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }

    public async Task AddAsync(string clientId, string friendCode, string ruleset, byte[] recording, string attemptId = "")
    {
        await _gate.WaitAsync();
        try
        {
            var item = new PendingRunUpload(Guid.NewGuid().ToString("N"), clientId, friendCode, ruleset, recording, AttemptId: attemptId);
            _entries.Add(item);
            await SaveAsync(item);
            StatusText = "Run saved. Waiting for leaderboard verification.";
        }
        finally { _gate.Release(); }
    }

    public async Task SynchronizeAsync(ClientIdentityDocument identity, OpenGarrisonPresenceClient api)
    {
        if (Interlocked.Exchange(ref _synchronizing, 1) != 0) return;
        try
        {
            PendingRunUpload? entry;
            await _gate.WaitAsync();
            try
            {
                await LoadAsync();
                entry = _entries.FirstOrDefault(item => item.ClientId == identity.ClientId
                    && item.FriendCode == identity.FriendCode && item.Status is not ("rejected" or "failed"));
                if (entry is null)
                {
                    var failed = _entries.FirstOrDefault(item => item.ClientId == identity.ClientId && item.FriendCode == identity.FriendCode);
                    if (failed is not null) StatusText = "Run kept locally: " + failed.Reason;
                    return;
                }
                // A pending co-op claim must not prevent later recordings from uploading.
                _entries.Remove(entry);
                _entries.Add(entry);
                await SaveAsync(entry);
            }
            finally { _gate.Release(); }

            // Do not hold the storage lock during a network request: another run can finish offline.
            var session = await api.CreateGameplaySessionAsync(identity);
            var result = entry.AttemptId.Length > 0
                ? await api.ClaimVerifiedRunAsync(session.GameplayToken, entry.AttemptId)
                : entry.JobId.Length == 0
                ? await api.UploadRunAsync(session.GameplayToken, entry.Ruleset, entry.Recording)
                : await api.GetRunUploadStatusAsync(session.GameplayToken, entry.JobId);
            await _gate.WaitAsync();
            try
            {
                var index = _entries.FindIndex(item => item.Id == entry.Id);
                if (index < 0) return;
                if (result.Status == "verified")
                {
                    if (OperatingSystem.IsBrowser())
                        await (BrowserRunUploadStore.Delete ?? throw new IOException("Browser run storage is unavailable."))(entry.Id);
                    else File.Delete(Path.Combine(_directoryPath, entry.Id + ".json"));
                    _entries.RemoveAt(index);
                    StatusText = "Run verified and added to the leaderboard.";
                }
                else
                {
                    var updated = entry with { JobId = result.Id, Status = result.Status, Reason = result.Reason };
                    await SaveAsync(updated);
                    _entries[index] = updated;
                    StatusText = result.Status is "rejected" or "failed"
                        ? "Run kept locally: " + result.Reason : "Run is waiting for leaderboard verification.";
                }
            }
            finally { _gate.Release(); }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or JsonException)
        {
            StatusText = "Run upload pending. It will retry when the service is available.";
        }
        finally { Volatile.Write(ref _synchronizing, 0); }
    }

}

public sealed record RunUploadStatus(string Id, string Status, string Reason);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PendingRunUpload))]
[JsonSerializable(typeof(PendingRunUpload[]))]
[JsonSerializable(typeof(RunUploadStatus))]
internal sealed partial class RunUploadJsonContext : JsonSerializerContext { }
