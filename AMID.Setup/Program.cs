using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace AMID.Setup;

internal static class Program
{
    private const string AppName = "AMID";
    private const string Publisher = "dylanchromedome";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AMID";
    private const string AppPathsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\App Paths\AMID.exe";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            bool quiet = HasFlag(args, "--quiet");
            bool uninstall = HasFlag(args, "--uninstall")
                             || string.Equals(
                                 Path.GetFileNameWithoutExtension(Environment.ProcessPath),
                                 "uninstall",
                                 StringComparison.OrdinalIgnoreCase);

            return uninstall
                ? Uninstall(args, quiet)
                : Install(args, quiet);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("AMID setup failed.");
            Console.Error.WriteLine(ex.Message);
            PauseIfNeeded(args);
            return 1;
        }
    }

    private static int Install(string[] args, bool quiet)
    {
        string sourceRoot = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string sourceAppDir = Path.Combine(sourceRoot, "AMID");
        if (!File.Exists(Path.Combine(sourceAppDir, "AMID.exe")))
        {
            throw new InvalidOperationException(
                "Could not find the AMID app folder. Run install.exe from the extracted AMID release zip.");
        }

        string installDir = GetInstallDir(args);
        ValidateInstallDir(installDir);
        EnsureAppNotRunningFrom(installDir);

        Console.WriteLine($"Installing AMID to {installDir}");
        Directory.CreateDirectory(installDir);
        ClearDirectory(installDir);
        CopyDirectory(sourceAppDir, installDir);
        CopySelfAsUninstaller(installDir);
        WriteInstallMetadata(installDir);
        CreateStartMenuShortcut(installDir);
        RegisterAppPaths(installDir);
        RegisterUninstallEntry(installDir);

        Console.WriteLine("AMID installed.");
        Console.WriteLine("Windows Search will find AMID through the Start Menu shortcut.");
        WriteChromeExtensionInstructions(installDir);
        ShowChromeExtensionInstructions(installDir, quiet);
        Console.WriteLine($"Run: {Path.Combine(installDir, "AMID.exe")}");
        PauseIfNeeded(args, quiet);
        return 0;
    }

    private static int Uninstall(string[] args, bool quiet)
    {
        string installDir = GetInstallDir(args, AppContext.BaseDirectory);
        ValidateInstallDir(installDir);

        Console.WriteLine($"Uninstalling AMID from {installDir}");
        EnsureAppNotRunningFrom(installDir);
        DeleteStartMenuShortcut();
        DeleteRegistryKey(Registry.LocalMachine, UninstallKeyPath);
        DeleteRegistryKey(Registry.LocalMachine, AppPathsKeyPath);
        ScheduleInstallDirectoryRemoval(installDir);

        Console.WriteLine("AMID uninstall started.");
        Console.WriteLine("Saved downloads and settings under your user profile were left in place.");
        PauseIfNeeded(args, quiet);
        return 0;
    }

    private static string GetInstallDir(string[] args, string? fallback = null)
    {
        string? explicitPath = GetOption(args, "--install-dir");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return Path.GetFullPath(fallback);
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, AppName);
    }

    private static void ValidateInstallDir(string installDir)
    {
        string fullPath = Path.GetFullPath(installDir).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullPath)
            || string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            || !fullPath.EndsWith(AppName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to install or uninstall unsafe path: {fullPath}");
        }
    }

    private static void EnsureAppNotRunningFrom(string installDir)
    {
        string fullInstallDir = Path.GetFullPath(installDir).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        foreach (Process process in Process.GetProcessesByName("AMID"))
        {
            try
            {
                string? path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path)
                    && path.StartsWith(fullInstallDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Close AMID before installing or uninstalling.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void ClearDirectory(string directory)
    {
        foreach (string childDirectory in Directory.GetDirectories(directory))
        {
            Directory.Delete(childDirectory, recursive: true);
        }

        foreach (string file in Directory.GetFiles(directory))
        {
            File.Delete(file);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static void CopySelfAsUninstaller(string installDir)
    {
        string? setupPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(setupPath) || !File.Exists(setupPath))
        {
            throw new InvalidOperationException("Could not locate install.exe.");
        }

        File.Copy(setupPath, Path.Combine(installDir, "uninstall.exe"), overwrite: true);
    }

    private static void WriteInstallMetadata(string installDir)
    {
        string appExe = Path.Combine(installDir, "AMID.exe");
        var metadata = new
        {
            app = AppName,
            version = FileVersionInfo.GetVersionInfo(appExe).ProductVersion ?? "unknown",
            installedAt = DateTimeOffset.Now,
            installDir,
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AMID",
                "settings.json")
        };

        string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(installDir, "install.json"), json);
    }

    private static string GetChromeExtensionDirectory(string installDir)
    {
        return Path.Combine(installDir, "chrome-extension");
    }

    private static void WriteChromeExtensionInstructions(string installDir)
    {
        string extensionDirectory = GetChromeExtensionDirectory(installDir);

        Console.WriteLine();
        Console.WriteLine("Chrome extension setup:");
        Console.WriteLine("1. Start AMID.");
        Console.WriteLine("2. Open Chrome and go to chrome://extensions.");
        Console.WriteLine("3. Enable Developer mode.");
        Console.WriteLine("4. Click Load unpacked.");
        Console.WriteLine("5. Select this folder:");
        Console.WriteLine(extensionDirectory);
        Console.WriteLine();
        Console.WriteLine("Chrome downloads normally when AMID is closed or unreachable.");
        Console.WriteLine();
    }

    private static void ShowChromeExtensionInstructions(string installDir, bool quiet)
    {
        if (quiet)
        {
            return;
        }

        try
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            using var form = new ChromeExtensionInstructionsForm(GetChromeExtensionDirectory(installDir));
            System.Windows.Forms.Application.Run(form);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not show the Chrome extension helper window: {ex.Message}");
        }
    }

    private static void CreateStartMenuShortcut(string installDir)
    {
        string programsFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        string shortcutPath = Path.Combine(programsFolder, "AMID.lnk");
        string targetPath = Path.Combine(installDir, "AMID.exe");
        CreateShortcut(shortcutPath, targetPath, installDir, "AMID download manager");
    }

    private static void DeleteStartMenuShortcut()
    {
        string shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            "AMID.lnk");

        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            throw new InvalidOperationException("Windows Script Host is unavailable; could not create shortcut.");
        }

        object? shell = Activator.CreateInstance(shellType);
        if (shell is null)
        {
            throw new InvalidOperationException("Could not create Windows shortcut helper.");
        }

        object? shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath]);

            if (shortcut is null)
            {
                throw new InvalidOperationException("Could not create shortcut object.");
            }

            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [workingDirectory]);
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, [description]);
            shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, [$"{targetPath},0"]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static void RegisterAppPaths(string installDir)
    {
        using RegistryKey key = Registry.LocalMachine.CreateSubKey(AppPathsKeyPath)
                                ?? throw new InvalidOperationException("Could not register AMID app path.");
        key.SetValue(string.Empty, Path.Combine(installDir, "AMID.exe"), RegistryValueKind.String);
        key.SetValue("Path", installDir, RegistryValueKind.String);
    }

    private static void RegisterUninstallEntry(string installDir)
    {
        string appExe = Path.Combine(installDir, "AMID.exe");
        string uninstallExe = Path.Combine(installDir, "uninstall.exe");
        string version = FileVersionInfo.GetVersionInfo(appExe).ProductVersion ?? "unknown";

        using RegistryKey key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath)
                                ?? throw new InvalidOperationException("Could not register AMID uninstall entry.");

        key.SetValue("DisplayName", "AMID", RegistryValueKind.String);
        key.SetValue("DisplayVersion", version, RegistryValueKind.String);
        key.SetValue("Publisher", Publisher, RegistryValueKind.String);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);
        key.SetValue("DisplayIcon", $"{appExe},0", RegistryValueKind.String);
        key.SetValue("UninstallString", $"\"{uninstallExe}\"", RegistryValueKind.String);
        key.SetValue("QuietUninstallString", $"\"{uninstallExe}\" --quiet", RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void DeleteRegistryKey(RegistryKey root, string keyPath)
    {
        root.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
    }

    private static void ScheduleInstallDirectoryRemoval(string installDir)
    {
        string currentProcessId = Environment.ProcessId.ToString();
        string script =
            $"Wait-Process -Id {currentProcessId} -ErrorAction SilentlyContinue; " +
            "Start-Sleep -Milliseconds 500; " +
            $"Remove-Item -LiteralPath {QuotePowerShellString(installDir)} -Recurse -Force";

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {QuotePowerShellString(script)}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Any(argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetOption(string[] args, string name)
    {
        string prefix = name + "=";
        return args.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
    }

    private static void PauseIfNeeded(string[] args, bool quiet = false)
    {
        if (quiet || HasFlag(args, "--quiet") || Console.IsInputRedirected)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey(intercept: true);
    }

    private static string QuotePowerShellString(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
