#nullable enable

using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

internal enum DevMessageEntryKind
{
    ShowMessage = 1,
    UpdateAvailable = 2,
}

internal sealed record DevMessageEntry(
    DevMessageEntryKind Kind,
    string Title,
    string Message,
    int VersionCode = 0,
    string VersionLabel = "");

internal sealed record DevMessageFetchResult(
    IReadOnlyList<DevMessageEntry> Entries,
    string SourceDescription,
    string? Error = null);

internal static class DevMessageService
{
    internal const int SourceParityVersionCode = 22000;
    internal const string SourceParityVersionLabel = "2.2";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private static readonly Uri[] RemoteSources =
    [
        new("https://www.ganggarrison.com/devmessages.txt"),
        new("http://www.ganggarrison.com/devmessages.txt"),
    ];

    public static async Task<DevMessageFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var localPath = FindLocalDevMessagesPath();
        if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
        {
            var content = await File.ReadAllTextAsync(localPath, cancellationToken).ConfigureAwait(false);
            return new DevMessageFetchResult(ParseEntries(content), $"local:{localPath}");
        }

        Exception? lastError = null;
        for (var index = 0; index < RemoteSources.Length; index += 1)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, RemoteSources[index]);
                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new DevMessageFetchResult(ParseEntries(content), RemoteSources[index].ToString());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                lastError = ex;
            }
        }

        return new DevMessageFetchResult(Array.Empty<DevMessageEntry>(), "unavailable", lastError?.Message);
    }

    public static string? FindBundledUpdaterPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "OpenGarrisonupdater.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "OpenGarrisonupdater.exe"),
        };

        for (var index = 0; index < candidates.Length; index += 1)
        {
            if (File.Exists(candidates[index]))
            {
                return candidates[index];
            }
        }

        return ProjectSourceLocator.FindFile("OpenGarrisonupdater.exe");
    }

    internal static IReadOnlyList<DevMessageEntry> ParseEntries(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<DevMessageEntry>();
        }

        var entries = new List<DevMessageEntry>();
        var lines = content
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index += 1)
        {
            var command = lines[index].Trim();
            if (command.Length == 0)
            {
                continue;
            }

            if (command.Equals("ShowMessage", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= lines.Length)
                {
                    break;
                }

                index += 1;
                var message = NormalizeDisplayText(lines[index]);
                if (message.Length > 0)
                {
                    entries.Add(new DevMessageEntry(DevMessageEntryKind.ShowMessage, "Developer Message", message));
                }

                continue;
            }

            if (command.Equals("Version", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= lines.Length)
                {
                    break;
                }

                index += 1;
                var payload = lines[index].Trim();
                if (payload.Length == 0)
                {
                    continue;
                }

                var separatorIndex = payload.IndexOf('!');
                var versionLabel = separatorIndex >= 0 ? payload[..separatorIndex].Trim() : payload;
                var changes = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : string.Empty;
                if (!TryParseVersionCode(versionLabel, out var versionCode) || versionCode <= SourceParityVersionCode)
                {
                    continue;
                }

                var body = changes.Length == 0
                    ? $"Updates have been made to Gang Garrison 2.\n\nVersion {versionLabel} is newer than the source parity target v{SourceParityVersionLabel}."
                    : $"Updates have been made to Gang Garrison 2.\n\n{NormalizeDisplayText(changes)}";
                entries.Add(new DevMessageEntry(
                    DevMessageEntryKind.UpdateAvailable,
                    $"Update Available ({versionLabel})",
                    body,
                    versionCode,
                    versionLabel));
            }
        }

        return entries;
    }

    internal static bool TryParseVersionCode(string? versionText, out int versionCode)
    {
        versionCode = 0;
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return false;
        }

        var trimmed = versionText.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out versionCode))
        {
            return versionCode > 0;
        }

        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var weights = new[] { 10000, 1000, 100, 1 };
        for (var index = 0; index < parts.Length && index < weights.Length; index += 1)
        {
            if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var part) || part < 0)
            {
                versionCode = 0;
                return false;
            }

            versionCode += part * weights[index];
        }

        return versionCode > 0;
    }

    private static string? FindLocalDevMessagesPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "devmessages.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "devmessages.txt"),
        };
        for (var index = 0; index < candidates.Length; index += 1)
        {
            if (File.Exists(candidates[index]))
            {
                return candidates[index];
            }
        }

        return ProjectSourceLocator.FindFile("devmessages.txt");
    }

    private static string NormalizeDisplayText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text
            .Replace("##", "\n\n", StringComparison.Ordinal)
            .Replace('#', '\n')
            .Trim();
    }
}
