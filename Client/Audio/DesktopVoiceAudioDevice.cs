#if !BROWSER_KNI
using System.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Audio;

namespace OpenGarrison.Client;

internal sealed class DesktopVoiceAudioDevice : IVoiceAudioDevice
{
    // DynamicSoundEffectInstance is intentionally not used for voice playback.
    // MonoGame 3.8.4.1's OpenAL DynamicSoundEffectInstance.PlatformCreate reserves
    // a source but never initializes SoundEffectInstance.sourceChannels. Its
    // inherited Pan setter therefore cannot configure a recycled source until a
    // private field is populated. The SDL callback below owns one continuous
    // stereo queue and applies each stream's explicit pan in the audio thread.
    private readonly VoiceAudioMixer _mixer = new();
    private readonly AudioCallback _audioCallback;
    private GCHandle _userDataHandle;
    private uint _audioDevice;
    private bool _ownsAudioSubsystem;
    private Microphone? _microphone;
    private bool _requestedCapture;
    private string _requestedName = "";
    private readonly byte[] _captureBytes = new byte[16384];
    static DesktopVoiceAudioDevice()
    {
        // The packaged Unix assets use SDL's soname, while the Windows asset
        // is SDL2.dll. Resolve the same supported SDL API on every desktop
        // target without relying on a platform-specific symlink.
        try { NativeLibrary.SetDllImportResolver(typeof(DesktopVoiceAudioDevice).Assembly, ResolveSdlImport); }
        catch (InvalidOperationException) { }
    }

