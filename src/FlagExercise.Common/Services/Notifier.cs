using System.Net;
using System.Net.Mail;
using FlagExercise.Common.Models;

namespace FlagExercise.Common.Services;

public class Notifier
{
    private readonly string _appName;
    private readonly FileLogger _log;
    private readonly SyslogClient _syslog = new();

    public Notifier(string appName, FileLogger log)
    {
        _appName = appName;
        _log = log;
    }

    public void Notify(AppConfig cfg, string subject, string body)
    {
        if (cfg.SyslogEnabled) Task.Run(() => TrySendSyslog(cfg, subject, body));
        if (cfg.SmtpEnabled)   Task.Run(() => TrySendEmail(cfg, subject, body));
    }

    private void TrySendSyslog(AppConfig cfg, string subject, string body)
    {
        try
        {
            _syslog.Send(cfg.SyslogHost, cfg.SyslogPort, _appName, $"{subject} - {body}");
            _log.Info($"Syslog sent to {cfg.SyslogHost}:{cfg.SyslogPort}.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to send syslog to {cfg.SyslogHost}:{cfg.SyslogPort}.", ex);
        }
    }

    private void TrySendEmail(AppConfig cfg, string subject, string body)
    {
        try
        {
            using var client = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort);
            client.EnableSsl = cfg.SmtpUseSsl;

            if (!string.IsNullOrEmpty(cfg.SmtpUser))
                client.Credentials = new NetworkCredential(cfg.SmtpUser, cfg.SmtpPassword);

            using var message = new MailMessage(cfg.SmtpFrom, cfg.SmtpTo, subject, body);
            client.Send(message);

            _log.Info($"Email sent to {cfg.SmtpTo} via {cfg.SmtpHost}:{cfg.SmtpPort}.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to send email via {cfg.SmtpHost}:{cfg.SmtpPort}.", ex);
        }
    }
}
