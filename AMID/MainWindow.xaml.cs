using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AMID.Models;
using AMID.Services;

namespace AMID;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DownloadService _downloadService = new();
    private readonly DownloadPersistenceService _persistenceService = new();
    private readonly GitHubUpdateService _updateService = new();
    private readonly Dictionary<DownloadItem, DownloadRunContext> _downloadContexts = new();
    private readonly DispatcherTimer _saveTimer;
    private ChromeIntegrationServer? _chromeIntegrationServer;
    private DownloadCategory? _selectedCategory;
    private DownloadItem? _selectedDownload;
    private int _activeDownloadCount;
    private bool _saveWarningShown;
    private bool _startupUpdateCheckStarted;

    public MainWindow()
    {
        Downloads = new ObservableCollection<DownloadItem>();
        DownloadsView = CollectionViewSource.GetDefaultView(Downloads);
        DownloadsView.Filter = FilterDownload;
        Categories =
        [
            new("all", "All"),
            new("downloading", "Downloading"),
            new("completed", "Completed"),
            new("failed", "Failed"),
            new("paused", "Paused"),
            new("old", "Old")
        ];
        _selectedCategory = Categories[0];

        LogEntries = new ObservableCollection<string>();
        AddLog("AMID started.");
        AddLog("Stage 6 release build ready.");

        DownloadFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _saveTimer.Tick += SaveTimer_Tick;

        LoadPersistedDownloads();
        UpdateCategoryCounts();
        _chromeIntegrationServer = new ChromeIntegrationServer(HandleChromeDownloadAsync);

        InitializeComponent();
        DataContext = this;
        Loaded += MainWindow_Loaded;
        StartChromeIntegrationServer();
    }

    public ObservableCollection<DownloadItem> Downloads { get; }

    public ICollectionView DownloadsView { get; }

    public ObservableCollection<DownloadCategory> Categories { get; }

    public ObservableCollection<string> LogEntries { get; }

    public string DownloadFolder { get; }

    public string TotalDownloadsText => $"Downloads: {Downloads.Count}";

    public string ActiveDownloadsText => $"Active: {_activeDownloadCount}";

    public string DownloadFolderText => $"Folder: {DownloadFolder}";

    public string ChromeIntegrationText => _chromeIntegrationServer?.IsRunning == true
        ? $"Chrome: listening on 127.0.0.1:{ChromeIntegrationServer.Port}"
        : "Chrome: not connected";

    public DownloadCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value)
            {
                return;
            }

            _selectedCategory = value;
            OnPropertyChanged(nameof(SelectedCategory));
            DownloadsView.Refresh();
        }
    }

    public DownloadItem? SelectedDownload
    {
        get => _selectedDownload;
        set
        {
            if (_selectedDownload == value)
            {
                return;
            }

            _selectedDownload = value;
            OnPropertyChanged(nameof(SelectedDownload));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected override void OnClosing(CancelEventArgs e)
    {
        _saveTimer.Stop();

        foreach ((DownloadItem item, DownloadRunContext context) in _downloadContexts.ToArray())
        {
            if (item.SupportsResume == true)
            {
                context.StopReason = DownloadStopReason.Pause;
                RefreshDownloadedBytesFromPartial(item);
                item.MarkPaused();
            }
            else
            {
                context.StopReason = DownloadStopReason.Cancel;
                DeleteDownloadResidue(item);
                item.ClearPartialPath();
                item.MarkCanceled();
            }

            context.CancellationTokenSource.Cancel();
        }

        _chromeIntegrationServer?.Stop();
        SaveDownloads();
        base.OnClosing(e);
    }

    private void AddDownload_Click(object sender, RoutedEventArgs e)
    {
        AddDownloadFromTextBox();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_startupUpdateCheckStarted)
        {
            return;
        }

        _startupUpdateCheckStarted = true;
        _ = CheckForUpdatesAsync(showNoUpdateMessage: false);
    }

    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        AddDownloadFromTextBox();
    }

    private void PauseSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDownload is null)
        {
            AddLog("No download selected to pause.");
            return;
        }

        if (SelectedDownload.SupportsResume != true)
        {
            AddLog($"Cannot pause {SelectedDownload.FileName}; the server has not confirmed HTTP range support.");
            return;
        }

        if (!_downloadContexts.TryGetValue(SelectedDownload, out DownloadRunContext? context))
        {
            AddLog($"Cannot pause {SelectedDownload.FileName}; it is not active.");
            return;
        }

        context.StopReason = DownloadStopReason.Pause;
        SelectedDownload.MarkPausing();
        context.CancellationTokenSource.Cancel();
        AddLog($"Pause requested for {SelectedDownload.FileName}.");
    }

    private void RetrySelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDownload is null)
        {
            AddLog("No download selected to retry.");
            return;
        }

        if (SelectedDownload.IsActive)
        {
            AddLog($"{SelectedDownload.FileName} is already active.");
            return;
        }

        StartDownload(SelectedDownload, isRetry: true);
    }

    private void CancelSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDownload is null)
        {
            AddLog("No download selected to cancel.");
            return;
        }

        if (_downloadContexts.TryGetValue(SelectedDownload, out DownloadRunContext? context))
        {
            context.StopReason = DownloadStopReason.Cancel;
            SelectedDownload.MarkCanceling();
            context.CancellationTokenSource.Cancel();
            AddLog($"Cancel requested for {SelectedDownload.FileName}.");
            return;
        }

        if (!SelectedDownload.CanCancel)
        {
            AddLog($"Cannot cancel {SelectedDownload.FileName}; it is already finished.");
            return;
        }

        DeleteDownloadResidue(SelectedDownload);
        SelectedDownload.ClearPartialPath();
        SelectedDownload.MarkCanceled();
        QueueSave();
        AddLog($"Canceled {SelectedDownload.FileName}.");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DownloadFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = DownloadFolder,
            UseShellExecute = true
        });
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        _ = CheckForUpdatesAsync(showNoUpdateMessage: true);
    }

    private async Task CheckForUpdatesAsync(bool showNoUpdateMessage)
    {
        try
        {
            AddLog("Checking GitHub releases for updates.");
            GitHubUpdateInfo? updateInfo = await _updateService.CheckForUpdateAsync(CancellationToken.None);
            if (updateInfo is null)
            {
                AddLog("No AMID update found.");
                if (showNoUpdateMessage)
                {
                    MessageBox.Show(
                        this,
                        $"AMID { _updateService.CurrentVersionText } is up to date.",
                        "AMID Updates",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }

            var prompt = new UpdatePromptWindow(updateInfo, _updateService.CurrentVersionText)
            {
                Owner = this
            };

            if (prompt.ShowDialog() != true)
            {
                AddLog($"Skipped update {updateInfo.TagName}.");
                return;
            }

            AddLog($"Downloading update {updateInfo.TagName}.");
            Mouse.OverrideCursor = Cursors.Wait;
            string zipPath = await _updateService.DownloadUpdateAsync(
                updateInfo,
                progress: null,
                cancellationToken: CancellationToken.None);
            Mouse.OverrideCursor = null;

            AddLog($"Downloaded update package {updateInfo.AssetName}.");
            _updateService.LaunchUpdater(zipPath, Environment.ProcessId);
            AddLog("Updater launched. AMID will close and reopen after the update.");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            AddLog($"Update check failed: {ex.Message}");
            if (showNoUpdateMessage)
            {
                MessageBox.Show(
                    this,
                    $"Could not check for updates:\n\n{ex.Message}",
                    "AMID Updates",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private void AddDownloadFromTextBox()
    {
        string url = UrlTextBox.Text.Trim();
        if (!IsSupportedUrl(url))
        {
            AddLog("Enter a valid HTTP or HTTPS URL.");
            return;
        }

        AddDownloadFromUrl(url, "Added");
        UrlTextBox.Clear();
    }

    private DownloadItem AddDownloadFromUrl(
        string url,
        string logPrefix,
        string preferredFileName = "")
    {
        string normalizedUrl = url.Trim();
        string displayFileName = string.IsNullOrWhiteSpace(preferredFileName)
            ? DownloadService.GetSuggestedFileName(normalizedUrl)
            : preferredFileName;
        var item = new DownloadItem(displayFileName, normalizedUrl, preferredFileName);
        AddDownloadItem(item);
        SelectedDownload = item;
        OnPropertyChanged(nameof(TotalDownloadsText));

        AddLog($"{logPrefix} {item.FileName}.");
        StartDownload(item, isRetry: false);
        QueueSave();
        return item;
    }

    private void StartDownload(DownloadItem item, bool isRetry)
    {
        if (_downloadContexts.ContainsKey(item))
        {
            AddLog($"{item.FileName} is already active.");
            return;
        }

        bool canContinuePartial = isRetry
                                  && item.SupportsResume == true
                                  && !string.IsNullOrWhiteSpace(item.PartialPath)
                                  && File.Exists(item.PartialPath);

        if (isRetry && !canContinuePartial)
        {
            DeleteDownloadResidue(item);
            item.ClearPartialPath();
        }

        item.MarkStarting(isRetry);
        var context = new DownloadRunContext(new CancellationTokenSource());
        _downloadContexts[item] = context;
        if (isRetry)
        {
            AddLog(canContinuePartial
                ? $"Retrying {item.FileName} from the saved partial file."
                : $"Retrying {item.FileName} from the beginning.");
        }

        _ = RunDownloadAsync(item, context);
    }

    private async Task RunDownloadAsync(DownloadItem item, DownloadRunContext context)
    {
        _activeDownloadCount++;
        OnPropertyChanged(nameof(ActiveDownloadsText));

        var progress = new Progress<DownloadProgress>(downloadProgress =>
        {
            item.UpdateProgress(
                downloadProgress.DownloadedBytes,
                downloadProgress.TotalBytes,
                downloadProgress.SpeedBytesPerSecond,
                downloadProgress.Status,
                downloadProgress.FileName,
                downloadProgress.DestinationPath,
                downloadProgress.PartialPath,
                downloadProgress.SupportsResume);
        });

        try
        {
            DownloadResult result = await _downloadService.DownloadAsync(
                new DownloadRequest(
                    item.Url,
                    DownloadFolder,
                    item.PreferredFileName,
                    item.DestinationPath,
                    item.PartialPath),
                progress,
                context.CancellationTokenSource.Token);

            item.MarkCompleted(result.DestinationPath);
            AddLog($"Completed {item.FileName}.");
        }
        catch (OperationCanceledException) when (context.StopReason == DownloadStopReason.Pause)
        {
            RefreshDownloadedBytesFromPartial(item);
            item.MarkPaused();
            AddLog($"Paused {item.FileName}.");
        }
        catch (OperationCanceledException)
        {
            DeleteDownloadResidue(item);
            item.ClearPartialPath();
            item.MarkCanceled();
            AddLog($"Canceled {item.FileName}.");
        }
        catch (ResumeNotSupportedException ex)
        {
            item.MarkResumeNotSupported(ex.Message);
            AddLog($"Resume not supported for {item.FileName}: {ex.Message}");
        }
        catch (Exception ex)
        {
            string message = GetReadableError(ex);
            if (item.SupportsResume != true)
            {
                DownloadService.DeletePartialFile(item.PartialPath);
                item.ClearPartialPath();
            }
            else
            {
                RefreshDownloadedBytesFromPartial(item);
            }

            item.MarkFailed(message);
            AddLog($"Failed {item.FileName}: {message}");
        }
        finally
        {
            _downloadContexts.Remove(item);
            context.CancellationTokenSource.Dispose();
            _activeDownloadCount--;
            OnPropertyChanged(nameof(ActiveDownloadsText));
            QueueSave();
        }
    }

    private void RefreshDownloadedBytesFromPartial(DownloadItem item)
    {
        if (string.IsNullOrWhiteSpace(item.PartialPath) || !File.Exists(item.PartialPath))
        {
            return;
        }

        long partialLength = new FileInfo(item.PartialPath).Length;
        item.UpdateProgress(
            partialLength,
            item.TotalBytes,
            0,
            item.Status,
            item.FileName,
            item.DestinationPath,
            item.PartialPath,
            item.SupportsResume);
    }

    private void DeleteDownloadResidue(DownloadItem item)
    {
        DeleteResidueFile(item.PartialPath);

        if (item.Status != "Completed"
            && !string.IsNullOrWhiteSpace(item.DestinationPath)
            && !string.Equals(item.DestinationPath, item.PartialPath, StringComparison.OrdinalIgnoreCase))
        {
            DeleteResidueFile(item.DestinationPath);
        }
    }

    private void DeleteResidueFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddLog($"Could not delete leftover file {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    private void AddDownloadItem(DownloadItem item)
    {
        item.PropertyChanged += DownloadItem_PropertyChanged;
        Downloads.Add(item);
        UpdateCategoryCounts();
        DownloadsView.Refresh();
    }

    private void DownloadItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DownloadItem.Status) or nameof(DownloadItem.IsActive) or nameof(DownloadItem.IsOld))
        {
            UpdateCategoryCounts();
            DownloadsView.Refresh();
        }

        QueueSave();
    }

    private void LoadPersistedDownloads()
    {
        try
        {
            foreach (DownloadItem item in _persistenceService.Load())
            {
                AddDownloadItem(item);
            }

            if (Downloads.Count > 0)
            {
                AddLog($"Loaded {Downloads.Count} saved downloads.");
                OnPropertyChanged(nameof(TotalDownloadsText));
            }
        }
        catch (Exception ex)
        {
            AddLog($"Could not load saved downloads: {ex.Message}");
        }
    }

    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        SaveDownloads();
    }

    private void QueueSave()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(QueueSave);
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveDownloads()
    {
        try
        {
            _persistenceService.Save(Downloads);
        }
        catch (Exception ex)
        {
            if (_saveWarningShown)
            {
                return;
            }

            _saveWarningShown = true;
            AddLog($"Could not save downloads: {ex.Message}");
        }
    }

    private bool FilterDownload(object item)
    {
        return item is DownloadItem download
               && MatchesCategory(download, SelectedCategory?.Key ?? "all");
    }

    private void UpdateCategoryCounts()
    {
        foreach (DownloadCategory category in Categories)
        {
            category.Count = Downloads.Count(download => MatchesCategory(download, category.Key));
        }
    }

    private static bool MatchesCategory(DownloadItem item, string categoryKey)
    {
        return categoryKey switch
        {
            "all" => !item.IsOld,
            "downloading" => !item.IsOld
                             && (item.IsActive || item.Status is "Connecting" or "Downloading" or "Downloading (no resume)" or "Resuming" or "Retrying" or "Pausing" or "Canceling"),
            "completed" => item.Status == "Completed",
            "failed" => !item.IsOld && (item.Status is "Failed" or "Resume not supported" or "Interrupted"),
            "paused" => !item.IsOld && item.Status == "Paused",
            "old" => item.IsOld || item.Status == "Canceled",
            _ => !item.IsOld
        };
    }

    private void StartChromeIntegrationServer()
    {
        if (_chromeIntegrationServer is null)
        {
            return;
        }

        try
        {
            _chromeIntegrationServer.Start();
            AddLog($"Chrome integration listening on http://127.0.0.1:{ChromeIntegrationServer.Port}.");
        }
        catch (Exception ex)
        {
            AddLog($"Chrome integration unavailable: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(nameof(ChromeIntegrationText));
        }
    }

    private Task<ChromeDownloadBridgeResult> HandleChromeDownloadAsync(
        ChromeDownloadBridgeRequest request,
        CancellationToken cancellationToken)
    {
        return Dispatcher.InvokeAsync(
            () =>
            {
                string url = request.Url?.Trim() ?? string.Empty;
                if (!IsSupportedUrl(url))
                {
                    return ChromeDownloadBridgeResult.CreateRejected("Only HTTP and HTTPS download URLs are supported.");
                }

                string preferredFileName = DownloadService.GetSafeSuggestedFileName(
                    request.Filename,
                    request.Mime);
                DownloadItem item = AddDownloadFromUrl(url, "Chrome sent", preferredFileName);
                return ChromeDownloadBridgeResult.CreateAccepted(item.FileName);
            },
            DispatcherPriority.Normal,
            cancellationToken).Task;
    }

    private static bool IsSupportedUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string GetReadableError(Exception exception)
    {
        if (exception is HttpRequestException httpRequestException && httpRequestException.StatusCode.HasValue)
        {
            int statusCode = (int)httpRequestException.StatusCode.Value;
            return $"HTTP {statusCode} {httpRequestException.StatusCode.Value}";
        }

        return exception.Message;
    }

    private void AddLog(string message)
    {
        string entry = $"{DateTime.Now:t}  {message}";
        LogEntries.Insert(0, entry);
        AppLog.Add(entry);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class DownloadRunContext(CancellationTokenSource cancellationTokenSource)
    {
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        public DownloadStopReason StopReason { get; set; }
    }

    private enum DownloadStopReason
    {
        None,
        Pause,
        Cancel
    }
}

public sealed class DownloadCategory : INotifyPropertyChanged
{
    private int _count;

    public DownloadCategory(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public string Key { get; }

    public string Title { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (_count == value)
            {
                return;
            }

            _count = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
