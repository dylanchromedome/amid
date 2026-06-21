using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AMID.Models;

public sealed class DownloadItem : INotifyPropertyChanged
{
    private string _fileName;
    private string _destinationPath = string.Empty;
    private string _partialPath = string.Empty;
    private double _progressPercent;
    private long _downloadedBytes;
    private long? _totalBytes;
    private bool? _supportsResume;
    private bool _isActive;
    private string _downloadedSize = "0 B";
    private string _totalSize = "Unknown";
    private string _speed = "0 B/s";
    private string _eta = "--";
    private string _status = "Queued";
    private string _errorMessage = "--";

    public DownloadItem(string fileName, string url)
    {
        _fileName = fileName;
        Url = url;
    }

    public string FileName
    {
        get => _fileName;
        private set => SetField(ref _fileName, value);
    }

    public string Url { get; }

    public string DestinationPath
    {
        get => _destinationPath;
        private set => SetField(ref _destinationPath, value);
    }

    public string PartialPath
    {
        get => _partialPath;
        private set => SetField(ref _partialPath, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (SetField(ref _progressPercent, value))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    public string ProgressText => TotalBytes.HasValue ? $"{ProgressPercent:0}%" : "--";

    public long DownloadedBytes
    {
        get => _downloadedBytes;
        private set => SetField(ref _downloadedBytes, value);
    }

    public long? TotalBytes
    {
        get => _totalBytes;
        private set
        {
            if (SetField(ref _totalBytes, value))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    public bool? SupportsResume
    {
        get => _supportsResume;
        private set
        {
            if (SetField(ref _supportsResume, value))
            {
                OnPropertyChanged(nameof(ResumeSupportText));
                NotifyActionPropertiesChanged();
            }
        }
    }

    public string ResumeSupportText => SupportsResume switch
    {
        true => "Yes",
        false => "No",
        _ => "Unknown"
    };

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetField(ref _isActive, value))
            {
                NotifyActionPropertiesChanged();
            }
        }
    }

    public string DownloadedSize
    {
        get => _downloadedSize;
        private set => SetField(ref _downloadedSize, value);
    }

    public string TotalSize
    {
        get => _totalSize;
        private set => SetField(ref _totalSize, value);
    }

    public string Speed
    {
        get => _speed;
        private set => SetField(ref _speed, value);
    }

    public string Eta
    {
        get => _eta;
        private set => SetField(ref _eta, value);
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (SetField(ref _status, value))
            {
                NotifyActionPropertiesChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public bool CanPause => IsActive && SupportsResume == true;

    public bool CanResume => !IsActive
                             && SupportsResume == true
                             && Status is "Paused" or "Failed" or "Interrupted" or "Resume not supported";

    public bool CanCancel => IsActive
                             || Status is "Queued" or "Paused" or "Failed" or "Interrupted" or "Resume not supported";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void MarkStarting(bool isResume)
    {
        IsActive = true;
        Speed = "0 B/s";
        Eta = "--";
        ErrorMessage = "--";
        Status = isResume ? "Resuming" : "Connecting";
    }

    public void UpdateProgress(
        long downloadedBytes,
        long? totalBytes,
        double speedBytesPerSecond,
        string status,
        string fileName,
        string destinationPath,
        string partialPath,
        bool? supportsResume)
    {
        FileName = fileName;
        DestinationPath = destinationPath;
        PartialPath = partialPath;
        DownloadedBytes = downloadedBytes;
        TotalBytes = totalBytes;
        SupportsResume = supportsResume;
        ProgressPercent = totalBytes is > 0
            ? Math.Min(100, downloadedBytes * 100d / totalBytes.Value)
            : 0;
        DownloadedSize = FormatBytes(downloadedBytes);
        TotalSize = totalBytes.HasValue ? FormatBytes(totalBytes.Value) : "Unknown";
        Speed = $"{FormatBytes((long)Math.Max(0, speedBytesPerSecond))}/s";
        Eta = CalculateEta(downloadedBytes, totalBytes, speedBytesPerSecond);
        Status = supportsResume == false && status == "Downloading"
            ? "Downloading (no resume)"
            : status;
    }

    public void MarkPausing()
    {
        Status = "Pausing";
        Speed = "0 B/s";
        Eta = "--";
    }

    public void MarkPaused()
    {
        IsActive = false;
        Speed = "0 B/s";
        Eta = "--";
        Status = "Paused";
    }

    public void MarkCompleted(string destinationPath)
    {
        IsActive = false;
        DestinationPath = destinationPath;
        PartialPath = string.Empty;
        ProgressPercent = TotalBytes is > 0 ? 100 : ProgressPercent;
        Speed = "0 B/s";
        Eta = "Done";
        Status = "Completed";
        ErrorMessage = "--";
    }

    public void MarkCanceling()
    {
        Status = "Canceling";
        Speed = "0 B/s";
        Eta = "--";
    }

    public void MarkCanceled()
    {
        IsActive = false;
        Speed = "0 B/s";
        Eta = "--";
        Status = "Canceled";
    }

    public void MarkFailed(string errorMessage)
    {
        IsActive = false;
        Speed = "0 B/s";
        Eta = "--";
        Status = "Failed";
        ErrorMessage = errorMessage;
    }

    public void MarkResumeNotSupported(string errorMessage)
    {
        IsActive = false;
        SupportsResume = false;
        Speed = "0 B/s";
        Eta = "--";
        Status = "Resume not supported";
        ErrorMessage = errorMessage;
    }

    public void MarkInterrupted()
    {
        IsActive = false;
        Speed = "0 B/s";
        Eta = "--";
        Status = SupportsResume == true ? "Paused" : "Interrupted";
    }

    public void RestoreState(
        string destinationPath,
        string partialPath,
        long downloadedBytes,
        long? totalBytes,
        bool? supportsResume,
        string status,
        string errorMessage,
        bool partialFileExists)
    {
        DestinationPath = destinationPath;
        PartialPath = partialPath;
        DownloadedBytes = downloadedBytes;
        TotalBytes = totalBytes;
        SupportsResume = supportsResume;
        ProgressPercent = totalBytes is > 0
            ? Math.Min(100, downloadedBytes * 100d / totalBytes.Value)
            : 0;
        DownloadedSize = FormatBytes(downloadedBytes);
        TotalSize = totalBytes.HasValue ? FormatBytes(totalBytes.Value) : "Unknown";
        Speed = "0 B/s";
        Eta = status == "Completed" ? "Done" : "--";
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "--" : errorMessage;
        IsActive = false;

        Status = status is "Downloading" or "Downloading (no resume)" or "Connecting" or "Resuming" or "Pausing" or "Canceling" or "Queued"
            ? GetRestartStatus(partialFileExists)
            : status;
    }

    public void ClearPartialPath()
    {
        PartialPath = string.Empty;
    }

    private string GetRestartStatus(bool partialFileExists)
    {
        if (SupportsResume == true && partialFileExists)
        {
            return "Paused";
        }

        return DownloadedBytes > 0 ? "Interrupted" : "Canceled";
    }

    private void NotifyActionPropertiesChanged()
    {
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanCancel));
    }

    private static string CalculateEta(long downloadedBytes, long? totalBytes, double speedBytesPerSecond)
    {
        if (!totalBytes.HasValue || totalBytes.Value <= downloadedBytes || speedBytesPerSecond <= 0)
        {
            return "--";
        }

        double remainingSeconds = (totalBytes.Value - downloadedBytes) / speedBytesPerSecond;
        if (double.IsInfinity(remainingSeconds) || double.IsNaN(remainingSeconds))
        {
            return "--";
        }

        TimeSpan eta = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
        return eta.TotalHours >= 1
            ? $"{(int)eta.TotalHours}:{eta.Minutes:00}:{eta.Seconds:00}"
            : $"{eta.Minutes:00}:{eta.Seconds:00}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
