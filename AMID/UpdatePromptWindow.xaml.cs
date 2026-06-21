using System.Diagnostics;
using System.Windows;
using AMID.Services;

namespace AMID;

public partial class UpdatePromptWindow : Window
{
    public UpdatePromptWindow(GitHubUpdateInfo updateInfo, string currentVersionText)
    {
        UpdateInfo = updateInfo;
        CurrentVersionText = currentVersionText;
        InitializeComponent();
        DataContext = this;
    }

    public GitHubUpdateInfo UpdateInfo { get; }

    public string CurrentVersionText { get; }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UpdateInfo.ReleaseUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = UpdateInfo.ReleaseUrl,
            UseShellExecute = true
        });
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
