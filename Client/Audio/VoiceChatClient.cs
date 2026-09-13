using System;
using System.Collections.Generic;
using System.Linq;
using Concentus;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal sealed class VoiceChatClient : IDisposable
{
    internal sealed class Speaker(AudioRelayMessage message) : IDisposable
    {
        public byte Slot { get; } = message.SpeakerSlot;
        public uint StreamId { get; } = message.StreamId;
        public string Name { get; set; } = message.IsJukebox ? "Jukebox" : message.SpeakerName;
        public byte Team { get; set; } = message.Team;
        public bool IsJukebox => message.IsJukebox;
        public AudioPlaybackBuffer Buffer { get; } = new(message.IsJukebox);
        public void Dispose() => Buffer.Dispose();
    }
    private readonly IVoiceAudioDevice _device;
    private readonly VoiceChatSettings _settings;
    private readonly Action<bool, AudioPacket> _send;
    private readonly Action<VoiceChannelMembershipMessage>? _sendMembership;
    private bool _wantsVoiceChannel;
    private uint _membershipRevision;
    private double _nextMembershipAt;
    private readonly Dictionary<byte, Speaker> _speakers = new();
    // PCM gain above 100% is baked into chunks before they enter the device.
    // Only a change to that baked receive gain flushes the queue. Spatial and
    // master-volume changes stay continuous through the device mixer.
    private readonly Dictionary<byte, float> _playbackGains = new();
    private readonly AudioPacketHistory _history = new();
    private IOpusEncoder? _encoder;
    private StreamingPcmResampler? _resampler;
    private int _captureRate;
    private bool _capturing;
    private string _error = "";
    private double _lastTransmittedAt = double.NegativeInfinity;
    private double _now;
    public ServerAudioStateMessage? ServerState { get; private set; }
    public bool IsVoiceChannelJoined => ServerState is { VoiceChannelRequiresJoin: false } || _wantsVoiceChannel;
    public bool CanUseVoiceChannel => ServerState is { } state && (!state.VoiceChannelRequiresJoin
        || _wantsVoiceChannel && state.VoiceChannelJoined && state.VoiceChannelRevision == _membershipRevision);
    public IEnumerable<Speaker> Speakers => _speakers.Values;
    public IReadOnlyList<string> MicrophoneNames => _device.MicrophoneNames;
    public string Status => string.IsNullOrEmpty(_error) ? _device.Status : _error;
    public bool IsTransmitting(double now) => _capturing && now - _lastTransmittedAt < 0.2;
    public bool IsSpeaking(byte slot, double now) => _speakers.TryGetValue(slot, out var speaker) && now - speaker.Buffer.LastReceivedAt < 0.25;

    public VoiceChatClient(IVoiceAudioDevice device, VoiceChatSettings settings, Action<bool, AudioPacket> send,
        Action<VoiceChannelMembershipMessage>? sendMembership = null)
    { _device = device; _settings = settings; _send = send; _sendMembership = sendMembership; }

    public void SetVoiceChannelJoined(bool joined)
    {
        if (ServerState is not { VoiceChannelRequiresJoin: true } || _wantsVoiceChannel == joined) return;
        _wantsVoiceChannel = joined;
        if (++_membershipRevision == 0) ++_membershipRevision;
        if (!joined) StopPlayerVoice();
        SendMembership();
    }

    private void SendMembership()
    {
        _nextMembershipAt = _now + 0.25;
        _sendMembership?.Invoke(new(_membershipRevision, _wantsVoiceChannel));
    }

    private void StopPlayerVoice()
    {
        SuspendCapture();
        foreach (var slot in _speakers.Keys.Where(slot => slot != 0).ToArray()) RemoveSpeaker(slot);
    }

    public void SetVoiceMuted(bool muted)
    {
        _settings.VoiceMuted = muted;
        // Flush both jitter and device queues without changing capture or channel membership.
        foreach (var slot in _speakers.Keys.Where(slot => slot != 0).ToArray()) RemoveSpeaker(slot);
    }

    public void ApplyState(ServerAudioStateMessage state)
    {
        if (ServerState is { } previous && state.Revision != previous.Revision && !AudioWireFormat.IsNewer(state.Revision, previous.Revision)) return;
        ServerState = state;
        if (!CanUseVoiceChannel) StopPlayerVoice();
        if (!state.JukeboxPlaying || state.JukeboxPaused || _speakers.TryGetValue(0, out var jukebox) && jukebox.StreamId != state.JukeboxStreamId) RemoveSpeaker(0);
        // This permission is personalized for gagged clients. They can still listen.
    }

    public void Receive(AudioRelayMessage message, double now)
    {
        if (message.StreamId == 0 || !StreamingOpus.IsValid(message.Packet)) return;
        if (!message.IsJukebox && (!CanUseVoiceChannel || _settings.VoiceMuted)) return;
        if (message.IsJukebox && (ServerState is not { JukeboxPlaying: true, JukeboxPaused: false } state || state.JukeboxStreamId != message.StreamId)) return;
        if (_speakers.TryGetValue(message.SpeakerSlot, out var speaker) && speaker.StreamId != message.StreamId)
        {
            if (!AudioWireFormat.IsNewer(message.StreamId, speaker.StreamId)) return;
            RemoveSpeaker(message.SpeakerSlot); speaker = null;
        }
        if (speaker is null)
        {
            if (_speakers.Count >= 65) return;
            _speakers.Add(message.SpeakerSlot, speaker = new Speaker(message));
        }
        speaker.Name = message.IsJukebox ? "Jukebox" : message.SpeakerName;
        speaker.Team = message.Team;
        speaker.Buffer.Add(message.Packet, now);
    }

    public static bool ShouldCapture(VoiceTransmitMode mode, bool inputAllowed, bool pushToTalk, bool serverAllowsVoice)
        => serverAllowsVoice && inputAllowed && (mode == VoiceTransmitMode.OpenMicrophone || mode == VoiceTransmitMode.PushToTalk && pushToTalk);

    public void Update(double now, bool inputAllowed, bool pushToTalk, float masterVolume, Func<byte, bool> muted,
        Func<byte, (float Volume, float Pan)>? spatialMix = null)
    {
        _now = now;
        if (ServerState is { VoiceChannelRequiresJoin: true } state && _membershipRevision != 0 && now >= _nextMembershipAt
            && (state.VoiceChannelRevision != _membershipRevision || state.VoiceChannelJoined != _wantsVoiceChannel)) SendMembership();
        var capture = ShouldCapture(_settings.Mode, inputAllowed, pushToTalk, CanUseVoiceChannel && ServerState?.VoiceEnabled == true);
        try { _device.SetCapture(capture, _settings.MicrophoneName); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _error = "Microphone unavailable: " + ex.Message; capture = false; }
        _capturing = capture && _device.CaptureActive;
        if (!_capturing) { _resampler = null; _history.Clear(); }
        else
        {
            for (var i = 0; i < 4 && _device.TryReadCapture(out var samples, out var rate); i++)
            {
                if (_resampler is null || _captureRate != rate)
                {
                    _captureRate = rate;
                    _encoder ??= StreamingOpus.CreateEncoder(music: false);
                    _resampler = new StreamingPcmResampler(rate, 1, frame =>
                    {
                        _send(_settings.TeamOnly, _history.Add(StreamingOpus.Encode(_encoder, frame)));
                        _lastTransmittedAt = _now;
                    });
                }
                try { _resampler.Add(samples, _settings.MicrophoneGainPercent / 100f); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { _error = "Voice capture: " + ex.Message; _device.SetCapture(false, _settings.MicrophoneName); break; }
            }
        }
        foreach (var speaker in _speakers.Values.ToArray())
        {
            if (now - speaker.Buffer.LastReceivedAt > 0.6) { RemoveSpeaker(speaker.Slot); continue; }
            var receiveGain = speaker.Slot == 0 ? (_settings.JukeboxMuted ? 0 : _settings.JukeboxVolumePercent / 100f)
                : _settings.VoiceMuted || muted(speaker.Slot) ? 0 : _settings.VoiceVolumePercent / 100f;
            var mix = speaker.IsJukebox || !_settings.SpatialVoice
                ? (1f, 0f)
                : spatialMix?.Invoke(speaker.Slot) ?? (1f, 0f);
            var spatialVolume = Math.Clamp(mix.Item1, 0f, 1f);
            var deviceVolume = receiveGain;
            if (!_device.UsesGameMasterVolume) deviceVolume *= masterVolume;
            deviceVolume = Math.Clamp(deviceVolume * spatialVolume, 0f, 1f);
            var bakedGain = speaker.Slot == 0 ? 1f : Math.Max(1f, receiveGain);
            try
            {
                if (speaker.Slot != 0
                    && _playbackGains.TryGetValue(speaker.Slot, out var previousGain)
                    && MathF.Abs(previousGain - bakedGain) > 0.0001f)
                {
                    _device.StopPlayback(speaker.Slot);
                }
                _playbackGains[speaker.Slot] = bakedGain;
                if (deviceVolume <= 0 || masterVolume <= 0) _device.StopPlayback(speaker.Slot);
                else _device.SetVolume(speaker.Slot, deviceVolume);
                for (var i = 0; i < 6 && speaker.Buffer.TryRead(now, out var pcm); i++)
                    if (deviceVolume > 0 && masterVolume > 0)
                    {
                        if (speaker.Slot != 0 && bakedGain > 1f)
                        {
                            pcm = ApplyPlaybackGain(pcm, bakedGain);
                        }

                        _device.Play(speaker.Slot, pcm, speaker.Slot == 0 ? 2 : 1, deviceVolume, mix.Item2);
                    }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { _error = "Voice playback: " + ex.Message; RemoveSpeaker(speaker.Slot); }
        }
    }

    public void RemoveSpeaker(byte slot)
    {
        _playbackGains.Remove(slot);
        if (_speakers.Remove(slot, out var speaker)) speaker.Dispose();
        try { _device.StopPlayback(slot); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _error = "Audio device unavailable: " + ex.Message; }
    }
    public void SuspendCapture()
    {
        try { _device.SetCapture(false, _settings.MicrophoneName); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { _error = "Microphone unavailable: " + ex.Message; }
        _capturing = false; _resampler = null; _history.Clear();
    }
    public void Reset()
    {
        SuspendCapture(); ServerState = null; _error = "";
        _wantsVoiceChannel = false; _membershipRevision = 0; _nextMembershipAt = 0;
        foreach (var slot in _speakers.Keys.ToArray()) RemoveSpeaker(slot);
    }
    public void Dispose() { Reset(); _encoder?.Dispose(); _device.Dispose(); }

    private static short[] ApplyPlaybackGain(short[] samples, float gain)
    {
        var result = new short[samples.Length];
        for (var index = 0; index < samples.Length; index += 1)
        {
            var value = samples[index] / 32768f * gain;
            var absolute = MathF.Abs(value);
            if (absolute > 0.88f)
            {
                absolute = 0.88f + 0.12f * MathF.Tanh((absolute - 0.88f) / 0.12f);
                value = MathF.CopySign(absolute, value);
            }

            result[index] = (short)Math.Clamp((int)MathF.Round(value * 32767f), short.MinValue, short.MaxValue);
        }

        return result;
    }
}
