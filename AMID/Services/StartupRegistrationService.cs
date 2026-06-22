using System.Diagnostics;
using Microsoft.Win32;

namespace AMID.Services;

public static class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AMID";

    public static void SetStartWithWindows(bool enabled)
    {
        using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath)
                                   ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (!enabled)
        {
            runKey.DeleteValue(AppName, throwOnMissingValue: false);
            return;
        }

        string executablePath = Environment.ProcessPath
                                ?? Process.GetCurrentProcess().MainModule?.FileName
                                ?? throw new InvalidOperationException("Could not find AMID.exe for startup registration.");

        runKey.SetValue(AppName, $"\"{executablePath}\" --minimized", RegistryValueKind.String);
    }

    public static bool IsStartWithWindowsEnabled()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return runKey?.GetValue(AppName) is string value
               && value.Contains("AMID", StringComparison.OrdinalIgnoreCase);
    }
}
