using HPSkyStatusUpdator.Configuration;

namespace HPSkyStatusUpdator.Services;

public class DatabaseBackupService : BackgroundService
{
    private readonly SettingsService _settings;
    private readonly ILogger<HypixelService> _logger;

    private static readonly string DataPath =
        Environment.GetEnvironmentVariable("DATA_PATH")
        ?? Path.Combine(AppContext.BaseDirectory, "Data");

    private static readonly string DatabasePath =
        Path.Combine(DataPath, "hpstatus.db");

    private static readonly string BackupFolder =
        Path.Combine(DataPath, "Backups");

    public DatabaseBackupService(
        SettingsService settings,
        ILogger<HypixelService> logger)
    {
        _settings = settings;
        _logger = logger;

        Directory.CreateDirectory(
            BackupFolder);
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                BackupDatabase();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Database backup failed.");
            }


            int minutes = _settings.GetInt(
                SettingKeys.DatabaseBackupIntervalMinutes,
                1440);


            minutes = Math.Max(minutes, 1);


            await Task.Delay(
                TimeSpan.FromMinutes(minutes),
                stoppingToken);
        }
    }


    private void BackupDatabase()
    {
        if (!File.Exists(DatabasePath))
        {
            _logger.LogWarning("Database does not exist.");
            return;
        }


        string fileName =
            $"hpstatus-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.db";


        string destination =
            Path.Combine(
                BackupFolder,
                fileName);


        File.Copy(
            DatabasePath,
            destination,
            true);

        CleanupBackups();
        _logger.LogInformation(
            $"Database backup created: {destination}");
    }
    private void CleanupBackups()
    {
        var files = Directory.GetFiles(
            BackupFolder,
            "*.db")
            .OrderByDescending(
                File.GetCreationTime)
            .ToList();


        foreach (var file in files.Skip(14))
        {
            File.Delete(file);

            _logger.LogInformation(
                $"Deleted old backup: {file}");
        }
    }
}