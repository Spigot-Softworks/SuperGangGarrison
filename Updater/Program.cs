using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

const string DefaultManifestUrl = "https://api.unkind-dev.com/updates/windows-x64/stable/latest.json";
const string GameExecutableName = "OG2.exe";
const string LauncherExecutableName = "OG2.Launcher.exe";
const string VersionFileName = "version.txt";
const string ApplyUpdateArgument = "--apply-update";

if (args.Length > 0 && string.Equals(args[0], ApplyUpdateArgument, StringComparison.Ordinal))
{
    await RunApplyUpdateModeAsync(args).ConfigureAwait(false);
    return;
}

var appDirectory = AppContext.BaseDirectory;
var manifestUrl = Environment.GetEnvironmentVariable("OPENGARRISON_UPDATE_MANIFEST");
if (string.IsNullOrWhiteSpace(manifestUrl))
{
    manifestUrl = DefaultManifestUrl;
}

using var updateUi = UpdateUi.Create();
var launchGame = true;
try
{
    var result = await TryApplyUpdateAsync(appDirectory, manifestUrl, args, updateUi).ConfigureAwait(false);
    launchGame = result != UpdateApplyResult.DelegatedToHelper;
}
catch (OperationCanceledException)
{
    updateUi.Report(UpdateUiState.KnownProgress("Update canceled. Launching installed version...", 0d));
    await Task.Delay(450).ConfigureAwait(false);
}
catch
{
    // Launch should remain reliable even when the updater endpoint is offline or a package is bad.
    if (updateUi.IsVisible)
    {
        updateUi.Report(UpdateUiState.KnownProgress("Update failed. Launching installed version...", 0d));
        await Task.Delay(900).ConfigureAwait(false);
    }
}

if (launchGame)
{
    LaunchGame(appDirectory, args);
}

static async Task RunApplyUpdateModeAsync(string[] args)
{
    if (args.Length < 5)
    {
        return;
    }

    var sourceDirectory = args[1];
    var destinationDirectory = args[2];
    var version = args[3];
    _ = int.TryParse(args[4], out var parentProcessId);
    var gameArgs = GetArgumentsAfterSeparator(args, startIndex: 5);

    using var updateUi = UpdateUi.Create();
    try
    {
        updateUi.Show();
        updateUi.Report(UpdateUiState.Indeterminate("Installing update..."));
        WaitForProcessExit(parentProcessId, TimeSpan.FromSeconds(30));

        CopyUpdatePayload(
            sourceDirectory,
            destinationDirectory,
            progress => updateUi.Report(UpdateUiState.KnownProgress("Installing update...", progress)));

        File.WriteAllText(Path.Combine(destinationDirectory, VersionFileName), version.Trim());
        updateUi.Report(UpdateUiState.KnownProgress("Launching updated version...", 1d));
        await Task.Delay(300).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        updateUi.Report(UpdateUiState.KnownProgress("Update canceled. Launching installed version...", 0d));
        await Task.Delay(450).ConfigureAwait(false);
    }
    catch
    {
        updateUi.Report(UpdateUiState.KnownProgress("Update failed. Launching installed version...", 0d));
        await Task.Delay(900).ConfigureAwait(false);
    }

    LaunchGame(destinationDirectory, gameArgs);
}

static string[] GetArgumentsAfterSeparator(string[] args, int startIndex)
{
    for (var index = startIndex; index < args.Length; index += 1)
    {
        if (string.Equals(args[index], "--", StringComparison.Ordinal))
        {
            return args[(index + 1)..];
        }
    }

    return Array.Empty<string>();
}

static void WaitForProcessExit(int processId, TimeSpan timeout)
{
    if (processId <= 0)
    {
        return;
    }

    try
    {
        using var process = Process.GetProcessById(processId);
        process.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue));
    }
    catch
    {
        // If the parent is already gone or cannot be queried, continue with the install.
    }
}

