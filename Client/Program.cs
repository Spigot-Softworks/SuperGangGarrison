using System;
using System.IO;
using System.Text;
using System.Globalization;
using OpenGarrison.Core;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

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
    throw;
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
