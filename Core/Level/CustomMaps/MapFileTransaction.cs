namespace OpenGarrison.Core;

internal static class MapFileTransaction
{
    internal static void Write(string destination, Action<string> writeAndVerify)
    {
        destination = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, "." + Path.GetFileName(destination) + ".save-" + Guid.NewGuid().ToString("N"));
        try
        {
            writeAndVerify(temporary);
            if (File.Exists(destination)) File.Replace(temporary, destination, destination + ".bak", ignoreMetadataErrors: true);
            else File.Move(temporary, destination);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
