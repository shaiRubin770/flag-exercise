namespace FlagExercise.Common.Services;

public class FileLogger
{
    private const int MaxLinesInMemory = 500;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private readonly string _path;
    private readonly object _lock = new();
    private readonly Queue<string> _recent = new();

    public string MinLevel { get; set; } = "Info";

    public FileLogger(string role)
    {
        _path = Paths.LogFile(role);
    }

    public string Path => _path;

    public void Info(string message)  => Write("INFO",  message, null);
    public void Warn(string message)  => Write("WARN",  message, null);
    public void Debug(string message) => Write("DEBUG", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    public List<string> Tail(int n)
    {
        lock (_lock)
            return _recent.TakeLast(n).ToList();
    }

    private void Write(string level, string message, Exception? ex)
    {
        if (LevelOrder(level) < LevelOrder(MinLevel)) return;

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}";
        if (ex != null)
            line += $" :: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";

        lock (_lock)
        {
            try
            {
                RollIfTooBig();
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch
            {
                
            }

            _recent.Enqueue(line);
            if (_recent.Count > MaxLinesInMemory)
                _recent.Dequeue();
        }
    }

    private void RollIfTooBig()
    {
        if (!File.Exists(_path)) return;
        if (new FileInfo(_path).Length <= MaxFileSizeBytes) return;

        var rolled = _path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
        File.Move(_path, rolled, overwrite: true);
    }

    private static int LevelOrder(string level) => level.ToUpperInvariant() switch
    {
        "DEBUG" => 0,
        "INFO"  => 1,
        "WARN"  => 2,
        "ERROR" => 3,
        _       => 1
    };
}
