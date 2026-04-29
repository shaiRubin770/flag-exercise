using System.Text.Json;
using FlagExercise.Common.Models;

namespace FlagExercise.Common.Services;

public class ConfigStore
{
    private readonly string _file;
    private readonly object _lock = new();
    private AppConfig _current = new();

    public event Action<AppConfig>? Changed;

    public ConfigStore(string role)
    {
        _file = Paths.ConfigFile(role);
        Load();
    }

    public AppConfig Get()
    {
        lock (_lock) return _current;
    }

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
            // corrupt file - fall back to defaults
            _current = new AppConfig();
        }
    }
}
