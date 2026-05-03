using FlagExercise.Common.Models;
using FlagExercise.Common.Services;

namespace FlagExercise.RxService;

public class RxWorker : BackgroundService
{
    private const int RetryAttempts = 10;
    private const int RetryDelayMs = 100;
    private const int RecoveryDelayMs = 2000;

    private readonly ConfigStore _config;
    private readonly FileLogger _log;
    private readonly Notifier _notifier;
    private readonly object _counterLock = new();

    private FileSystemWatcher? _watcher;
    private int _filesDeleted;
    private int _errors;
    private volatile bool _paused;

    public RxWorker(ConfigStore config, FileLogger log, Notifier notifier)
    {
        _config = config;
        _log = log;
        _notifier = notifier;

        _config.Changed += _ => RebuildWatcher();
    }

    public RxStatusDto Status()
    {
        lock (_counterLock)
        {
            return new RxStatusDto(
                Running: !_paused,
                Machine: Environment.MachineName,
                FilesDeleted: _filesDeleted,
                Errors: _errors);
        }
    }

    public void Control(string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "start":
                _paused = false;
                _log.Info("Rx resumed by user.");
                break;
            case "stop":
                _paused = true;
                _log.Info("Rx paused by user.");
                break;
            case "restart":
                _paused = true;
                RebuildWatcher();
                _paused = false;
                _log.Info("Rx restarted by user.");
                break;
            default:
                throw new ArgumentException($"Unknown action: {action}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("Rx worker is starting.");
        EnsureFolder(_config.Get());
        RebuildWatcher();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cfg = _config.Get();

                if (cfg.ServiceEnabled && !_paused)
                {
                    EnsureFolder(cfg);

                    foreach (var file in Directory.EnumerateFiles(cfg.DestinationFolder))
                        DeleteFile(file, cfg);
                }

                await Task.Delay(_config.Get().PollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                IncrementErrors();
                _log.Error($"Rx loop crashed; will retry in {RecoveryDelayMs} ms.", ex);
                _notifier.Notify(_config.Get(), "Rx service error", ex.Message);
                try { await Task.Delay(RecoveryDelayMs, stoppingToken); } catch { }
            }
        }

        _log.Info("Rx worker is stopping.");
    }

    private static void EnsureFolder(AppConfig cfg) =>
        Directory.CreateDirectory(cfg.DestinationFolder);

    private void RebuildWatcher()
    {
        try
        {
            _watcher?.Dispose();

            var cfg = _config.Get();
            EnsureFolder(cfg);

            var watcher = new FileSystemWatcher(cfg.DestinationFolder);
            watcher.IncludeSubdirectories = false;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

            watcher.Created += (sender, e) => DeleteFile(e.FullPath, _config.Get());
            watcher.Renamed += (sender, e) => DeleteFile(e.FullPath, _config.Get());
            watcher.Error   += (sender, e) => _log.Error("FileSystemWatcher error", e.GetException());

            watcher.EnableRaisingEvents = true;
            _watcher = watcher;

            _log.Info($"Watching folder: {cfg.DestinationFolder}");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to set up FileSystemWatcher.", ex);
        }
    }

    private void DeleteFile(string filePath, AppConfig cfg)
    {
        try
        {
            if (_paused || !cfg.ServiceEnabled) return;
            if (!File.Exists(filePath)) return;

            for (int attempt = 0; attempt < RetryAttempts; attempt++)
            {
                try
                {
                    using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(RetryDelayMs);
                }
            }

            File.Delete(filePath);

            lock (_counterLock) _filesDeleted++;

            var message = $"Deleted '{filePath}' on {Environment.MachineName} at {DateTime.Now:O}";
            _log.Info(message);

            _notifier.Notify(cfg, "Rx file deleted", message);
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            IncrementErrors();
            _log.Error($"Failed to delete file '{filePath}'.", ex);
            _notifier.Notify(cfg, "Rx delete failed", $"{filePath}: {ex.Message}");
        }
    }

    private void IncrementErrors()
    {
        lock (_counterLock) _errors++;
    }
}
