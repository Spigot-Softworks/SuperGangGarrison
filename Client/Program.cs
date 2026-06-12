using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using OpenGarrison.Core;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);
WriteStartupDiagnostics();

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    if (args.ExceptionObject is Exception exception)
    {
        WriteCrashLog("unhandled-exception", exception);
    }
};

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    WriteCrashLog("unobserved-task-exception", args.Exception);
    args.SetObserved();
};

try
{
    using var game = new OpenGarrison.Client.Game1();
    game.Run();
}
catch (Exception ex)
{
    WriteCrashLog("fatal-client-crash", ex);
    
}

static void WriteCrashLog(string kind, Exception exception)
{
    try
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = RuntimePaths.GetLogPath($"client-crash-{timestamp}.log");
        var builder = new StringBuilder();
        builder.AppendLine(FormattableString.Invariant($"kind: {kind}"));
        builder.AppendLine(FormattableString.Invariant($"timestamp: {DateTimeOffset.Now:O}"));
        builder.AppendLine(FormattableString.Invariant($"baseDirectory: {AppContext.BaseDirectory}"));
        builder.AppendLine();
        builder.AppendLine(exception.ToString());
        File.WriteAllText(path, builder.ToString());
    }
    catch
    {
    }
}

static void WriteStartupDiagnostics()
{
    try
    {
        var path = RuntimePaths.GetLogPath("client-startup.log");
        var builder = new StringBuilder();
        builder.AppendLine(FormattableString.Invariant($"timestamp: {DateTimeOffset.Now:O}"));
        builder.AppendLine(FormattableString.Invariant($"baseDirectory: {AppContext.BaseDirectory}"));
        builder.AppendLine(FormattableString.Invariant($"currentDirectory: {Directory.GetCurrentDirectory()}"));
        builder.AppendLine(FormattableString.Invariant($"processPath: {Environment.ProcessPath ?? string.Empty}"));
        builder.AppendLine(FormattableString.Invariant($"environmentVersion: {Environment.Version}"));
        builder.AppendLine(FormattableString.Invariant($"frameworkDescription: {RuntimeInformation.FrameworkDescription}"));
        builder.AppendLine(FormattableString.Invariant($"osDescription: {RuntimeInformation.OSDescription}"));
        builder.AppendLine(FormattableString.Invariant($"processArchitecture: {RuntimeInformation.ProcessArchitecture}"));
        builder.AppendLine(FormattableString.Invariant($"osArchitecture: {RuntimeInformation.OSArchitecture}"));
        builder.AppendLine(FormattableString.Invariant($"is64BitProcess: {Environment.Is64BitProcess}"));
        builder.AppendLine();
        builder.AppendLine("[package-files]");
        foreach (var fileName in new[]
                 {
                     "version.txt",
                     "release-channel.txt",
                     "OG2.dll",
                     "OG2.deps.json",
                     "OG2.runtimeconfig.json",
                     "OG2.Game.exe",
                     "OG2.Game.runtimeconfig.json",
                     "SDL2.dll",
                     "openal.dll",
                     "MonoGame.Framework.dll",
                 })
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, fileName);
            builder.AppendLine(FormattableString.Invariant(
                $"{fileName}: exists={File.Exists(filePath)} bytes={(File.Exists(filePath) ? new FileInfo(filePath).Length : 0)}"));
        }

        File.WriteAllText(path, builder.ToString());
    }
    catch
    {
    }
}
