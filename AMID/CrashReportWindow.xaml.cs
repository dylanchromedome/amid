using System.Windows;

namespace AMID;

public partial class CrashReportWindow : Window
{
    public CrashReportWindow(CrashReportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ContinueButton.Visibility = viewModel.CanContinue ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool ShouldContinue { get; private set; }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ReportTextBox.Text);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        ShouldContinue = true;
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ShouldContinue = false;
        DialogResult = false;
        Close();
    }
}

public sealed class CrashReportViewModel
{
    public CrashReportViewModel(
        string summaryText,
        string reportText,
        string reportPath,
        bool canContinue)
    {
        SummaryText = summaryText;
        ReportText = reportText;
        ReportPathText = $"Saved report: {reportPath}";
        CanContinue = canContinue;
    }

    public string SummaryText { get; }

    public string ReportText { get; }

    public string ReportPathText { get; }

    public bool CanContinue { get; }
}
