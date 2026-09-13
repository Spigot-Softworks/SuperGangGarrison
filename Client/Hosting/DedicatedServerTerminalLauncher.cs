using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenGarrison.Client;

internal static class DedicatedServerTerminalLauncher
{
    public static int Start(ProcessStartInfo info, bool visible = true)
    {
        if (info.UseShellExecute) throw new ArgumentException("Terminal launches require direct process creation.", nameof(info));
        if (OperatingSystem.IsWindows()) return StartWindows(info, visible);
        foreach (var candidate in new[] { "x-terminal-emulator", "gnome-terminal", "konsole", "xterm" })
        {
            var executable = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Select(directory => Path.Combine(directory, candidate)).FirstOrDefault(File.Exists);
            if (executable is null) continue;
            var terminal = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = info.WorkingDirectory };
            terminal.Environment.Clear();
            foreach (var entry in info.Environment) terminal.Environment[entry.Key] = entry.Value;
            terminal.ArgumentList.Add(candidate == "gnome-terminal" ? "--" : "-e");
            terminal.ArgumentList.Add(info.FileName);
            foreach (var argument in info.ArgumentList) terminal.ArgumentList.Add(argument);
            using var process = Process.Start(terminal) ?? throw new InvalidOperationException("Terminal did not start.");
            return process.Id;
        }
        throw new InvalidOperationException("No supported terminal is installed. Use Start Server to host in the background.");
    }

    private static int StartWindows(ProcessStartInfo info, bool visible)
    {
        var command = new StringBuilder(string.Join(" ", new[] { info.FileName }.Concat(info.ArgumentList).Select(QuoteWindowsArgument)));
        var environmentText = string.Join('\0', info.Environment.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Where(entry => entry.Value is not null).Select(entry => entry.Key + "=" + entry.Value)) + "\0\0";
        var environment = Marshal.StringToHGlobalUni(environmentText);
        try
        {
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Flags = 1, ShowWindow = (short)(visible ? 1 : 0) };
            // CREATE_NEW_CONSOLE | CREATE_UNICODE_ENVIRONMENT. The explicit
            // console does not inherit the GUI launcher's console/lifetime.
            if (!CreateProcess(null, command, IntPtr.Zero, IntPtr.Zero, false, 0x410,
                    environment, info.WorkingDirectory, ref startup, out var process))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(process.Thread);
            CloseHandle(process.Process);
            return process.ProcessId;
        }
        finally { Marshal.FreeHGlobal(environment); }
    }

    internal static string QuoteWindowsArgument(string value)
    {
        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { backslashes++; continue; }
            result.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
            result.Append(character);
            backslashes = 0;
        }
        return result.Append('\\', backslashes * 2).Append('"').ToString();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public short ShowWindow, ReservedSize;
        public IntPtr ReservedData, StdInput, StdOutput, StdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string? application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags,
        IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation process);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
