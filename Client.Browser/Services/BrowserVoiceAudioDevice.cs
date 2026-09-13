using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;
using OpenGarrison.Client;

namespace OpenGarrison.Client.Browser.Services;

public sealed class BrowserVoiceAudioDevice : IVoiceAudioDevice
{
    private readonly IJSInProcessRuntime _js;
    private readonly Queue<(float[] Samples, int Rate)> _capture = new();
    private readonly DotNetObjectReference<BrowserVoiceAudioDevice> _reference;
    private bool _requested;
    private bool _disposed;
    private int _generation;
    public bool UsesGameMasterVolume => false;
    public bool CaptureActive { get; private set; }
    public string Status { get; private set; } = "";
    public IReadOnlyList<string> MicrophoneNames => _js.Invoke<string[]>("OpenGarrisonVoice.microphoneNames");

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(BrowserVoiceAudioDevice))]
    public BrowserVoiceAudioDevice(IJSRuntime js)
    {
        _js = (IJSInProcessRuntime)js;
        _reference = DotNetObjectReference.Create(this);
    }
    public ValueTask InitializeAsync() => _js.InvokeVoidAsync("OpenGarrisonVoice.init", _reference);

    public void SetCapture(bool active, string microphoneName)
    {
        if (_disposed || active == _requested) return;
        _requested = active;
        _generation++;
        if (!active) { _capture.Clear(); CaptureActive = false; }
        _js.InvokeVoid("OpenGarrisonVoice.setCapture", active, microphoneName, _generation);
    }

    [JSInvokable("VoiceCaptureState")]
    public void SetCaptureState(int generation, bool active, string status)
    {
        if (_disposed || generation != _generation) return;
        CaptureActive = active;
        Status = status;
        if (!active) _capture.Clear();
        if (!active && string.IsNullOrEmpty(status)) _requested = false;
    }

    [JSInvokable("VoiceCapture")]
    public void ReceiveCapture(int generation, byte[] pcm, int sampleRate)
    {
        if (_disposed || !_requested || !CaptureActive || generation != _generation || pcm.Length > 4096 || pcm.Length % 2 != 0) return;
        var samples = new float[pcm.Length / 2];
        for (var i = 0; i < samples.Length; i++) samples[i] = (short)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8)) / 32768f;
        while (_capture.Count >= 4) _capture.Dequeue();
        _capture.Enqueue((samples, sampleRate));
    }

    public bool TryReadCapture(out float[] samples, out int sampleRate)
    {
        if (_capture.TryDequeue(out var frame)) { samples = frame.Samples; sampleRate = frame.Rate; return true; }
        samples = []; sampleRate = 48000; return false;
    }
    public void Play(byte speakerSlot, short[] samples, int channels, float volume, float pan)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        _js.InvokeVoid("OpenGarrisonVoice.play", speakerSlot, bytes, channels, volume, pan);
    }
    public void SetVolume(byte speakerSlot, float volume) => _js.InvokeVoid("OpenGarrisonVoice.setVolume", speakerSlot, volume);
    public void StopPlayback(byte speakerSlot) { if (!_disposed) _js.InvokeVoid("OpenGarrisonVoice.stopPlayback", speakerSlot); }
    public void Dispose()
    {
        if (_disposed) return;
        _js.InvokeVoid("OpenGarrisonVoice.dispose");
        _disposed = true;
        _capture.Clear();
        _reference.Dispose();
    }
}
