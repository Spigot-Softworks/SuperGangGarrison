using System.Text.Json;
using OpenGarrison.Core;
using OpenGarrison.SessionRuntime;

if (args is ["--ruleset"])
{
    Console.WriteLine(LastToDieRecording.CurrentRuleset);
    return 0;
}
if (args is not ["--verify", var path])
{
    Console.Error.WriteLine("Usage: RunVerifier --ruleset | --verify <recording.gz>");
    return 2;
}

var output = Console.Out;
Console.SetOut(Console.Error);
try
{
    // Workers run from a temporary directory; content belongs to this installation.
    var contentDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
    if (!File.Exists(Path.Combine(contentDirectory, "_gamemaker-asset-manifest.json")))
        throw new InvalidDataException("The verifier's installed game content is missing.");
    ContentRoot.Initialize(contentDirectory);
    if (new FileInfo(path).Length > LastToDieRecording.MaximumCompressedBytes)
        throw new InvalidDataException("Run recording is too large.");
    var recording = LastToDieRecording.Read(File.ReadAllBytes(path));
    var result = LastToDieRecordingVerifier.Verify(recording);
    output.WriteLine(JsonSerializer.Serialize(result));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Run verification failed: {exception.Message}");
    return 1;
}
