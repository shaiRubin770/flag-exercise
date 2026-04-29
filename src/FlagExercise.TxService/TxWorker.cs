using FlagExercise.Common.Models;
using FlagExercise.Common.Services;

namespace FlagExercise.TxService;

/// <summary>
/// What T(x) does:
///   1. Every 5-10 seconds (random, configurable) it CREATES a "flag" file
///      inside the Source folder.
///   2. It WATCHES the Source folder. Whenever any file appears there, it
///      MOVES that file into the Destination folder.
///   3. It writes everything it does (and any errors) to the log file.
/// </summary>
public class TxWorker : BackgroundService
{
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
    private bool _paused;

    public TxWorker(ConfigStore config, FileLogger log, Notifier notifier)
    {
        _config = config;
        _log = log;
        _notifier = notifier;

        // If the user changes the Source folder from the UI, rebuild the watcher.
        _config.Changed += _ => RebuildWatcher();
    }

    /// <summary>Snapshot used by the UI status panel.</summary>
    public object Status()
    {
        lock (_counterLock)
        {
            return new
            {
                running = !_paused,
                machine = Environment.MachineName,
                flagsCreated = _flagsCreated,
                filesMoved = _filesMoved,
                errors = _errors,
                nextFlagAtUtc = _nextFlagTimeUtc.ToUniversalTime(),
                config = _config.Get()
            };
        }
    }

    /// <summary>Called from the UI: start / stop / restart the worker loop.</summary>
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

    // -------- Main loop --------

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

                    // 1. Time to create a new flag?
                    if (DateTime.UtcNow >= _nextFlagTimeUtc)
                    {
                        CreateFlagFile(cfg);
                        ScheduleNextFlag(cfg);
                    }

                    // 2. Belt-and-suspenders: also check the folder on a timer.
                    //    The watcher catches new files instantly; this catches anything
                    //    the watcher might miss (e.g. when the service was restarting).
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
                _log.Error("Tx loop crashed; will retry in 2 seconds.", ex);
                _notifier.Notify(_config.Get(), "Tx service error", ex.Message);
                try { await Task.Delay(2000, stoppingToken); } catch { }
            }
        }

        _log.Info("Tx worker is stopping.");
    }

    // -------- Helpers --------

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
            if (!File.Exists(sourcePath)) return; // already moved by another event

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(cfg.DestinationFolder, fileName);

            // Don't overwrite an existing file in the destination - add a timestamp.
            if (File.Exists(destPath))
            {
                var name = Path.GetFileNameWithoutExtension(fileName);
                var ext  = Path.GetExtension(fileName);
                destPath = Path.Combine(cfg.DestinationFolder, $"{name}_{DateTime.Now:HHmmssfff}{ext}");
            }

            // Wait briefly until the file is no longer locked by the writer.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    using var fs = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }

            File.Move(sourcePath, destPath);

            lock (_counterLock) _filesMoved++;
            _log.Info($"Moved file: {sourcePath} -> {destPath}");
        }
        catch (FileNotFoundException)
        {
            // Another event already moved it - fine, ignore.
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
