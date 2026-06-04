#nullable enable

using System;
using System.Diagnostics;
using System.IO;

namespace OpenGarrison.Client;

internal static class HostSetupFileDialogs
{
    public static bool TryOpenPlaylistFile(string? initialPath, out string selectedPath)
    {
        return TryChooseFile(
            "Import Playlist",
            "Playlist files (*.txt)|*.txt|All files (*.*)|*.*",
            initialPath,
            out selectedPath);
    }

    public static bool TrySavePlaylistFile(string? initialPath, out string selectedPath)
    {
        return TryChooseSaveFile(
            "Export Playlist",
            "Playlist files (*.txt)|*.txt|All files (*.*)|*.*",
            initialPath,
            out selectedPath);
    }

    private static bool TryChooseFile(string title, string filter, string? initialPath, out string selectedPath)
    {
        var script = string.Concat(
            "Add-Type -AssemblyName System.Windows.Forms;",
            "$d=New-Object System.Windows.Forms.OpenFileDialog;",
            "$d.Title=", ToPowerShellSingleQuotedString(title), ";",
            "$d.Filter=", ToPowerShellSingleQuotedString(filter), ";",
            SetInitialDialogDirectoryScript(initialPath),
            "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Write($d.FileName)}");
        return TryRunDialogScript(script, out selectedPath);
    }

    private static bool TryChooseSaveFile(string title, string filter, string? initialPath, out string selectedPath)
    {
        var script = string.Concat(
            "Add-Type -AssemblyName System.Windows.Forms;",
            "$d=New-Object System.Windows.Forms.SaveFileDialog;",
            "$d.Title=", ToPowerShellSingleQuotedString(title), ";",
            "$d.Filter=", ToPowerShellSingleQuotedString(filter), ";",
            "$d.DefaultExt='txt';",
            "$d.AddExtension=$true;",
            SetInitialDialogDirectoryScript(initialPath),
            "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){[Console]::Write($d.FileName)}");
        return TryRunDialogScript(script, out selectedPath);
    }

    private static string SetInitialDialogDirectoryScript(string? initialPath)
    {
        var trimmed = (initialPath ?? string.Empty).Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(
            "$p=", ToPowerShellSingleQuotedString(trimmed), ";",
            "if(Test-Path -LiteralPath $p){",
            "$i=Get-Item -LiteralPath $p;",
            "if($i.PSIsContainer){$d.InitialDirectory=$i.FullName}else{$d.InitialDirectory=$i.DirectoryName;$d.FileName=$i.Name}",
            "};");
    }

    private static bool TryRunDialogScript(string script, out string selectedPath)
    {
        selectedPath = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -STA -Command {ToCommandLineArgument(script)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }

            selectedPath = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return selectedPath.Length > 0;
        }
        catch
        {
            selectedPath = string.Empty;
            return false;
        }
    }

    private static string ToPowerShellSingleQuotedString(string value)
    {
        return string.Concat("'", value.Replace("'", "''", StringComparison.Ordinal), "'");
    }

    private static string ToCommandLineArgument(string value)
    {
        return string.Concat("\"", value.Replace("\"", "\\\"", StringComparison.Ordinal), "\"");
    }
}
