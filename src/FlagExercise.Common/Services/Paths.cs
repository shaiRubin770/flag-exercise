namespace FlagExercise.Common.Services;

/// <summary>Resolves on-disk locations for config and logs per role.</summary>
public static class Paths
{
    public static string RoleRoot(string role)
    {
        // Allow override (useful for tests / non-default install). On Windows this defaults
        // to %ProgramData%\FlagExercise\<role> which the LocalSystem service account can write.
        var overrideRoot = Environment.GetEnvironmentVariable("FLAGEX_DATA_ROOT");
        var baseDir = !string.IsNullOrWhiteSpace(overrideRoot)
            ? overrideRoot!
            : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(baseDir, "FlagExercise", role);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ConfigFile(string role) => Path.Combine(RoleRoot(role), "config.json");
    public static string LogsDir(string role)
    {
        var d = Path.Combine(RoleRoot(role), "logs");
        Directory.CreateDirectory(d);
        return d;
    }
    public static string LogFile(string role) => Path.Combine(LogsDir(role), $"{role.ToLowerInvariant()}-service.log");
}
