namespace FlagExercise.Common.Services;

/// <summary>
/// Simple thread-safe logger.
/// Writes every line to a flat file AND keeps the last N lines in memory
/// so the UI can show a "live tail" without reading the file from disk.
/// </summary>
public class FileLogger
{
    private const int MaxLinesInMemory = 500;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB then roll

    private readonly string _path;
    private readonly object _lock = new();
    private readonly List<string> _recent = new();

    public FileLogger(string role)
    {
        _path = Paths.LogFile(role);
    }

    public string Path => _path;

    public void Info(string message)  => Write("INFO",  message, null);
    public void Warn(string message)  => Write("WARN",  message, null);
    public void Debug(string message) => Write("DEBUG", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    /// <summary>Returns the last N log lines (oldest first).</summary>
    public List<string> Tail(int n)
    {
        lock (_lock)
        {
            if (n >= _recent.Count) return new List<string>(_recent);
            return _recent.GetRange(_recent.Count - n, n);
        }
    }

    private void Write(string level, string message, Exception? ex)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}";
        if (ex != null)
            line += $" :: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";

        lock (_lock)
        {
            // 1. Write to the flat file.
            try
            {
                RollIfTooBig();
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch
            {
                // Never throw from the logger - it would just crash the service.
            }

            // 2. Keep an in-memory copy for the UI tail.
            _recent.Add(line);
            if (_recent.Count > MaxLinesInMemory)
                _recent.RemoveAt(0);
        }
    }

    private void RollIfTooBig()
    {
        if (!File.Exists(_path)) return;
        if (new FileInfo(_path).Length <= MaxFileSizeBytes) return;

        var rolled = _path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
        File.Move(_path, rolled, overwrite: true);
    }
}
