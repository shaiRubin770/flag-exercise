using FlagExercise.Common.Models;
using Xunit;

namespace FlagExercise.Tests;

public class AppConfigValidateTests
{
    [Fact]
    public void Validate_ReturnsError_WhenDestinationFolderIsMissing()
    {
        var cfg = new AppConfig
        {
            SourceFolder = @"C:\FlagExercise\Source",
            DestinationFolder = "" // missing on purpose
        };

        var errors = cfg.Validate(isTx: true);

        Assert.Contains(errors, e => e.Contains("Destination folder", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsNoErrors_WhenConfigIsValid()
    {
        var cfg = new AppConfig
        {
            SourceFolder = @"C:\FlagExercise\Source",
            DestinationFolder = @"C:\FlagExercise\Destination",
            PollIntervalMs = 2000,
            FlagCreateMinSeconds = 5,
            FlagCreateMaxSeconds = 10,
            SmtpEnabled = false,
            SyslogEnabled = false
        };

        var errors = cfg.Validate(isTx: true);

        Assert.Empty(errors);
    }
}