static async Task<UpdateApplyResult> TryApplyUpdateAsync(
    string appDirectory,
    string manifestUrl,
    string[] gameArgs,
    IUpdateUi updateUi)
{
    using var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    var manifest = await httpClient.GetFromJsonAsync<UpdateManifest>(manifestUrl).ConfigureAwait(false);
    if (manifest is null || string.IsNullOrWhiteSpace(manifest.Url) || string.IsNullOrWhiteSpace(manifest.Version))
    {
        return UpdateApplyResult.NoUpdate;
    }

    var currentVersion = ReadCurrentVersion(appDirectory);
    if (!IsNewerVersion(manifest.Version, currentVersion))
    {
        return UpdateApplyResult.NoUpdate;
    }

    updateUi.Show();
    updateUi.Report(UpdateUiState.KnownProgress("Starting update...", 0d));

    var packageUri = new Uri(new Uri(manifestUrl), manifest.Url);
    var tempRoot = Path.Combine(Path.GetTempPath(), "OpenGarrisonUpdate");
    var packagePath = Path.Combine(tempRoot, "package.zip");
    var extractPath = Path.Combine(tempRoot, "extract");
    if (Directory.Exists(tempRoot))
    {
        Directory.Delete(tempRoot, recursive: true);
    }

    Directory.CreateDirectory(tempRoot);
    await DownloadPackageAsync(httpClient, packageUri, packagePath, manifest.Size, updateUi).ConfigureAwait(false);

    updateUi.Report(UpdateUiState.Indeterminate("Verifying update..."));
    if (!string.IsNullOrWhiteSpace(manifest.Sha256)
        && !string.Equals(await ComputeSha256Async(packagePath).ConfigureAwait(false), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
    {
        updateUi.Report(UpdateUiState.KnownProgress("Update verification failed.", 0d));
        await Task.Delay(900).ConfigureAwait(false);
        return UpdateApplyResult.NoUpdate;
    }

    updateUi.Report(UpdateUiState.Indeterminate("Preparing update..."));
    ZipFile.ExtractToDirectory(packagePath, extractPath);

    if (TryLaunchUpdateHelper(extractPath, appDirectory, manifest.Version, gameArgs))
    {
        updateUi.Report(UpdateUiState.KnownProgress("Installing update...", 1d));
        await Task.Delay(250).ConfigureAwait(false);
        return UpdateApplyResult.DelegatedToHelper;
    }

    CopyUpdatePayload(
        extractPath,
        appDirectory,
        progress => updateUi.Report(UpdateUiState.KnownProgress("Installing update...", progress)));
    File.WriteAllText(Path.Combine(appDirectory, VersionFileName), manifest.Version.Trim());
    return UpdateApplyResult.AppliedInProcess;
}

static async Task DownloadPackageAsync(
    HttpClient httpClient,
    Uri packageUri,
    string packagePath,
    long manifestSize,
    IUpdateUi updateUi)
{
    using var response = await httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault(manifestSize);
    var stopwatch = Stopwatch.StartNew();
    var downloadedBytes = 0L;
    var buffer = new byte[128 * 1024];

    await using var packageStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    await using var fileStream = File.Create(packagePath);
    while (true)
    {
        updateUi.ThrowIfCancellationRequested();
        var bytesRead = await packageStream.ReadAsync(buffer).ConfigureAwait(false);
        if (bytesRead <= 0)
        {
            break;
        }

        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
        downloadedBytes += bytesRead;

        if (totalBytes > 0)
        {
            var progress = Math.Clamp(downloadedBytes / (double)totalBytes, 0d, 1d);
            updateUi.Report(UpdateUiState.KnownProgress(FormatDownloadStatus(downloadedBytes, totalBytes, stopwatch.Elapsed), progress));
        }
        else
        {
            updateUi.Report(UpdateUiState.Indeterminate($"Downloading update... {FormatBytes(downloadedBytes)}"));
        }
    }

    updateUi.Report(UpdateUiState.KnownProgress("Download complete.", 1d));
}

static bool TryLaunchUpdateHelper(string extractPath, string appDirectory, string version, string[] gameArgs)
{
    if (!OperatingSystem.IsWindows())
    {
        return false;
    }

    var helperPath = Path.Combine(extractPath, LauncherExecutableName);
    if (!File.Exists(helperPath))
    {
        return false;
    }

    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            WorkingDirectory = extractPath,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(ApplyUpdateArgument);
        startInfo.ArgumentList.Add(extractPath);
        startInfo.ArgumentList.Add(appDirectory);
        startInfo.ArgumentList.Add(version);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--");
        foreach (var argument in gameArgs)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) is not null;
    }
    catch
    {
        return false;
    }
}

