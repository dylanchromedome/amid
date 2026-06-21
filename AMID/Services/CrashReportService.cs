using System.IO;
using System.Text;
using System.Windows;

namespace AMID.Services;

public static class CrashReportService
{
    public static CrashReportAction ShowCrashReport(
        Exception exception,
        string source,
        bool canContinue)
    {
        CrashReport report = WriteCrashReport(exception, source);

        try
        {
            var viewModel = new CrashReportViewModel(
                canContinue
                    ? "The app caught a recoverable error. You can copy the report, keep the app open, or close AMID."
                    : "The app caught a serious error. Copy the report if needed, then close AMID.",
                report.Text,
                report.Path,
                canContinue);

            var window = new CrashReportWindow(viewModel)
            {
                Owner = Application.Current?.Windows
                    .OfType<Window>()
                    .FirstOrDefault(window => window.IsActive)
            };

            window.ShowDialog();
            return window.ShouldContinue
                ? CrashReportAction.ContinueApplication
                : CrashReportAction.CloseApplication;
        }
        catch
        {
            MessageBox.Show(
                $"AMID hit an unexpected error. A crash report was saved here:{Environment.NewLine}{report.Path}",
                "AMID Crash Report",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return CrashReportAction.CloseApplication;
        }
    }

    public static CrashReport WriteCrashReport(Exception exception, string source)
    {
        string reportText = BuildReportText(exception, source);
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AMID",
            "CrashReports");

        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"amid-crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, reportText, Encoding.UTF8);
        return new CrashReport(path, reportText);
    }

    private static string BuildReportText(Exception exception, string source)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AMID Crash Report");
        builder.AppendLine("=================");
        builder.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Source: {source}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($".NET: {Environment.Version}");
        builder.AppendLine();
        builder.AppendLine("Exception");
        builder.AppendLine("---------");
        builder.AppendLine(exception.ToString());
        builder.AppendLine();
        builder.AppendLine("Recent AMID Log");
        builder.AppendLine("---------------");
        builder.AppendLine(AppLog.GetRecentText());
        return builder.ToString();
    }
}

public enum CrashReportAction
{
    ContinueApplication,
    CloseApplication
}

public sealed record CrashReport(string Path, string Text);
