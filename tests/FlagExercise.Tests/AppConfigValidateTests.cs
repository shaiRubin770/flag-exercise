using FlagExercise.Common.Models;
using Xunit;

namespace FlagExercise.Tests;

public class AppConfigValidateTests
{  
    private static AppConfig ValidTx() => new()
    {
        SourceFolder      = @"C:\FlagExercise\Source",
        DestinationFolder = @"C:\FlagExercise\Destination",
        PollIntervalMs    = 2000,
        FlagCreateMinSeconds = 5,
        FlagCreateMaxSeconds = 10,
        SmtpEnabled   = false,
        SyslogEnabled = false
    };

    private static AppConfig ValidRx() => new()
    {
        DestinationFolder = @"C:\FlagExercise\Destination",
        PollIntervalMs    = 2000,
        SmtpEnabled   = false,
        SyslogEnabled = false
    };


    [Fact]
    public void Validate_ReturnsError_WhenDestinationFolderIsMissing()
    {
        var cfg = ValidTx();
        cfg.DestinationFolder = "";

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("Destination folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenSourceFolderMissingForTx()
    {
        var cfg = ValidTx();
        cfg.SourceFolder = "   ";

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("Source folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_DoesNotRequireSourceFolder_ForRx()
    {
        var cfg = ValidRx();
        cfg.SourceFolder = "";

        var errors = cfg.Validate(isTx: false);

        Assert.DoesNotContain(errors, e => e.Contains("Source folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenSourceAndDestinationAreTheSame()
    {
        var cfg = ValidTx();
        cfg.DestinationFolder = cfg.SourceFolder;

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("differ", StringComparison.OrdinalIgnoreCase));
    }   

    [Fact]
    public void Validate_ReturnsError_WhenPollIntervalTooLow()
    {
        var cfg = ValidTx();
        cfg.PollIntervalMs = 249;

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("Poll interval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenPollIntervalTooHigh()
    {
        var cfg = ValidTx();
        cfg.PollIntervalMs = 600_001;

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("Poll interval", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(250)]
    [InlineData(2000)]
    [InlineData(600_000)]
    public void Validate_AcceptsBoundaryPollIntervals(int ms)
    {
        var cfg = ValidTx();
        cfg.PollIntervalMs = ms;

        var errors = cfg.Validate(isTx: true);

        Assert.DoesNotContain(errors, e => e.Contains("Poll interval", StringComparison.OrdinalIgnoreCase));
    }  

    [Fact]
    public void Validate_ReturnsError_WhenFlagMinSecondsLessThanOne()
    {
        var cfg = ValidTx();
        cfg.FlagCreateMinSeconds = 0;

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("min seconds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenFlagMaxSecondsLessThanMin()
    {
        var cfg = ValidTx();
        cfg.FlagCreateMinSeconds = 10;
        cfg.FlagCreateMaxSeconds = 5;

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("max seconds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_DoesNotCheckFlagTimer_ForRx()
    {
        var cfg = ValidRx();
        cfg.FlagCreateMinSeconds = 0;
        cfg.FlagCreateMaxSeconds = 0;

        var errors = cfg.Validate(isTx: false);

        Assert.DoesNotContain(errors, e => e.Contains("seconds", StringComparison.OrdinalIgnoreCase));
    } 

    [Fact]
    public void Validate_ReturnsError_WhenSmtpEnabledButHostMissing()
    {
        var cfg = ValidTx();
        cfg.SmtpEnabled = true;
        cfg.SmtpHost    = "";
        cfg.SmtpFrom    = "a@example.com";
        cfg.SmtpTo      = "b@example.com";

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("SMTP host", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_ReturnsError_WhenSmtpPortIsOutOfRange(int port)
    {
        var cfg = ValidTx();
        cfg.SmtpEnabled = true;
        cfg.SmtpHost    = "smtp.example.com";
        cfg.SmtpPort    = port;
        cfg.SmtpFrom    = "a@example.com";
        cfg.SmtpTo      = "b@example.com";

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("SMTP port", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenSmtpFromIsInvalidEmail()
    {
        var cfg = ValidTx();
        cfg.SmtpEnabled = true;
        cfg.SmtpHost    = "smtp.example.com";
        cfg.SmtpFrom    = "not-an-email";
        cfg.SmtpTo      = "b@example.com";

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("From", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenSmtpToIsInvalidEmail()
    {
        var cfg = ValidTx();
        cfg.SmtpEnabled = true;
        cfg.SmtpHost    = "smtp.example.com";
        cfg.SmtpFrom    = "a@example.com";
        cfg.SmtpTo      = "not-an-email";

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("To", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsNoSmtpErrors_WhenSmtpConfigIsValid()
    {
        var cfg = ValidTx();
        cfg.SmtpEnabled = true;
        cfg.SmtpHost    = "smtp.example.com";
        cfg.SmtpPort    = 587;
        cfg.SmtpFrom    = "sender@example.com";
        cfg.SmtpTo      = "recipient@example.com";

        var errors = cfg.Validate(isTx: true);

        Assert.DoesNotContain(errors, e => e.Contains("SMTP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsError_WhenSyslogEnabledButHostMissing()
    {
        var cfg = ValidTx();
        cfg.SyslogEnabled = true;
        cfg.SyslogHost    = "";

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("Syslog host", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_ReturnsError_WhenSyslogPortIsOutOfRange(int port)
    {
        var cfg = ValidTx();
        cfg.SyslogEnabled = true;
        cfg.SyslogHost    = "syslog.example.com";
        cfg.SyslogPort    = port;

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("Syslog port", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenConfigIsValid()
    {
        var errors = ValidTx().Validate(isTx: true);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenRxConfigIsValid()
    {
        var errors = ValidRx().Validate(isTx: false);
        Assert.Empty(errors);
    }
}
