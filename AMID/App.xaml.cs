using System.Windows;
using System.Windows.Threading;
using AMID.Services;

namespace AMID;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        CrashReportAction action = CrashReportService.ShowCrashReport(
            e.Exception,
            "WPF dispatcher",
            canContinue: true);

        if (action == CrashReportAction.CloseApplication)
        {
            Shutdown(1);
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        try
        {
            Dispatcher.Invoke(() =>
            {
                CrashReportService.ShowCrashReport(
                    exception,
                    "Unhandled application exception",
                    canContinue: false);
            });
        }
        catch
        {
            CrashReportService.WriteCrashReport(
                exception,
                "Unhandled application exception");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();

        Dispatcher.BeginInvoke(() =>
        {
            CrashReportAction action = CrashReportService.ShowCrashReport(
                e.Exception,
                "Unobserved background task",
                canContinue: true);

            if (action == CrashReportAction.CloseApplication)
            {
                Shutdown(1);
            }
        });
    }
}
