using System.Text.Json;
using FlagExercise.Common.Models;

namespace FlagExercise.Common.Services;

/// <summary>
/// Reads and writes the service configuration as a JSON file on disk.
/// Raises <see cref="Changed"/> when the configuration is saved so workers
/// can react (e.g. rebuild their FileSystemWatcher on a new folder).
/// </summary>
public class ConfigStore
{
    private readonly string _file;
    private readonly object _lock = new();
    private AppConfig _current = new();

    /// <summary>Fired after a successful save.</summary>
    public event Action<AppConfig>? Changed;

    public ConfigStore(string role)
    {
        _file = Paths.ConfigFile(role);
        Load();
    }

    /// <summary>Returns the current configuration.</summary>
    public AppConfig Get()
    {
        lock (_lock) return _current;
    }

    /// <summary>Saves a new configuration to disk and notifies listeners.</summary>
    public void Save(AppConfig newConfig)
    {
        lock (_lock)
        {
            _current = newConfig;
            var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_file, json);
        }
        Changed?.Invoke(newConfig);
    }

    private void Load()
    {
        // First run: create a default config file on disk.
        if (!File.Exists(_file))
        {
            Save(_current);
            return;
        }

        try
        {
            var json = File.ReadAllText(_file);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json);
            if (loaded != null) _current = loaded;
        }
        catch
        {
            // If the file is corrupt, fall back to defaults rather than crashing.
            _current = new AppConfig();
        }
    }
}
