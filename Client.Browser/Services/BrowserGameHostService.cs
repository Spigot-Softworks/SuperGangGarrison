using Microsoft.JSInterop;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Threading;

namespace OpenGarrison.Client.Browser.Services;

public sealed class BrowserGameHostService : IDisposable, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private OpenGarrison.Client.Game1? _game;
    private DotNetObjectReference<BrowserGameHostService>? _inputBridgeReference;
    private bool _started;
    private bool _failed;
    private string? _statusMessage;
    private bool _framePumpInitialized;
    private int _framePumpInProgress;
    private long _lastLoggedBrowserPerfSampleCount;
    private long _managedPumpSamples;
    private double _managedPumpTotalMilliseconds;
    private double _lastManagedPumpMilliseconds;

    public event Action? StateChanged;

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(BrowserGameHostService))]
    public BrowserGameHostService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public bool IsStarted => _started;
    public bool HasFailed => _failed;
    public string StatusMessage => _statusMessage ?? (_started ? "Real client startup requested." : "Not started.");

    public void SetStatus(string statusMessage)
    {
        if (_started || _failed)
        {
            return;
        }

        _statusMessage = statusMessage;
        NotifyStateChanged();
    }

    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _statusMessage = "Constructing real client...";
        await PublishStateAsync();
        try
        {
            ClientRuntimeBootstrap.InitializeBrowserHttpClient(_httpClient);
            await _jsRuntime.InvokeVoidAsync("OpenGarrisonBrowserHost.focusCanvas");
            var jsCanvasStatus = await _jsRuntime.InvokeAsync<string>("OpenGarrisonBrowserHost.describeCanvas");
            var canvas = nkast.Wasm.Dom.Window.Current.Document.GetElementById<nkast.Wasm.Canvas.Canvas>("theCanvas");
            if (canvas is null)
            {
                _statusMessage = $"KNI canvas lookup returned null for #theCanvas before client construction. JS sees: {jsCanvasStatus}";
                await PublishStateAsync();
                return;
            }

            _statusMessage = $"Loading browser gameplay definitions. Canvas {canvas.Width}x{canvas.Height}. JS sees: {jsCanvasStatus}";
            await PublishStateAsync();
            var browserBaseAddress = ClientRuntimeBootstrap.GetBrowserBaseAddress();
            var gameplayCatalogTask = BrowserStockGameplayModCatalogLoader.EnsureLoadedAsync(_httpClient);
            var runtimeManifestTask = BrowserGameMakerAssetManifestLoader.LoadAsync(_httpClient);
            var bootstrapCatalogTask = BrowserBootstrapAssetCatalog.LoadDefaultAsync(_httpClient);
            var runtimeBundleTask = BrowserAssetBundleLoader.TryLoadAsync(
                _httpClient,
                BrowserDistributionPaths.RuntimeAssetBundlePath,
                browserBaseAddress);
            var clientPluginBundleTask = BrowserAssetBundleLoader.TryLoadAsync(
                _httpClient,
                BrowserDistributionPaths.ClientPluginBundlePath,
                browserBaseAddress);
            var bootstrapAtlasManifestTask = BrowserAtlasManifestLoader.LoadBootstrapAsync(_httpClient);
            var stockGameplayAtlasManifestTask = BrowserAtlasManifestLoader.LoadStockGameplayAsync(_httpClient);
            var gameMakerAtlasManifestTask = BrowserAtlasManifestLoader.LoadGameMakerAsync(_httpClient);

            await Task.WhenAll(
                    gameplayCatalogTask,
                    runtimeManifestTask,
                    bootstrapCatalogTask,
                    runtimeBundleTask,
                    clientPluginBundleTask,
                    bootstrapAtlasManifestTask,
                    stockGameplayAtlasManifestTask,
                    gameMakerAtlasManifestTask)
                .ConfigureAwait(false);

            var browserRuntimeAssetManifest = runtimeManifestTask.Result;
            ClientRuntimeBootstrap.SetBrowserRuntimeAssetManifest(browserRuntimeAssetManifest);
            var browserBootstrapAssets = bootstrapCatalogTask.Result;
            ClientRuntimeBootstrap.SetBrowserBootstrapAssetCatalog(browserBootstrapAssets);
            var bootstrapAtlasManifest = bootstrapAtlasManifestTask.Result;
            var stockGameplayAtlasManifest = stockGameplayAtlasManifestTask.Result;
            var gameMakerAtlasManifest = gameMakerAtlasManifestTask.Result;
            ClientRuntimeBootstrap.SetBrowserBootstrapAtlasManifest(bootstrapAtlasManifest);
            ClientRuntimeBootstrap.SetBrowserStockGameplayAtlasManifest(stockGameplayAtlasManifest);
            ClientRuntimeBootstrap.SetBrowserGameMakerAtlasManifest(gameMakerAtlasManifest);
            BrowserContentCatalog.SetBinaryAssets(browserBootstrapAssets.GetBinaryAssets());
            BrowserContentCatalog.AddOrUpdateBinaryAssets(runtimeBundleTask.Result);
            BrowserContentCatalog.AddOrUpdateBinaryAssets(clientPluginBundleTask.Result);
            await PreloadAtlasPageBytesAsync(
                    _httpClient,
                    bootstrapAtlasManifest,
                    stockGameplayAtlasManifest.Manifest,
                    gameMakerAtlasManifest.Manifest)
                .ConfigureAwait(false);
            Console.WriteLine("Browser host: stock gameplay definitions loaded.");
            _statusMessage = $"Constructing real client with canvas {canvas.Width}x{canvas.Height}. JS sees: {jsCanvasStatus}";
            await PublishStateAsync();
            _game = new OpenGarrison.Client.Game1();
            Console.WriteLine("Browser host: Game1 constructor completed.");
            _statusMessage = "Starting browser game loop...";
            _started = true;
            await PublishStateAsync();
            _inputBridgeReference = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync("OpenGarrisonBrowserHost.attachInputBridge", _inputBridgeReference);
            _statusMessage = "Real client run loop scheduled.";
            await PublishStateAsync();
            _framePumpInitialized = false;
            _lastLoggedBrowserPerfSampleCount = 0;
            await _jsRuntime.InvokeVoidAsync("OpenGarrisonBrowserHost.startGameLoop", _inputBridgeReference);
        }
        catch (Exception ex)
        {
            _failed = true;
            _started = false;
            _statusMessage = BuildExceptionSummary(ex);
            await PublishStateAsync();
            TryDisposeGame();
            _game = null;
        }
    }

    [JSInvokable("PumpFrame")]
    public void PumpFrame()
    {
        if (Interlocked.Exchange(ref _framePumpInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            if (_failed || _game is null)
            {
                return;
            }

            if (!_framePumpInitialized)
            {
                Console.WriteLine("Browser host: entering Game.RunOneFrame().");
                _statusMessage = "Real client run loop active.";
                NotifyStateChanged();
                var managedPumpStartTimestamp = Stopwatch.GetTimestamp();
                _game.RunOneFrame();
                _game.EnsureBrowserHostLifecycleInitialized();
                RecordManagedPumpDuration(managedPumpStartTimestamp);
                _framePumpInitialized = true;
                return;
            }

            var managedTickStartTimestamp = Stopwatch.GetTimestamp();
            _game.EnsureBrowserHostLifecycleInitialized();
            _game.Tick();
            RecordManagedPumpDuration(managedTickStartTimestamp);
            LogBrowserPerformanceIfNeeded();
        }
        catch (Exception ex)
        {
            _failed = true;
            _started = false;
            var lastRenderTrace = OpenGarrison.Client.Game1.GetBrowserLastGameplayRenderTrace();
            var recentRenderTraces = OpenGarrison.Client.Game1.GetBrowserRecentGameplayRenderTraces();
            _statusMessage = $"{BuildExceptionSummary(ex)} | lastRenderTrace={lastRenderTrace} | recentRenderTraces={recentRenderTraces}";
            Console.WriteLine($"Browser host pump failure. lastRenderTrace={lastRenderTrace}");
            Console.WriteLine($"Browser host pump failure. recentRenderTraces={recentRenderTraces}");
            NotifyStateChanged();
            TryDisposeGame();
            _game = null;
            _ = HandlePumpFailureAsync();
        }
        finally
        {
            Volatile.Write(ref _framePumpInProgress, 0);
        }
    }

    [JSInvokable("PumpFrameAsync")]
    public Task PumpFrameAsync()
    {
        PumpFrame();
        return Task.CompletedTask;
    }

    // These callbacks are invoked through the instance DotNetObjectReference passed to JS.
#pragma warning disable CA1822
    [JSInvokable("HandleBrowserMouseMove")]
    public void HandleBrowserMouseMove(int x, int y)
    {
        OpenGarrison.Client.BrowserInputBridge.SetMousePosition(x, y);
    }

    [JSInvokable("HandleBrowserMouseButton")]
    public void HandleBrowserMouseButton(int button, bool pressed, int x, int y)
    {
        OpenGarrison.Client.BrowserInputBridge.SetMousePosition(x, y);
        OpenGarrison.Client.BrowserInputBridge.SetMouseButton(button, pressed);
    }

    [JSInvokable("HandleBrowserWheel")]
    public void HandleBrowserWheel(int deltaY, int x, int y)
    {
        OpenGarrison.Client.BrowserInputBridge.SetMousePosition(x, y);
        OpenGarrison.Client.BrowserInputBridge.AddWheelDelta(-deltaY);
    }

    [JSInvokable("HandleBrowserKey")]
    public void HandleBrowserKey(string code, bool pressed)
    {
        if (!TryMapBrowserKey(code, out var key))
        {
            return;
        }

        OpenGarrison.Client.BrowserInputBridge.SetKey(key, pressed);
    }

    [JSInvokable("HandleBrowserText")]
    public void HandleBrowserText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var character in text)
        {
            OpenGarrison.Client.BrowserInputBridge.EnqueueTextInput(character);
        }
    }

    [JSInvokable("HandleBrowserFocus")]
    public void HandleBrowserFocus(bool focused)
    {
        OpenGarrison.Client.BrowserInputBridge.SetFocus(focused);
    }