static string FormatDownloadStatus(long downloadedBytes, long totalBytes, TimeSpan elapsed)
{
    var remainingText = "Estimating time remaining...";
    if (downloadedBytes > 0 && elapsed.TotalSeconds > 0.5d)
    {
        var bytesPerSecond = downloadedBytes / elapsed.TotalSeconds;
        if (bytesPerSecond > 0d)
        {
            var remainingSeconds = Math.Max(0d, (totalBytes - downloadedBytes) / bytesPerSecond);
            var minutes = (int)Math.Floor(remainingSeconds / 60d);
            var seconds = (int)Math.Round(remainingSeconds - (minutes * 60));
            if (seconds >= 60)
            {
                minutes += 1;
                seconds = 0;
            }

            remainingText = $"{minutes} minutes {seconds} seconds Remaining...";
        }
    }

    return remainingText;
}

static string FormatBytes(long bytes)
{
    if (bytes >= 1024L * 1024L)
    {
        return $"{bytes / (1024d * 1024d):0.0} MB";
    }

    if (bytes >= 1024L)
    {
        return $"{bytes / 1024d:0.0} KB";
    }

    return $"{bytes} B";
}

static string ReadCurrentVersion(string appDirectory)
{
    var versionPath = Path.Combine(appDirectory, VersionFileName);
    if (File.Exists(versionPath))
    {
        return File.ReadAllText(versionPath).Trim();
    }

    var gamePath = Path.Combine(appDirectory, GameExecutableName);
    if (File.Exists(gamePath))
    {
        var version = FileVersionInfo.GetVersionInfo(gamePath).ProductVersion;
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version.Trim();
        }
    }

    return "0.0.0";
}

static bool IsNewerVersion(string candidate, string current)
{
    if (!TryParseComparableVersion(candidate, out var candidateVersion))
    {
        return false;
    }

    if (!TryParseComparableVersion(current, out var currentVersion))
    {
        currentVersion = new Version(0, 0, 0);
    }

    return candidateVersion > currentVersion;
}

static bool TryParseComparableVersion(string value, out Version version)
{
    version = new Version(0, 0, 0);
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var clean = value.Trim().TrimStart('v', 'V');
    var suffixIndex = clean.IndexOfAny(['-', '+']);
    if (suffixIndex >= 0)
    {
        clean = clean[..suffixIndex];
    }

    return Version.TryParse(clean, out version!);
}

static async Task<string> ComputeSha256Async(string path)
{
    await using var stream = File.OpenRead(path);
    var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static void CopyUpdatePayload(string sourceDirectory, string destinationDirectory, Action<double>? reportProgress)
{
    var directories = Directory
        .EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories)
        .Where(directory => !ShouldPreserveLocalPath(Path.GetRelativePath(sourceDirectory, directory)))
        .ToArray();

    for (var index = 0; index < directories.Length; index += 1)
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, directories[index]);
        Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        reportProgress?.Invoke(directories.Length == 0 ? 0d : index / (double)(directories.Length + 1));
    }

    var files = Directory
        .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
        .Where(file => !ShouldPreserveLocalPath(Path.GetRelativePath(sourceDirectory, file)))
        .ToArray();

    for (var index = 0; index < files.Length; index += 1)
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, files[index]);
        var destinationPath = Path.Combine(destinationDirectory, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDirectory);
        File.Copy(files[index], destinationPath, overwrite: true);
        reportProgress?.Invoke(files.Length == 0 ? 1d : Math.Clamp((index + 1) / (double)files.Length, 0d, 1d));
    }
}

