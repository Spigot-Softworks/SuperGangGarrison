using System.Globalization;
using OpenGarrison.Client;
using OpenGarrison.Protocol;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: OpenGarrison.Tools.ReDsmReplayDump <replay-path> [--max-frames N] [--translate-summary] [--verify-playback] [--write-demo <demo-path>] [--verify-demo-playback]");
    return 1;
}

var replayPath = Path.GetFullPath(args[0]);
var maxFrames = 8;
var translateSummary = false;
var verifyPlayback = false;
string? writeDemoPath = null;
var verifyDemoPlayback = false;

for (var i = 1; i < args.Length; i += 1)
{
    if (string.Equals(args[i], "--translate-summary", StringComparison.OrdinalIgnoreCase))
    {
        translateSummary = true;
        continue;
    }

    if (string.Equals(args[i], "--verify-playback", StringComparison.OrdinalIgnoreCase))
    {
        verifyPlayback = true;
        continue;
    }

    if (string.Equals(args[i], "--verify-demo-playback", StringComparison.OrdinalIgnoreCase))
    {
        verifyDemoPlayback = true;
        continue;
    }

    if (string.Equals(args[i], "--write-demo", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("Expected a path after --write-demo.");
            return 1;
        }

        writeDemoPath = Path.GetFullPath(args[i + 1]);
        i += 1;
        continue;
    }

    if (!string.Equals(args[i], "--max-frames", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Unknown argument: {args[i]}");
        return 1;
    }

    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxFrames) || maxFrames < 0)
    {
        Console.Error.WriteLine("Expected a non-negative integer after --max-frames.");
        return 1;
    }

    i += 1;
}

var replay = ReDsmReplayParser.Parse(replayPath);
var frameByteTotal = replay.Frames.Sum(frame => frame.PayloadLength);
var minFrameLength = replay.Frames.Count > 0 ? replay.Frames.Min(frame => frame.PayloadLength) : 0;
var maxFrameLength = replay.Frames.Count > 0 ? replay.Frames.Max(frame => frame.PayloadLength) : 0;
var avgFrameLength = replay.Frames.Count > 0 ? frameByteTotal / (double)replay.Frames.Count : 0d;

Console.WriteLine($"Replay: {replayPath}");
Console.WriteLine(
    $"Header: replayVersion={replay.Header.ReplayVersion} gg2Version={replay.Header.GameVersion} " +
    $"fpsKind={replay.Header.FrameRateKind} fps={replay.Header.FramesPerSecond}");
Console.WriteLine($"Server: {replay.Header.ServerName}");
Console.WriteLine($"Map: {replay.Header.MapName}");
Console.WriteLine($"MapHash: {replay.Header.MapContentHash}");
Console.WriteLine($"PluginsRequired: {replay.Header.PluginsRequired}");
Console.WriteLine($"PluginList: {replay.Header.PluginList}");
Console.WriteLine(
    $"JoinStatePayloadBytes: {replay.Header.JoinStatePayload.Length} " +
    $"firstBytes={FormatHex(replay.Header.JoinStatePayload, 24)}");
if (translateSummary)
{
    if (!ReDsmReplayTransport.TryTranslateSummary(replayPath, out var summary, out var error))
    {
        Console.WriteLine($"TranslateSummaryError: {error}");
        return 2;
    }

    Console.WriteLine($"TranslateSummary: {summary}");
}
if (verifyPlayback)
{
    if (!ReDsmReplayTransport.TryVerifyPlaybackSummary(replayPath, out var summary, out var error))
    {
        Console.WriteLine($"VerifyPlaybackError: {error}");
        return 3;
    }

    Console.WriteLine($"VerifyPlayback: {summary}");
}
if (!string.IsNullOrWhiteSpace(writeDemoPath))
{
    if (!ReDsmReplayTransport.TryWriteOpenGarrisonDemo(replayPath, writeDemoPath, out var summary, out var error))
    {
        Console.WriteLine($"WriteDemoError: {error}");
        return 4;
    }

    Console.WriteLine($"WriteDemo: {summary}");
}
if (verifyDemoPlayback)
{
    if (string.IsNullOrWhiteSpace(writeDemoPath))
    {
        Console.WriteLine("VerifyDemoPlaybackError: --verify-demo-playback requires --write-demo <demo-path> in the same invocation.");
        return 5;
    }

    if (!OpenGarrisonDemoTransport.TryVerifyPlaybackSummary(writeDemoPath, out var summary, out var error))
    {
        Console.WriteLine($"VerifyDemoPlaybackError: {error}");
        return 6;
    }

    Console.WriteLine($"VerifyDemoPlayback: {summary}");
}
Console.WriteLine(
    $"Frames: {replay.Frames.Count} endMarker={replay.HasEndMarker} totalPayloadBytes={frameByteTotal} " +
    $"min={minFrameLength} max={maxFrameLength} avg={avgFrameLength:F1}");
Console.WriteLine("LeadingByteHistogram:");

foreach (var entry in replay.Frames
             .Where(frame => frame.FirstByte.HasValue)
             .GroupBy(frame => frame.FirstByte!.Value)
             .OrderByDescending(group => group.Count())
             .ThenBy(group => group.Key)
             .Take(16))
{
    Console.WriteLine($"  0x{entry.Key:X2} count={entry.Count()}");
}

var framesToShow = Math.Min(maxFrames, replay.Frames.Count);
for (var i = 0; i < framesToShow; i += 1)
{
    var frame = replay.Frames[i];
    Console.WriteLine($"Frame[{frame.Index}] len={frame.PayloadLength} bytes={FormatHex(frame.Payload, 32)}");
}

return 0;

static string FormatHex(byte[] bytes, int maxBytes)
{
    if (bytes.Length == 0 || maxBytes <= 0)
    {
        return string.Empty;
    }

    return BitConverter.ToString(bytes, 0, Math.Min(bytes.Length, maxBytes));
}
