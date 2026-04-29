namespace FlagExercise.Common.Models;

/// <summary>
/// Single configuration model used by both Tx and Rx services.
/// Persisted to JSON in %ProgramData%\FlagExercise\{Tx|Rx}\config.json.
/// </summary>
public class AppConfig
{
    // Folders. On Windows the defaults point to C:\FlagExercise\... .
    // On Linux/macOS (useful for development) we fall back to /tmp/flagexercise/... .
    public string SourceFolder { get; set; } =
        OperatingSystem.IsWindows() ? @"C:\FlagExercise\Source" : "/tmp/flagexercise/source";
    public string DestinationFolder { get; set; } =
        OperatingSystem.IsWindows() ? @"C:\FlagExercise\Destination" : "/tmp/flagexercise/destination";

    // Polling timer (ms) — used in addition to FileSystemWatcher to "check the folder"
    public int PollIntervalMs { get; set; } = 2000;

    // Tx-only: random flag-creation interval (seconds)
    public int FlagCreateMinSeconds { get; set; } = 5;
    public int FlagCreateMaxSeconds { get; set; } = 10;

    // SMTP - defaults set up for sending from a Gmail account.
    // To actually send email: create a Google App Password (Google Account -> Security ->
    // 2-Step Verification -> App passwords) and put the 16-char password into SmtpPassword
    // via the UI, then tick "SMTP enabled" and Save.
    public bool SmtpEnabled { get; set; } = false;
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string SmtpUser { get; set; } = "rubinshai@gmail.com";
    public string SmtpPassword { get; set; } = "";
    public string SmtpFrom { get; set; } = "rubinshai@gmail.com";
    public string SmtpTo { get; set; } = "rubinshai@gmail.com";

    // Syslog (RFC 3164, UDP)
    public bool SyslogEnabled { get; set; } = false;
    public string SyslogHost { get; set; } = "127.0.0.1";
    public int SyslogPort { get; set; } = 514;

    // Misc
    public bool ServiceEnabled { get; set; } = true;
    public string LogLevel { get; set; } = "Info"; // Debug|Info|Warn|Error

    /// <summary>Validate config and return human readable errors (empty list = OK).</summary>
    public List<string> Validate(bool isTx)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DestinationFolder))
            errors.Add("Destination folder is required.");
        if (isTx && string.IsNullOrWhiteSpace(SourceFolder))
            errors.Add("Source folder is required.");
        if (isTx && string.Equals(SourceFolder?.TrimEnd('\\','/'), DestinationFolder?.TrimEnd('\\','/'),
                StringComparison.OrdinalIgnoreCase))
            errors.Add("Source and Destination must differ.");

        if (PollIntervalMs < 250 || PollIntervalMs > 600_000)
            errors.Add("Poll interval must be between 250 ms and 600000 ms.");

        if (isTx)
        {
            if (FlagCreateMinSeconds < 1) errors.Add("Flag min seconds must be >= 1.");
            if (FlagCreateMaxSeconds < FlagCreateMinSeconds)
                errors.Add("Flag max seconds must be >= min seconds.");
        }

        if (SmtpEnabled)
        {
            if (string.IsNullOrWhiteSpace(SmtpHost)) errors.Add("SMTP host is required when SMTP is enabled.");
            if (SmtpPort <= 0 || SmtpPort > 65535) errors.Add("SMTP port must be between 1 and 65535.");
            if (!IsValidEmail(SmtpFrom)) errors.Add("SMTP From is not a valid email address.");
            if (!IsValidEmail(SmtpTo))   errors.Add("SMTP To is not a valid email address.");
        }

        if (SyslogEnabled)
        {
            if (string.IsNullOrWhiteSpace(SyslogHost)) errors.Add("Syslog host is required when Syslog is enabled.");
            if (SyslogPort <= 0 || SyslogPort > 65535) errors.Add("Syslog port must be between 1 and 65535.");
        }

        return errors;
    }

    private static bool IsValidEmail(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        try { var a = new System.Net.Mail.MailAddress(s); return a.Address == s; }
        catch { return false; }
    }
}
