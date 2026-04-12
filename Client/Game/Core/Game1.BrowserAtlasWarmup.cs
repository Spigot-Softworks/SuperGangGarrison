#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Queue<BrowserGameplayAtlasWarmupItem> _browserGameplayAtlasWarmupQueue = new();
    private Task<bool>? _browserGameplayAtlasWarmupTask;
    private BrowserGameplayAtlasWarmupItem? _browserGameplayAtlasWarmupCurrentItem;
    private int _browserGameplayAtlasWarmupCompletedCount;
    private int _browserGameplayAtlasWarmupTotalCount;
    private bool _browserGameplayAtlasWarmupStarted;
    private bool _browserGameplayAtlasWarmupComplete;

    private readonly record struct BrowserGameplayAtlasWarmupItem(string RelativePath, string Label);

    private void BeginBrowserGameplayWarmup()
    {
        if (!OperatingSystem.IsBrowser() || _browserGameplayAtlasWarmupStarted)
        {
            return;
        }

        _browserGameplayAtlasWarmupStarted = true;
        _browserGameplayAtlasWarmupQueue.Clear();
        _browserGameplayAtlasWarmupCompletedCount = 0;
        _browserGameplayAtlasWarmupTotalCount = 0;
        _browserGameplayAtlasWarmupTask = null;
        _browserGameplayAtlasWarmupCurrentItem = null;
        _browserGameplayAtlasWarmupComplete = false;

        if (!ShouldEagerWarmBrowserGameplayAtlases())
        {
            _browserGameplayAtlasWarmupComplete = true;
            return;
        }

        if (_gameplayModAssets is null)
        {
            _browserGameplayAtlasWarmupComplete = true;
            return;
        }

        foreach (var relativePath in _gameplayModAssets.GetBrowserAtlasPagePaths())
        {
            _browserGameplayAtlasWarmupQueue.Enqueue(new BrowserGameplayAtlasWarmupItem(relativePath, "stock gameplay atlas"));
        }

        _browserGameplayAtlasWarmupTotalCount = _browserGameplayAtlasWarmupQueue.Count;
        if (_browserGameplayAtlasWarmupTotalCount == 0)
        {
            _browserGameplayAtlasWarmupComplete = true;
        }
    }

    private bool AdvanceBrowserGameplayWarmup()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return true;
        }

        if (!_browserGameplayAtlasWarmupStarted)
        {
            BeginBrowserGameplayWarmup();
        }

        if (_browserGameplayAtlasWarmupComplete)
        {
            return true;
        }

        if (_browserGameplayAtlasWarmupTask is null)
        {
            if (_browserGameplayAtlasWarmupQueue.Count == 0)
            {
                _browserGameplayAtlasWarmupComplete = true;
                AddConsoleLine($"browser gameplay atlas warmup complete ({_browserGameplayAtlasWarmupCompletedCount}/{_browserGameplayAtlasWarmupTotalCount})");
                return true;
            }

            _browserGameplayAtlasWarmupCurrentItem = _browserGameplayAtlasWarmupQueue.Dequeue();
            _browserGameplayAtlasWarmupTask = _gameplayModAssets?.WarmBrowserAtlasPageAsync(_browserGameplayAtlasWarmupCurrentItem.Value.RelativePath)
                ?? Task.FromResult(false);
            return false;
        }

        if (!_browserGameplayAtlasWarmupTask.IsCompleted)
        {
            return false;
        }

        try
        {
            var warmed = _browserGameplayAtlasWarmupTask.GetAwaiter().GetResult();
            if (!warmed && _browserGameplayAtlasWarmupCurrentItem is { } failedItem)
            {
                AddConsoleLine($"browser atlas warmup failed for {failedItem.Label}: {failedItem.RelativePath}");
            }
        }
        catch (Exception ex)
        {
            if (_browserGameplayAtlasWarmupCurrentItem is { } failedItem)
            {
                AddConsoleLine($"browser atlas warmup exception for {failedItem.Label}: {failedItem.RelativePath} ({ex.Message})");
            }
        }

        _browserGameplayAtlasWarmupCompletedCount += 1;
        _browserGameplayAtlasWarmupTask = null;
        _browserGameplayAtlasWarmupCurrentItem = null;

        if (_browserGameplayAtlasWarmupQueue.Count == 0)
        {
            _browserGameplayAtlasWarmupComplete = true;
            AddConsoleLine($"browser gameplay atlas warmup complete ({_browserGameplayAtlasWarmupCompletedCount}/{_browserGameplayAtlasWarmupTotalCount})");
            return true;
        }

        return false;
    }

    private bool IsBrowserGameplayWarmupComplete()
    {
        return !OperatingSystem.IsBrowser() || _browserGameplayAtlasWarmupComplete;
    }

    private string GetBrowserGameplayWarmupStatusMessage()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return string.Empty;
        }

        if (_browserGameplayAtlasWarmupTotalCount <= 0)
        {
            return "Browser gameplay textures are still warming up.";
        }

        return $"Browser gameplay textures are still warming up ({_browserGameplayAtlasWarmupCompletedCount}/{_browserGameplayAtlasWarmupTotalCount}).";
    }

    private static bool ShouldEagerWarmBrowserGameplayAtlases()
    {
        return AppContext.TryGetSwitch("OpenGarrison.Browser.EagerGameplayAtlasWarmup", out var enabled) && enabled;
    }
}
