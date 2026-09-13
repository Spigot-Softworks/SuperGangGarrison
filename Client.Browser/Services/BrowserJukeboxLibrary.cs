using Microsoft.JSInterop;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;

namespace OpenGarrison.Client.Browser.Services;

internal sealed class BrowserJukeboxLibrary(IJSInProcessRuntime js)
{
    private static string DirectoryPath => Path.Combine(RuntimePaths.ConfigDirectory, "jukebox");
    public void Manage() => js.InvokeVoid("OpenGarrisonJukebox.manage");
    public async Task Restore()
    {
        using var callback = DotNetObjectReference.Create(this);
        Directory.CreateDirectory(DirectoryPath);
        var names = await js.InvokeAsync<string[]>("OpenGarrisonJukebox.restore", callback);
        File.WriteAllLines(Path.Combine(DirectoryPath, "playlist.txt"), names.Select(SafeName));
        var keep = names.Select(SafeName).ToHashSet(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(DirectoryPath))
            if (Path.GetFileName(file) != "playlist.txt" && !keep.Contains(Path.GetFileName(file))) File.Delete(file);
    }
    [JSInvokable] public void ReceiveTrack(string name, byte[] data)
    {
        if (data.Length > 32 * 1024 * 1024) throw new InvalidDataException("A track is too large (32 MB maximum).");
        File.WriteAllBytes(Path.Combine(DirectoryPath, SafeName(name)), data);
    }
    private static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || System.Text.Encoding.UTF8.GetByteCount(name) > OpenGarrison.Protocol.AudioWireFormat.MaxTrackBytes
            || name.IndexOfAny(['/', '\\', ':', '\r', '\n']) >= 0
            || Path.GetExtension(name).ToLowerInvariant() is not (".wav" or ".mp3" or ".ogg"))
            throw new InvalidDataException("Invalid music filename.");
        return name;
    }
}
