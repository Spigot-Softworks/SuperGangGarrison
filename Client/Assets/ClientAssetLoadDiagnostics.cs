using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class ClientAssetLoadDiagnostics
{
    private const string TraceEnvironmentVariable = "OG_CLIENT_ASSET_TRACE";
    private const int MaximumLoggedAssets = 128;
    private static readonly bool TraceEnabled = !OperatingSystem.IsBrowser()
        && (IsEnabled(Environment.GetEnvironmentVariable(TraceEnvironmentVariable))
            || IsEnabled(Environment.GetEnvironmentVariable("OG_CLIENT_PERF_LOG")));
    private static readonly HashSet<string> LoggedAssets = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static long StartTimestamp()
        => TraceEnabled ? Stopwatch.GetTimestamp() : 0L;

    public static double GetElapsedMilliseconds(long startTimestamp)
        => startTimestamp <= 0L
            ? 0d
            : Math.Max(0d, (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency);

    public static void RecordOnce(string kind, string asset, string details, long startTimestamp)
    {
        if (!TraceEnabled || startTimestamp <= 0L)
        {
            return;
        }

        var key = string.Concat(kind, ":", asset);
        lock (Gate)
        {
            if (LoggedAssets.Count >= MaximumLoggedAssets || !LoggedAssets.Add(key))
            {
                return;
            }
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"timestamp={DateTimeOffset.Now:O} kind={kind} asset=\"{asset}\" totalMs={GetElapsedMilliseconds(startTimestamp):F3} {details}");
        try
        {
            File.AppendAllText(RuntimePaths.GetLogPath("client-asset-diagnostics.log"), line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must never change asset loading behavior.
        }
    }

    private static bool IsEnabled(string? value)
        => string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
