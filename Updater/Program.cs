using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;

const string DefaultManifestUrl = "https://api.unkind-dev.com/updates/windows-x64/stable/latest.json";
const string GameExecutableName = "OG2.exe";
const string VersionFileName = "version.txt";

var appDirectory = AppContext.BaseDirectory;
var manifestUrl = Environment.GetEnvironmentVariable("OPENGARRISON_UPDATE_MANIFEST");
if (string.IsNullOrWhiteSpace(manifestUrl))
{
    manifestUrl = DefaultManifestUrl;
}

try
{
    await TryApplyUpdateAsync(appDirectory, manifestUrl);
}
catch
{
    // Launch should remain reliable even when the updater endpoint is offline or a package is bad.
}

LaunchGame(appDirectory, args);

static async Task TryApplyUpdateAsync(string appDirectory, string manifestUrl)
{
    using var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    var manifest = await httpClient.GetFromJsonAsync<UpdateManifest>(manifestUrl).ConfigureAwait(false);
    if (manifest is null || string.IsNullOrWhiteSpace(manifest.Url) || string.IsNullOrWhiteSpace(manifest.Version))
    {
        return;
    }

    var currentVersion = ReadCurrentVersion(appDirectory);
    if (!IsNewerVersion(manifest.Version, currentVersion))
    {
        return;
    }

    var packageUri = new Uri(new Uri(manifestUrl), manifest.Url);
    var tempRoot = Path.Combine(Path.GetTempPath(), "OpenGarrisonUpdate");
    var packagePath = Path.Combine(tempRoot, "package.zip");
    var extractPath = Path.Combine(tempRoot, "extract");
    if (Directory.Exists(tempRoot))
    {
        Directory.Delete(tempRoot, recursive: true);
    }

    Directory.CreateDirectory(tempRoot);
    await using (var packageStream = await httpClient.GetStreamAsync(packageUri).ConfigureAwait(false))
    await using (var fileStream = File.Create(packagePath))
    {
        await packageStream.CopyToAsync(fileStream).ConfigureAwait(false);
    }

    if (!string.IsNullOrWhiteSpace(manifest.Sha256)
        && !string.Equals(await ComputeSha256Async(packagePath).ConfigureAwait(false), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    ZipFile.ExtractToDirectory(packagePath, extractPath);
    CopyUpdatePayload(extractPath, appDirectory);
    File.WriteAllText(Path.Combine(appDirectory, VersionFileName), manifest.Version.Trim());
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
    return Version.TryParse(NormalizeVersion(candidate), out var candidateVersion)
        && Version.TryParse(NormalizeVersion(current), out var currentVersion)
        && candidateVersion > currentVersion;
}

static string NormalizeVersion(string value)
{
    var clean = value.Trim().TrimStart('v', 'V');
    var dashIndex = clean.IndexOf('-', StringComparison.Ordinal);
    return dashIndex >= 0 ? clean[..dashIndex] : clean;
}

static async Task<string> ComputeSha256Async(string path)
{
    await using var stream = File.OpenRead(path);
    var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static void CopyUpdatePayload(string sourceDirectory, string destinationDirectory)
{
    var launcherPath = Environment.ProcessPath;
    foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, directory);
        if (ShouldPreserveLocalPath(relativePath))
        {
            continue;
        }

        Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
    }

    foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, file);
        if (ShouldPreserveLocalPath(relativePath))
        {
            continue;
        }

        var destinationPath = Path.Combine(destinationDirectory, relativePath);
        if (!string.IsNullOrWhiteSpace(launcherPath)
            && string.Equals(Path.GetFullPath(destinationPath), Path.GetFullPath(launcherPath), StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationDirectory);
        File.Copy(file, destinationPath, overwrite: true);
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

    Process.Start(new ProcessStartInfo
    {
        FileName = gamePath,
        WorkingDirectory = appDirectory,
        UseShellExecute = true,
        Arguments = string.Join(" ", args.Select(QuoteArgument)),
    });
}

static string QuoteArgument(string value)
{
    return value.Contains(' ') || value.Contains('"')
        ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
        : value;
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
