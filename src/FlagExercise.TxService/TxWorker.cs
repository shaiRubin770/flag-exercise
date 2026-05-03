using FlagExercise.Common.Models;
using FlagExercise.Common.Services;

namespace FlagExercise.TxService;

public class TxWorker : BackgroundService
{
    private const int RetryAttempts = 10;
    private const int RetryDelayMs = 100;
    private const int RecoveryDelayMs = 2000;

    private readonly ConfigStore _config;
    private readonly FileLogger _log;
    private readonly Notifier _notifier;
    private readonly Random _random = new();
    private readonly object _counterLock = new();

    private FileSystemWatcher? _watcher;
    private DateTime _nextFlagTimeUtc = DateTime.MinValue;

    private int _flagsCreated;
    private int _filesMoved;
    private int _errors;
    private volatile bool _paused;

    public TxWorker(ConfigStore config, FileLogger log, Notifier notifier)
    {
        _config = config;
        _log = log;
        _notifier = notifier;

        _config.Changed += _ => RebuildWatcher();
    }

    public TxStatusDto Status()
    {
        lock (_counterLock)
        {
            return new TxStatusDto(
                Running: !_paused,
                Machine: Environment.MachineName,
                FlagsCreated: _flagsCreated,
                FilesMoved: _filesMoved,
                Errors: _errors,
                NextFlagAtUtc: _nextFlagTimeUtc.ToUniversalTime());
        }
    }

    public void Control(string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "start":
                _paused = false;
                _log.Info("Tx resumed by user.");
                break;
            case "stop":
                _paused = true;
                _log.Info("Tx paused by user.");
                break;
            case "restart":
                _paused = true;
                RebuildWatcher();
                _paused = false;
                _log.Info("Tx restarted by user.");
                break;
            default:
                throw new ArgumentException($"Unknown action: {action}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("Tx worker is starting.");
        EnsureFolders(_config.Get());
        RebuildWatcher();
        ScheduleNextFlag(_config.Get());

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cfg = _config.Get();

                if (cfg.ServiceEnabled && !_paused)
                {
                    EnsureFolders(cfg);

                    if (DateTime.UtcNow >= _nextFlagTimeUtc)
                    {
                        CreateFlagFile(cfg);
                        ScheduleNextFlag(cfg);
                    }

                    foreach (var file in Directory.EnumerateFiles(cfg.SourceFolder))
                        MoveFileToDestination(file, cfg);
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
                _log.Error($"Tx loop crashed; will retry in {RecoveryDelayMs} ms.", ex);
                _notifier.Notify(_config.Get(), "Tx service error", ex.Message);
                try { await Task.Delay(RecoveryDelayMs, stoppingToken); } catch { }
            }
        }

        _log.Info("Tx worker is stopping.");
    }

    private static void EnsureFolders(AppConfig cfg)
    {
        Directory.CreateDirectory(cfg.SourceFolder);
        Directory.CreateDirectory(cfg.DestinationFolder);
    }

    private void ScheduleNextFlag(AppConfig cfg)
    {
        int min = Math.Max(1, cfg.FlagCreateMinSeconds);
        int max = Math.Max(min, cfg.FlagCreateMaxSeconds);
        int seconds = _random.Next(min, max + 1);

        _nextFlagTimeUtc = DateTime.UtcNow.AddSeconds(seconds);
        _log.Debug($"Next flag will be created in {seconds} seconds.");
    }

    private void CreateFlagFile(AppConfig cfg)
    {
        try
        {
            var fileName = $"flag_{DateTime.Now:yyyyMMdd_HHmmss_fff}.flag";
            var fullPath = Path.Combine(cfg.SourceFolder, fileName);

            File.WriteAllText(fullPath, $"Flag created at {DateTime.Now:O} on {Environment.MachineName}");

            lock (_counterLock) _flagsCreated++;
            _log.Info($"Created flag file: {fullPath}");
        }
        catch (Exception ex)
        {
            IncrementErrors();
            _log.Error("Failed to create flag file.", ex);
        }
    }

    private void RebuildWatcher()
    {
        try
        {
            _watcher?.Dispose();

            var cfg = _config.Get();
            EnsureFolders(cfg);

            var watcher = new FileSystemWatcher(cfg.SourceFolder);
            watcher.IncludeSubdirectories = false;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

            watcher.Created += (sender, e) => MoveFileToDestination(e.FullPath, _config.Get());
            watcher.Renamed += (sender, e) => MoveFileToDestination(e.FullPath, _config.Get());
            watcher.Error   += (sender, e) => _log.Error("FileSystemWatcher error", e.GetException());

            watcher.EnableRaisingEvents = true;
            _watcher = watcher;

            _log.Info($"Watching folder: {cfg.SourceFolder}");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to set up FileSystemWatcher.", ex);
        }
    }

    private void MoveFileToDestination(string sourcePath, AppConfig cfg)
    {
        try
        {
            if (_paused || !cfg.ServiceEnabled) return;
            if (!File.Exists(sourcePath)) return;

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(cfg.DestinationFolder, fileName);

            if (File.Exists(destPath))
            {
                var name = Path.GetFileNameWithoutExtension(fileName);
                var ext  = Path.GetExtension(fileName);
                destPath = Path.Combine(cfg.DestinationFolder, $"{name}_{DateTime.Now:HHmmssfff}{ext}");
            }

            for (int attempt = 0; attempt < RetryAttempts; attempt++)
            {
                try
                {
                    using var fs = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(RetryDelayMs);
                }
            }

            File.Move(sourcePath, destPath);

            lock (_counterLock) _filesMoved++;
            _log.Info($"Moved file: {sourcePath} -> {destPath}");
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            IncrementErrors();
            _log.Error($"Failed to move file '{sourcePath}'.", ex);
            _notifier.Notify(cfg, "Tx move failed", $"{sourcePath}: {ex.Message}");
        }
    }

    private void IncrementErrors()
    {
        lock (_counterLock) _errors++;
    }
}