static bool ShouldPreserveLocalPath(string relativePath)
{
    var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
    return firstSegment.Equals("config", StringComparison.OrdinalIgnoreCase)
        || firstSegment.Equals("logs", StringComparison.OrdinalIgnoreCase);
}

static void LaunchGame(string appDirectory, string[] args)
{
    var gamePath = Path.Combine(appDirectory, GameExecutableName);
    if (!File.Exists(gamePath))
    {
        return;
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = gamePath,
        WorkingDirectory = appDirectory,
        UseShellExecute = true,
    };
    foreach (var argument in args)
    {
        startInfo.ArgumentList.Add(argument);
    }

    Process.Start(startInfo);
}

internal enum UpdateApplyResult
{
    NoUpdate,
    AppliedInProcess,
    DelegatedToHelper,
}

internal sealed class UpdateManifest
{
    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Size { get; set; }

    public string MinLauncherVersion { get; set; } = string.Empty;

    public string NotesUrl { get; set; } = string.Empty;
}

internal interface IUpdateUi : IDisposable
{
    bool IsVisible { get; }

    void Show();

    void Report(UpdateUiState state);

    void ThrowIfCancellationRequested();
}

internal sealed record UpdateUiState(string Message, double? Progress)
{
    public static UpdateUiState KnownProgress(string message, double progress)
    {
        return new UpdateUiState(message, Math.Clamp(progress, 0d, 1d));
    }

    public static UpdateUiState Indeterminate(string message)
    {
        return new UpdateUiState(message, null);
    }
}

internal static class UpdateUi
{
    public static IUpdateUi Create()
    {
        return OperatingSystem.IsWindows()
            ? new WindowsUpdateUi()
            : new ConsoleUpdateUi();
    }
}

internal sealed class ConsoleUpdateUi : IUpdateUi
{
    private bool _isVisible;
    private UpdateUiState _lastState = UpdateUiState.Indeterminate(string.Empty);

    public bool IsVisible => _isVisible;

    public void Show()
    {
        _isVisible = true;
    }

    public void Report(UpdateUiState state)
    {
        _lastState = state;
        if (!_isVisible)
        {
            return;
        }

        var suffix = state.Progress.HasValue
            ? $" {state.Progress.Value:P0}"
            : string.Empty;
        Console.Error.WriteLine($"[updater] {state.Message}{suffix}");
    }

    public void ThrowIfCancellationRequested()
    {
    }

