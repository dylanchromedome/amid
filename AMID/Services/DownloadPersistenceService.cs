using System.IO;
using System.Text.Json;
using AMID.Models;

namespace AMID.Services;

public sealed class DownloadPersistenceService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public DownloadPersistenceService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        StatePath = Path.Combine(localAppData, "AMID", "downloads.json");
    }

    public string StatePath { get; }

    public IReadOnlyList<DownloadItem> Load()
    {
        if (!File.Exists(StatePath))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(StatePath);
            List<PersistedDownloadItem>? persistedItems = JsonSerializer.Deserialize<List<PersistedDownloadItem>>(json, _jsonOptions);
            if (persistedItems is null)
            {
                return [];
            }

            return persistedItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Url))
                .Select(ToDownloadItem)
                .ToList();
        }
        catch (JsonException)
        {
            BackupUnreadableStateFile();
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<DownloadItem> downloads)
    {
        string? folder = Path.GetDirectoryName(StatePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var persistedItems = downloads.Select(PersistedDownloadItem.FromDownloadItem).ToList();
        string json = JsonSerializer.Serialize(persistedItems, _jsonOptions);
        File.WriteAllText(StatePath, json);
    }

    private static DownloadItem ToDownloadItem(PersistedDownloadItem persistedItem)
    {
        var item = new DownloadItem(persistedItem.FileName, persistedItem.Url);
        bool partialFileExists = !string.IsNullOrWhiteSpace(persistedItem.PartialPath)
                                 && File.Exists(persistedItem.PartialPath);

        item.RestoreState(
            persistedItem.DestinationPath,
            persistedItem.PartialPath,
            persistedItem.DownloadedBytes,
            persistedItem.TotalBytes,
            persistedItem.SupportsResume,
            persistedItem.Status,
            persistedItem.ErrorMessage,
            isOld: true,
            partialFileExists);

        return item;
    }

    private void BackupUnreadableStateFile()
    {
        if (!File.Exists(StatePath))
        {
            return;
        }

        string backupPath = $"{StatePath}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Copy(StatePath, backupPath, overwrite: false);
    }

    private sealed class PersistedDownloadItem
    {
        public string FileName { get; set; } = "download";

        public string Url { get; set; } = string.Empty;

        public string DestinationPath { get; set; } = string.Empty;

        public string PartialPath { get; set; } = string.Empty;

        public long DownloadedBytes { get; set; }

        public long? TotalBytes { get; set; }

        public bool? SupportsResume { get; set; }

        public string Status { get; set; } = "Queued";

        public string ErrorMessage { get; set; } = "--";

        public bool IsOld { get; set; }

        public static PersistedDownloadItem FromDownloadItem(DownloadItem item)
        {
            return new PersistedDownloadItem
            {
                FileName = item.FileName,
                Url = item.Url,
                DestinationPath = item.DestinationPath,
                PartialPath = item.PartialPath,
                DownloadedBytes = item.DownloadedBytes,
                TotalBytes = item.TotalBytes,
                SupportsResume = item.SupportsResume,
                Status = item.Status,
                ErrorMessage = item.ErrorMessage,
                IsOld = item.IsOld
            };
        }
    }
}