    public DesktopVoiceAudioDevice() => _audioCallback = RenderAudio;
    public bool UsesGameMasterVolume => false;
    public bool CaptureActive => _microphone?.State == MicrophoneState.Started;
    public string Status { get; private set; } = "";
    public IReadOnlyList<string> MicrophoneNames
    {
        get
        {
            try { return Microphone.All.Select(microphone => microphone.Name).ToArray(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Status = "Microphone unavailable: " + ex.Message; return []; }
        }
    }

    public void SetCapture(bool active, string microphoneName)
    {
        if (_requestedCapture == active && _requestedName == microphoneName) return;
        _requestedCapture = active;
        _requestedName = microphoneName;
        StopCapture();
        if (!active) return;
        try
        {
            _microphone = string.IsNullOrEmpty(microphoneName) ? Microphone.Default
                : Microphone.All.FirstOrDefault(microphone => microphone.Name == microphoneName);
            if (_microphone is null) { Status = "No microphone. Select a device in Audio settings."; return; }
            _microphone.BufferDuration = TimeSpan.FromMilliseconds(100);
            // MonoGame's OpenAL capture query requires a BufferReady subscriber, even when polling GetData.
            _microphone.BufferReady += BufferReady;
            _microphone.Start();
            Status = "";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Status = "Microphone unavailable: " + ex.Message;
            StopCapture();
        }
    }

    private static void BufferReady(object? sender, EventArgs args) { }
    private void StopCapture()
    {
        if (_microphone is null) return;
        try { _microphone.Stop(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Status = "Microphone stopped: " + ex.Message; }
        finally { _microphone.BufferReady -= BufferReady; _microphone = null; }
    }

    public bool TryReadCapture(out float[] samples, out int sampleRate)
    {
        samples = []; sampleRate = 48000;
        if (!CaptureActive || _microphone is null) return false;
        try
        {
            var count = _microphone.GetData(_captureBytes);
            if (count <= 0) return false;
            sampleRate = _microphone.SampleRate;
            samples = new float[count / 2];
            for (var i = 0; i < samples.Length; i++) samples[i] = (short)(_captureBytes[i * 2] | (_captureBytes[(i * 2) + 1] << 8)) / 32768f;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Status = "Microphone unavailable: " + ex.Message;
            StopCapture();
            return false;
        }
    }

    public void Play(byte speakerSlot, short[] samples, int channels, float volume, float pan)
    {
        if (!EnsureAudioDevice()) return;
        _mixer.Enqueue(speakerSlot, samples.ToArray(), channels, volume, pan);
    }

    public void SetVolume(byte speakerSlot, float volume) => _mixer.SetVolume(speakerSlot, volume);
    public void StopPlayback(byte speakerSlot) => _mixer.Stop(speakerSlot);

    public void Dispose()
    {
        StopCapture();
        _mixer.StopAll();
        if (_audioDevice != 0)
        {
            SDL_PauseAudioDevice(_audioDevice, 1);
            SDL_CloseAudioDevice(_audioDevice);
            _audioDevice = 0;
        }
        if (_ownsAudioSubsystem)
        {
            SDL_QuitSubSystem(AudioSubsystemFlag);
            _ownsAudioSubsystem = false;
        }
        if (_userDataHandle.IsAllocated) _userDataHandle.Free();
    }

    private bool EnsureAudioDevice()
    {
        if (_audioDevice != 0) return true;
        try
        {
            if ((SDL_WasInit(AudioSubsystemFlag) & AudioSubsystemFlag) == 0)
            {
                if (SDL_InitSubSystem(AudioSubsystemFlag) != 0)
                    throw new InvalidOperationException("SDL audio initialization failed: " + GetSdlError());
                _ownsAudioSubsystem = true;
            }
            _userDataHandle = GCHandle.Alloc(this);
            var callbackPointer = Marshal.GetFunctionPointerForDelegate(_audioCallback);
            var desired = new SdlAudioSpec
            {
                Frequency = 48000,
                Format = AudioS16Lsb,
                Channels = 2,
                Samples = 1024,
                Callback = callbackPointer,
                UserData = GCHandle.ToIntPtr(_userDataHandle),
            };
            var device = SDL_OpenAudioDevice(IntPtr.Zero, 0, ref desired, out var obtained, 0);
            if (device == 0) throw new InvalidOperationException("SDL_OpenAudioDevice failed: " + GetSdlError());
            if (obtained.Frequency != desired.Frequency || obtained.Channels != desired.Channels || obtained.Format != desired.Format)
            {
                SDL_CloseAudioDevice(device);
                throw new InvalidOperationException("SDL audio device does not support 48 kHz stereo signed PCM.");
            }

            _audioDevice = device;
            SDL_PauseAudioDevice(_audioDevice, 0);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_userDataHandle.IsAllocated) _userDataHandle.Free();
            if (_ownsAudioSubsystem)
            {
                SDL_QuitSubSystem(AudioSubsystemFlag);
                _ownsAudioSubsystem = false;
            }
            Status = "Voice output unavailable: " + ex.Message;
            return false;
        }
    }

    private void RenderAudio(IntPtr userData, IntPtr stream, int length)
    {
        var sampleCount = length / sizeof(short);
        var samples = ArrayPool<short>.Shared.Rent(sampleCount);
        try
        {
            try { _mixer.Render(samples.AsSpan(0, sampleCount)); }
            catch { Array.Clear(samples, 0, sampleCount); }
            Marshal.Copy(samples, 0, stream, sampleCount);
        }
        finally { ArrayPool<short>.Shared.Return(samples); }
    }

    private static string GetSdlError()
    {
        try { return Marshal.PtrToStringUTF8(SDL_GetError()) ?? "unknown error"; }
        catch { return "unknown error"; }
    }

    private static IntPtr ResolveSdlImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "SDL2", StringComparison.Ordinal)) return IntPtr.Zero;
        var candidates = OperatingSystem.IsLinux()
            ? new[] { "libSDL2-2.0.so.0", "libSDL2.so", "SDL2" }
            : OperatingSystem.IsMacOS()
                ? new[] { "libSDL2-2.0.0.dylib", "libSDL2.dylib", "SDL2" }
                : new[] { "SDL2.dll", "SDL2" };
        foreach (var candidate in candidates)
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle)) return handle;
        return IntPtr.Zero;
    }

    private const uint AudioSubsystemFlag = 0x00000010;
    private const ushort AudioS16Lsb = 0x8010;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AudioCallback(IntPtr userData, IntPtr stream, int length);

    [StructLayout(LayoutKind.Sequential)]
    private struct SdlAudioSpec
    {
        public int Frequency;
        public ushort Format;
        public byte Channels;
        public byte Silence;
        public ushort Samples;
        public ushort Padding;
        public uint Size;
        public IntPtr Callback;
        public IntPtr UserData;
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint SDL_OpenAudioDevice(
        IntPtr device,
        int isCapture,
        ref SdlAudioSpec desired,
        out SdlAudioSpec obtained,
        int allowedChanges);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_PauseAudioDevice(uint device, int pauseOn);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_CloseAudioDevice(uint device);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetError();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint SDL_WasInit(uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_InitSubSystem(uint flags);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_QuitSubSystem(uint flags);
}
#endif