    public void Dispose()
    {
        if (_isVisible && !string.IsNullOrWhiteSpace(_lastState.Message))
        {
            Console.Error.WriteLine($"[updater] {_lastState.Message}");
        }
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsUpdateUi : IUpdateUi
{
    private const int WindowWidth = 384;
    private const int WindowHeight = 108;
    private const int WmDestroy = 0x0002;
    private const int WmPaint = 0x000F;
    private const int WmClose = 0x0010;
    private const int WmKeyDown = 0x0100;
    private const int WmLButtonDown = 0x0201;
    private const int WmAppClose = 0x8001;
    private const int VkEscape = 0x1B;
    private const int SwShow = 5;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int CsHRedraw = 0x0002;
    private const int CsVRedraw = 0x0001;
    private const int DtLeft = 0x00000000;
    private const int DtCenter = 0x00000001;
    private const int DtVCenter = 0x00000004;
    private const int DtSingleLine = 0x00000020;

    private static readonly int BackgroundColor = ColorRef(54, 51, 50);
    private static readonly int BorderColor = ColorRef(119, 119, 119);
    private static readonly int ProgressColor = ColorRef(69, 108, 140);
    private static readonly int ButtonColor = ColorRef(85, 80, 79);
    private static readonly int ButtonHighlightColor = ColorRef(136, 136, 136);
    private static readonly int ButtonShadowColor = ColorRef(34, 34, 34);
    private static readonly int WhiteColor = ColorRef(255, 255, 255);

    private static readonly ConcurrentDictionary<IntPtr, WindowsUpdateUi> WindowsByHandle = new();

    private readonly object _sync = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private readonly string _className = $"OpenGarrisonUpdaterWindow_{Guid.NewGuid():N}";
    private readonly WndProc _wndProc;

    private IntPtr _handle;
    private bool _disposed;
    private bool _isVisible;
    private bool _cancellationRequested;
    private UpdateUiState _state = UpdateUiState.Indeterminate("Starting update...");

    public WindowsUpdateUi()
    {
        _wndProc = WindowProcedure;
        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "OpenGarrison updater UI",
        };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    public bool IsVisible
    {
        get
        {
            lock (_sync)
            {
                return _isVisible;
            }
        }
    }

    public void Show()
    {
        lock (_sync)
        {
            if (_disposed || _isVisible)
            {
                return;
            }

            _isVisible = true;
            _thread.Start();
        }

        _ready.Wait(TimeSpan.FromSeconds(2));
    }

    public void Report(UpdateUiState state)
    {
        lock (_sync)
        {
            _state = state;
        }

        if (_handle != IntPtr.Zero)
        {
            InvalidateRect(_handle, IntPtr.Zero, true);
        }
    }

    public void ThrowIfCancellationRequested()
    {
        lock (_sync)
        {
            if (_cancellationRequested)
            {
                throw new OperationCanceledException();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        if (_handle != IntPtr.Zero)
        {
            PostMessage(_handle, WmAppClose, IntPtr.Zero, IntPtr.Zero);
        }

        if (_thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }
    }

    private void RunMessageLoop()
    {
        var instance = GetModuleHandle(null);
        var windowClass = new WndClass
        {
            style = CsHRedraw | CsVRedraw,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = instance,
            hCursor = LoadCursor(IntPtr.Zero, 32512),
            hbrBackground = IntPtr.Zero,
            lpszClassName = _className,
        };

        RegisterClass(ref windowClass);

        var screenWidth = GetSystemMetrics(0);
        var screenHeight = GetSystemMetrics(1);
        var left = Math.Max(0, (screenWidth - WindowWidth) / 2);
        var top = Math.Max(0, (screenHeight - WindowHeight) / 2);

        _handle = CreateWindowEx(
            0,
            _className,
            "Smoke - Updating",
            WsPopup,
            left,
            top,
            WindowWidth,
            WindowHeight,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            _ready.Set();
            return;
        }

        WindowsByHandle[_handle] = this;
        ShowWindow(_handle, SwShow);
        UpdateWindow(_handle);
        _ready.Set();

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmPaint:
                Paint(hwnd);
                return IntPtr.Zero;
            case WmLButtonDown:
                if (IsCancelButtonHit(lParam))
                {
                    RequestCancellation();
                }

                return IntPtr.Zero;
            case WmKeyDown:
                if (wParam.ToInt32() == VkEscape)
                {
                    RequestCancellation();
                }

                return IntPtr.Zero;
            case WmClose:
                RequestCancellation();
                return IntPtr.Zero;
            case WmAppClose:
                DestroyWindow(hwnd);
                return IntPtr.Zero;
            case WmDestroy:
                WindowsByHandle.TryRemove(hwnd, out _);
                PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private static bool IsCancelButtonHit(IntPtr lParam)
    {
        var packed = lParam.ToInt32();
        var x = packed & 0xFFFF;
        var y = (packed >> 16) & 0xFFFF;
        return x >= 275 && x <= 365 && y >= 61 && y <= 83;
    }

    private void RequestCancellation()
    {
        lock (_sync)
        {
            _cancellationRequested = true;
            _state = UpdateUiState.KnownProgress("Canceling update...", _state.Progress ?? 0d);
        }

        if (_handle != IntPtr.Zero)
        {
            InvalidateRect(_handle, IntPtr.Zero, true);
        }
    }

    private void Paint(IntPtr hwnd)
    {
        var paintStruct = new PaintStruct
        {
            rgbReserved = new byte[32],
        };
        var hdc = BeginPaint(hwnd, ref paintStruct);
        try
        {
            UpdateUiState state;
            lock (_sync)
            {
                state = _state;
            }

            FillRect(hdc, new Rect(0, 0, WindowWidth, WindowHeight), BackgroundColor);
            DrawTextLine(hdc, "Smoke - Updating", new Rect(26, 7, 260, 24), WhiteColor, DtLeft | DtSingleLine | DtVCenter);
            DrawTextLine(hdc, state.Message, new Rect(26, 38, 268, 60), WhiteColor, DtLeft | DtSingleLine | DtVCenter);
            DrawProgressFrame(hdc);
            DrawProgressSegments(hdc, state.Progress);
            DrawCancelButton(hdc);
        }
        finally
        {
            EndPaint(hwnd, ref paintStruct);
        }
    }

    private static void DrawProgressFrame(IntPtr hdc)
    {
        FillRect(hdc, new Rect(26, 61, 267, 62), BorderColor);
        FillRect(hdc, new Rect(26, 82, 267, 83), BorderColor);
        FillRect(hdc, new Rect(26, 61, 27, 83), BorderColor);
        FillRect(hdc, new Rect(266, 61, 267, 83), BorderColor);
        FillRect(hdc, new Rect(27, 62, 266, 82), BackgroundColor);
    }

    private static void DrawProgressSegments(IntPtr hdc, double? progress)
    {
        var segmentCount = progress.HasValue
            ? Math.Clamp((int)Math.Floor(progress.Value * 20d), 0, 20)
            : 1;
        for (var index = 0; index < segmentCount; index += 1)
        {
            var left = 26 + (index * 12);
            FillRect(hdc, new Rect(left, 65, left + 8, 79), ProgressColor);
        }
    }

    private static void DrawCancelButton(IntPtr hdc)
    {
        FillRect(hdc, new Rect(275, 61, 365, 83), ButtonColor);
        FillRect(hdc, new Rect(275, 61, 365, 62), ButtonHighlightColor);
        FillRect(hdc, new Rect(275, 82, 365, 83), ButtonShadowColor);
        FillRect(hdc, new Rect(275, 61, 276, 83), ButtonHighlightColor);
        FillRect(hdc, new Rect(364, 61, 365, 83), ButtonShadowColor);
        DrawTextLine(hdc, "Cancel", new Rect(275, 61, 365, 83), WhiteColor, DtCenter | DtSingleLine | DtVCenter);
    }

    private static void DrawTextLine(IntPtr hdc, string text, Rect rect, int color, int format)
    {
        _ = SetTextColor(hdc, color);
        _ = SetBkMode(hdc, 1);
        _ = DrawText(hdc, text.Length == 0 ? " " : text, text.Length == 0 ? 1 : text.Length, ref rect, format);
    }

    private static void FillRect(IntPtr hdc, Rect rect, int color)
    {
        var brush = CreateSolidBrush(color);
        try
        {
            _ = FillRect(hdc, ref rect, brush);
        }
        finally
        {
            DeleteObject(brush);
        }
    }

    private static int ColorRef(int red, int green, int blue)
    {
        return red | (green << 8) | (blue << 16);
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WndClass lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Message lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Message lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hwnd, ref PaintStruct lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PaintStruct lpPaint);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(int colorRef);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hdc, ref Rect lprc, IntPtr hbr);

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int crColor);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawText(IntPtr hdc, string lpchText, int cchText, ref Rect lprc, int format);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr hdc;
        public bool fErase;
        public Rect rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public Rect(int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }
    }
}
