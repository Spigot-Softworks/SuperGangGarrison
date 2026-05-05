using System;
using System.Collections.Generic;

namespace OpenGarrison.Protocol;

public sealed record ReDsmReplayHeader(
    byte ReplayVersion,
    ushort GameVersion,
    byte FrameRateKind,
    string ServerName,
    string MapName,
    string MapContentHash,
    bool PluginsRequired,
    string PluginList,
    byte[] JoinStatePayload)
{
    public int FramesPerSecond => FrameRateKind == 1 ? 60 : 30;
}

public sealed record ReDsmReplayTickFrame(int Index, byte[] Payload)
{
    public int PayloadLength => Payload.Length;
    public byte? FirstByte => Payload.Length > 0 ? Payload[0] : null;
}

public sealed record ReDsmReplayFile(
    ReDsmReplayHeader Header,
    IReadOnlyList<ReDsmReplayTickFrame> Frames,
    bool HasEndMarker);