#pragma warning restore CA1822

    [JSInvokable("GetAutomationState")]
    public OpenGarrison.Client.Game1.BrowserAutomationSnapshot GetAutomationState()
    {
        return _game?.GetBrowserAutomationSnapshot()
            ?? OpenGarrison.Client.Game1.BrowserAutomationSnapshot.Empty;
    }

    [JSInvokable("RunAutomationConsoleCommand")]
    public bool RunAutomationConsoleCommand(string commandText)
    {
        return _game?.TryRunBrowserAutomationConsoleCommand(commandText) == true;
    }

    [JSInvokable("InvokeAutomationAction")]
    public bool InvokeAutomationAction(string actionSet, string label)
    {
        return _game?.TryInvokeBrowserAutomationAction(actionSet, label) == true;
    }

    [JSInvokable("GetPerformanceSnapshot")]
    public OpenGarrison.Client.Game1.BrowserPerformanceSnapshot GetPerformanceSnapshot()
    {
        return _game?.GetBrowserPerformanceSnapshot()
            ?? default;
    }

    public void Dispose()
    {
        _ = StopGameLoopForDisposeAsync();
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await StopGameLoopForDisposeAsync().ConfigureAwait(false);
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    private void DisposeCore()
    {
        _inputBridgeReference?.Dispose();
        _inputBridgeReference = null;
        TryDisposeGame();
        _game = null;
        _started = false;
        _failed = false;
        _framePumpInitialized = false;
        _lastLoggedBrowserPerfSampleCount = 0;
        _managedPumpSamples = 0;
        _managedPumpTotalMilliseconds = 0d;
        _lastManagedPumpMilliseconds = 0d;
        NotifyStateChanged();
    }

    private async Task StopGameLoopForDisposeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("OpenGarrisonBrowserHost.stopGameLoop")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Browser host: ignored stopGameLoop during dispose: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string BuildExceptionSummary(Exception exception)
    {
        return exception.ToString()
            .Replace("\r\n", " | ")
            .Replace("\n", " | ");
    }

    private async ValueTask PublishStateAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "OpenGarrisonBrowserHost.setManagedState",
                _statusMessage ?? "Not started.",
                _started,
                _failed);
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException or JSDisconnectedException)
        {
            Console.WriteLine($"Browser host: ignored managed-state publish failure: {ex.GetType().Name}: {ex.Message}");
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private async Task HandlePumpFailureAsync()
    {
        await PublishStateAsync();
        try
        {
            await _jsRuntime.InvokeVoidAsync("OpenGarrisonBrowserHost.stopGameLoop");
        }
        catch (Exception stopEx) when (stopEx is TaskCanceledException or OperationCanceledException or JSDisconnectedException)
        {
            Console.WriteLine($"Browser host: ignored stopGameLoop failure: {stopEx.GetType().Name}: {stopEx.Message}");
        }
    }

    private void TryDisposeGame()
    {
        if (_game is null)
        {
            return;
        }

        try
        {
            _game.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Browser host: ignored dispose exception: {ex.Message}");
        }
    }

    private void LogBrowserPerformanceIfNeeded()
    {
        if (_game is null)
        {
            return;
        }

        var snapshot = _game.GetBrowserPerformanceSnapshot();
        if (snapshot.Samples < 120 || snapshot.Samples - _lastLoggedBrowserPerfSampleCount < 120)
        {
            return;
        }

        _lastLoggedBrowserPerfSampleCount = snapshot.Samples;
        var averageManagedPumpMilliseconds = _managedPumpSamples > 0
            ? _managedPumpTotalMilliseconds / _managedPumpSamples
            : 0d;
        Console.WriteLine(
            $"{snapshot.ToLogLine()} managedPump(last/avg)={_lastManagedPumpMilliseconds:0.0}/{averageManagedPumpMilliseconds:0.0}ms");
    }

    private void RecordManagedPumpDuration(long startTimestamp)
    {
        _lastManagedPumpMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
        _managedPumpSamples += 1;
        _managedPumpTotalMilliseconds += _lastManagedPumpMilliseconds;
    }

    private static async Task PreloadAtlasPageBytesAsync(HttpClient httpClient, params BrowserAtlasManifest[] manifests)
    {
        var pagePaths = manifests
            .SelectMany(static manifest => manifest.Atlases)
            .Select(static page => page.ImagePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (pagePaths.Length == 0)
        {
            return;
        }

        var pageLoadTasks = pagePaths
            .Select(async path =>
            {
                try
                {
                    var bytes = await httpClient.GetByteArrayAsync(path).ConfigureAwait(false);
                    return bytes.Length > 0
                        ? new KeyValuePair<string, byte[]>(path, bytes)
                        : default;
                }
                catch
                {
                    return default;
                }
            })
            .ToArray();
        var pages = (await Task.WhenAll(pageLoadTasks).ConfigureAwait(false))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is { Length: > 0 })
            .ToArray();
        if (pages.Length > 0)
        {
            BrowserContentCatalog.AddOrUpdateBinaryAssets(pages);
        }

        Console.WriteLine($"Browser host: preloaded {pages.Length}/{pagePaths.Length} atlas pages.");
    }

    private static bool TryMapBrowserKey(string? code, out Keys key)
    {
        key = code switch
        {
            "ArrowUp" => Keys.Up,
            "ArrowDown" => Keys.Down,
            "ArrowLeft" => Keys.Left,
            "ArrowRight" => Keys.Right,
            "Escape" => Keys.Escape,
            "Enter" => Keys.Enter,
            "Space" => Keys.Space,
            "Tab" => Keys.Tab,
            "Backspace" => Keys.Back,
            "Delete" => Keys.Delete,
            "Insert" => Keys.Insert,
            "Home" => Keys.Home,
            "End" => Keys.End,
            "PageUp" => Keys.PageUp,
            "PageDown" => Keys.PageDown,
            "CapsLock" => Keys.CapsLock,
            "ShiftLeft" => Keys.LeftShift,
            "ShiftRight" => Keys.RightShift,
            "ControlLeft" => Keys.LeftControl,
            "ControlRight" => Keys.RightControl,
            "AltLeft" => Keys.LeftAlt,
            "AltRight" => Keys.RightAlt,
            "Backquote" => Keys.OemTilde,
            "Minus" => Keys.OemMinus,
            "Equal" => Keys.OemPlus,
            "BracketLeft" => Keys.OemOpenBrackets,
            "BracketRight" => Keys.OemCloseBrackets,
            "Backslash" => Keys.OemPipe,
            "Semicolon" => Keys.OemSemicolon,
            "Quote" => Keys.OemQuotes,
            "Comma" => Keys.OemComma,
            "Period" => Keys.OemPeriod,
            "Slash" => Keys.OemQuestion,
            _ => Keys.None,
        };

        if (key != Keys.None)
        {
            return true;
        }

        if (TryMapBrowserLetterKey(code, out key)
            || TryMapBrowserDigitKey(code, out key)
            || TryMapBrowserNumpadKey(code, out key)
            || TryMapBrowserFunctionKey(code, out key))
        {
            return true;
        }

        key = Keys.None;
        return false;
    }

    private static bool TryMapBrowserLetterKey(string? code, out Keys key)
    {
        key = Keys.None;
        if (string.IsNullOrWhiteSpace(code)
            || code.Length != 4
            || !code.StartsWith("Key", StringComparison.Ordinal)
            || !char.IsAsciiLetter(code[3]))
        {
            return false;
        }

        return Enum.TryParse(code[3].ToString(), ignoreCase: true, out key) && key != Keys.None;
    }

    private static bool TryMapBrowserDigitKey(string? code, out Keys key)
    {
        key = Keys.None;
        if (string.IsNullOrWhiteSpace(code)
            || code.Length != 6
            || !code.StartsWith("Digit", StringComparison.Ordinal)
            || !char.IsAsciiDigit(code[5]))
        {
            return false;
        }

        return Enum.TryParse("D" + code[5], ignoreCase: false, out key) && key != Keys.None;
    }

    private static bool TryMapBrowserNumpadKey(string? code, out Keys key)
    {
        key = code switch
        {
            "Numpad0" => Keys.NumPad0,
            "Numpad1" => Keys.NumPad1,
            "Numpad2" => Keys.NumPad2,
            "Numpad3" => Keys.NumPad3,
            "Numpad4" => Keys.NumPad4,
            "Numpad5" => Keys.NumPad5,
            "Numpad6" => Keys.NumPad6,
            "Numpad7" => Keys.NumPad7,
            "Numpad8" => Keys.NumPad8,
            "Numpad9" => Keys.NumPad9,
            "NumpadAdd" => Keys.Add,
            "NumpadSubtract" => Keys.Subtract,
            "NumpadMultiply" => Keys.Multiply,
            "NumpadDivide" => Keys.Divide,
            "NumpadDecimal" => Keys.Decimal,
            "NumpadEnter" => Keys.Enter,
            _ => Keys.None,
        };

        return key != Keys.None;
    }

    private static bool TryMapBrowserFunctionKey(string? code, out Keys key)
    {
        key = Keys.None;
        if (string.IsNullOrWhiteSpace(code)
            || code.Length < 3
            || code[0] != 'F'
            || !int.TryParse(code[1..], out var functionNumber))
        {
            return false;
        }

        return functionNumber is >= 1 and <= 24
            && Enum.TryParse("F" + functionNumber, ignoreCase: false, out key)
            && key != Keys.None;
    }

}
