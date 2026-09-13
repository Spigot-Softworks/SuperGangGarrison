using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class JukeboxTests
{
    [Theory]
    [InlineData("mp3")]
    [InlineData("ogg")]
    public async Task CompressedPlaylistFormatsDecodeResampleAndProduceAudibleStereoOpus(string extension)
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-jukebox-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "tone." + extension);
        var assembly = typeof(JukeboxTests).Assembly;
        using (var resource = assembly.GetManifestResourceStream(Assert.Single(assembly.GetManifestResourceNames(), name => name.EndsWith("tone." + extension, StringComparison.Ordinal))))
        using (var destination = File.Create(path)) await resource!.CopyToAsync(destination);
        using var reader = new JukeboxTrackReader(path);
        using var decoder = StreamingOpus.CreateDecoder(music: true);
        var frameCount = 0;
        var peak = 0;
        try
        {
            for (var attempt = 0; attempt < 500 && !reader.Finished; attempt++)
            {
                while (reader.TryRead(out var frame))
                {
                    Assert.True(StreamingOpus.IsValid(new((uint)++frameCount, [frame])));
                    var samples = new short[1920];
                    Assert.Equal(960, decoder.Decode(frame, samples, 960));
                    peak = Math.Max(peak, samples.Max(sample => Math.Abs((int)sample)));
                }
                await Task.Delay(10);
            }
            Assert.True(reader.Finished);
            Assert.Null(reader.Error);
            Assert.InRange(frameCount, 12, 18);
            Assert.True(peak > 1000, "The decoded track should contain the generated test tone.");
        }
        finally { reader.Dispose(); await DeleteWhenClosed(directory); }
    }

    [Fact]
    public async Task PlaylistStreamsWithoutPlayersTakingSlotsAndSupportsPauseSkipStopAndLateJoin()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-jukebox-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        WriteWave(Path.Combine(directory, "01 first.wav"), 1);
        WriteWave(Path.Combine(directory, "02 second.wav"), 1);
        File.WriteAllLines(Path.Combine(directory, "playlist.txt"), ["02 second.wav", "01 first.wav"]);
        var clients = new Dictionary<byte, ClientSession>
        {
            [1] = new(1, 1, new IPEndPoint(IPAddress.Loopback, 31000), "Listener", TimeSpan.Zero),
        };
        var messages = new List<(ServerTransportPeer Peer, IProtocolMessage Message)>();
        var now = 0d;
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        using var service = new ServerAudioService(new() { JukeboxDirectory = directory, JukeboxLoop = false }, clients, world,
            (peer, message) => messages.Add((peer, message)), () => now, _ => { });
        try
        {
            Assert.Equal(2, service.Execute("list").Count);
            Assert.Equal(new[] { "1: 02 second.wav", "2: 01 first.wav" }, service.Execute("list"));
            Assert.Contains("not found", Assert.Single(service.Execute("play ../secret.wav")), StringComparison.OrdinalIgnoreCase);
            service.Execute("play \"01 first.wav\"");
            Assert.True(service.State.JukeboxPlaying);
            await PumpUntil(() => messages.Any(item => item.Message is AudioRelayMessage));
            var audio = Assert.IsType<AudioRelayMessage>(messages.First(item => item.Message is AudioRelayMessage).Message);
            Assert.Equal((byte)0, audio.SpeakerSlot);
            Assert.Equal("Jukebox", audio.SpeakerName);
            Assert.True(StreamingOpus.IsValid(audio.Packet));
            Assert.Single(clients);

            var lateJoiner = new ClientSession(2, 2, new IPEndPoint(IPAddress.Loopback, 31001), "Late", TimeSpan.Zero);
            clients.Add(2, lateJoiner);
            service.SendState(lateJoiner);
            var state = Assert.IsType<ServerAudioStateMessage>(messages.Last().Message);
            Assert.True(state.JukeboxPlaying);
            Assert.Equal("01 first", state.TrackName);
            service.Execute("pause");
            Assert.True(service.State.JukeboxPaused);
            messages.Clear();
            now += 1;
            service.Tick();
            Assert.DoesNotContain(messages, item => item.Message is AudioRelayMessage);
            service.Execute("resume");
            await PumpUntil(() => messages.Any(item => item.Message is AudioRelayMessage && item.Peer == lateJoiner.Peer));
            var oldStream = service.State.JukeboxStreamId;
            service.Execute("next");
            Assert.True(AudioWireFormat.IsNewer(service.State.JukeboxStreamId, oldStream));
            Assert.Equal("02 second", service.State.TrackName);
            service.Execute("stop");
            Assert.False(service.State.JukeboxPlaying);
            messages.Clear();
            now += 0.1;
            service.Tick();
            Assert.DoesNotContain(messages, item => item.Message is AudioRelayMessage);
        }
        finally
        {
            service.Dispose();
            await DeleteWhenClosed(directory);
        }

        async Task PumpUntil(Func<bool> predicate)
        {
            for (var i = 0; i < 500 && !predicate(); i++) { await Task.Delay(10); now += 0.01; service.Tick(); }
            Assert.True(predicate(), "Jukebox did not produce audio within 5 seconds.");
        }
    }

    [Fact]
    public async Task InvalidTracksAreSkippedAndAnEntireInvalidPlaylistStops()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-jukebox-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "bad.wav"), "not a wave file");
        WriteWave(Path.Combine(directory, "empty.wav"), 0);
        var now = 0d;
        var logs = new List<string>();
        using var service = new ServerAudioService(new() { JukeboxDirectory = directory, JukeboxLoop = true }, new(),
            new SimulationWorld(new SimulationConfig { EnableLocalDummies = false }), (_, _) => { }, () => now, logs.Add);
        try
        {
            service.Execute("play");
            for (var i = 0; i < 300 && service.State.JukeboxPlaying; i++) { await Task.Delay(10); now += 0.01; service.Tick(); }
            Assert.False(service.State.JukeboxPlaying);
            Assert.Contains(logs, line => line.Contains("bad.wav", StringComparison.Ordinal));
        }
        finally { service.Dispose(); await DeleteWhenClosed(directory); }
    }

    [Fact]
    public async Task EndOfTrackAdvancesAndLoopDisabledStopsAtEnd()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-jukebox-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        WriteWave(Path.Combine(directory, "z-last-alphabetically.wav"), 0.1);
        WriteWave(Path.Combine(directory, "two.wav"), 0.1);
        File.WriteAllLines(Path.Combine(directory, "playlist.txt"), ["z-last-alphabetically.wav", "two.wav"]);
        var now = 0d;
        var trackNames = new List<string>();
        var lastFrameAt = double.NegativeInfinity;
        var endedAt = double.PositiveInfinity;
        var clients = new Dictionary<byte, ClientSession> { [1] = new(1, 1, new IPEndPoint(IPAddress.Loopback, 31000), "Listener", TimeSpan.Zero) };
        using var service = new ServerAudioService(new() { JukeboxDirectory = directory, JukeboxLoop = false }, clients,
            new SimulationWorld(new SimulationConfig { EnableLocalDummies = false }), (_, message) =>
            {
                if (message is AudioRelayMessage) lastFrameAt = now;
                if (message is ServerAudioStateMessage { JukeboxPlaying: false }) endedAt = now;
            }, () => now, _ => { });
        try
        {
            service.Execute("play");
            for (var i = 0; i < 500 && service.State.JukeboxPlaying; i++)
            {
                if (trackNames.LastOrDefault() != service.State.TrackName) trackNames.Add(service.State.TrackName);
                await Task.Delay(10); now += 0.01; service.Tick();
            }
            Assert.Equal(new[] { "z-last-alphabetically", "two" }, trackNames);
            Assert.False(service.State.JukeboxPlaying);
            Assert.True(endedAt - lastFrameAt >= 0.2, "Track end must let the receiver finish its buffered audio.");
        }
        finally { service.Dispose(); await DeleteWhenClosed(directory); }
    }

    [Fact]
    public async Task ExplicitPlaylistPreservesLineOrderAndRepeatsWhileSkippingInvalidEntriesAndReloadingEdits()
    {
        var directory = Path.Combine(Path.GetTempPath(), "og2-jukebox-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        WriteWave(Path.Combine(directory, "A.wav"), 0.1);
        WriteWave(Path.Combine(directory, "Z song.wav"), 0.1);
        WriteWave(Path.Combine(directory, "unlisted.wav"), 0.1);
        var manifest = Path.Combine(directory, "playlist.txt");
        File.WriteAllLines(manifest, ["# Playback order", "", "Z song.wav", "missing.mp3", "../A.wav", "A.wav", "Z song.wav"]);
        using var service = new ServerAudioService(new() { JukeboxDirectory = directory }, new(),
            new SimulationWorld(new SimulationConfig { EnableLocalDummies = false }), (_, _) => { }, () => 0, _ => { });
        try
        {
            Assert.Equal(new[] { "1: Z song.wav", "2: A.wav", "3: Z song.wav" }, service.Execute("list"));
            service.Execute("play");
            Assert.Equal("Z song", service.State.TrackName);
            service.Execute("next");
            Assert.Equal("A", service.State.TrackName);
            service.Execute("next");
            Assert.Equal("Z song", service.State.TrackName);
            service.Execute("next");
            Assert.Equal("Z song", service.State.TrackName);
            File.WriteAllLines(manifest, ["A.wav", "Z song.wav"]);
            service.Execute("play 1");
            Assert.Equal("A", service.State.TrackName);
            Assert.Contains("not found", Assert.Single(service.Execute("play unlisted.wav")), StringComparison.OrdinalIgnoreCase);
            File.WriteAllText(manifest, "# Deliberately empty playlist\n");
            Assert.Contains("playlist.txt", Assert.Single(service.Execute("list")));
        }
        finally { service.Dispose(); await DeleteWhenClosed(directory); }
    }

    private static void WriteWave(string path, double seconds)
    {
        const int rate = 44100;
        var samples = (int)(rate * seconds);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(samples * 2);
        for (var i = 0; i < samples; i++) writer.Write((short)(Math.Sin(i * Math.PI * 880 / rate) * 9000));
    }

    private static async Task DeleteWhenClosed(string directory)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try { Directory.Delete(directory, recursive: true); return; }
            catch (IOException) when (attempt < 99) { await Task.Delay(20); }
        }
    }
}
