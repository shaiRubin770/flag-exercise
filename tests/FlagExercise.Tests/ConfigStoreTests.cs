using System.Text.Json;
using FlagExercise.Common.Models;
using FlagExercise.Common.Services;
using Xunit;

namespace FlagExercise.Tests;

/// <summary>
/// Tests for ConfigStore: save/load round-trip, Changed event, corrupt file recovery,
/// and default config file creation.
///
/// Each test gets its own temp directory via FLAGEX_DATA_ROOT so tests never
/// touch %ProgramData% and do not interfere with each other.
/// </summary>
public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _prevDataRoot;

    public ConfigStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "FlagExTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempRoot);
        _prevDataRoot = Environment.GetEnvironmentVariable("FLAGEX_DATA_ROOT");
        Environment.SetEnvironmentVariable("FLAGEX_DATA_ROOT", _tempRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("FLAGEX_DATA_ROOT", _prevDataRoot);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // ── round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void SaveAndGet_RoundTrips_AllFields()
    {
        var store = new ConfigStore("Tx");
        var cfg = new AppConfig
        {
            DestinationFolder    = "/dest",
            SourceFolder         = "/src",
            PollIntervalMs       = 3000,
            FlagCreateMinSeconds = 7,
            FlagCreateMaxSeconds = 14,
            SmtpEnabled          = true,
            SmtpHost             = "smtp.test.com",
            SmtpPort             = 465,
            SmtpFrom             = "from@test.com",
            SmtpTo               = "to@test.com",
            SyslogEnabled        = true,
            SyslogHost           = "syslog.test.com",
            SyslogPort           = 514
        };

        store.Save(cfg);
        var loaded = store.Get();

        Assert.Equal("/dest",          loaded.DestinationFolder);
        Assert.Equal("/src",           loaded.SourceFolder);
        Assert.Equal(3000,             loaded.PollIntervalMs);
        Assert.Equal(7,                loaded.FlagCreateMinSeconds);
        Assert.Equal(14,               loaded.FlagCreateMaxSeconds);
        Assert.True(loaded.SmtpEnabled);
        Assert.Equal("smtp.test.com",  loaded.SmtpHost);
        Assert.Equal(465,              loaded.SmtpPort);
        Assert.Equal("from@test.com",  loaded.SmtpFrom);
        Assert.Equal("to@test.com",    loaded.SmtpTo);
        Assert.True(loaded.SyslogEnabled);
        Assert.Equal("syslog.test.com",loaded.SyslogHost);
        Assert.Equal(514,              loaded.SyslogPort);
    }

    [Fact]
    public void SecondInstance_LoadsWhatFirstSaved()
    {
        var store1 = new ConfigStore("Tx");
        store1.Save(new AppConfig { PollIntervalMs = 9999 });

        var store2 = new ConfigStore("Tx");
        Assert.Equal(9999, store2.Get().PollIntervalMs);
    }

    // ── Changed event ─────────────────────────────────────────────────────────

    [Fact]
    public void Save_FiresChangedEvent_WithNewConfig()
    {
        var store = new ConfigStore("Tx");
        AppConfig? received = null;
        store.Changed += c => received = c;

        store.Save(new AppConfig { PollIntervalMs = 1234 });

        Assert.NotNull(received);
        Assert.Equal(1234, received!.PollIntervalMs);
    }

    [Fact]
    public void Save_DoesNotFireChangedEvent_BeforeSaveIsCalled()
    {
        AppConfig? received = null;
        var store = new ConfigStore("Tx");
        store.Changed += c => received = c;

        Assert.Null(received);
    }

    // ── corrupt / missing file ────────────────────────────────────────────────

    [Fact]
    public void NewStore_WhenNoFileExists_CreatesConfigFileOnDisk()
    {
        var store = new ConfigStore("Fresh");

        var expected = Path.Combine(_tempRoot, "FlagExercise", "Fresh", "config.json");
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public void NewStore_WhenNoFileExists_ReturnsDefaultConfig()
    {
        var store = new ConfigStore("Fresh");
        var cfg = store.Get();

        Assert.NotNull(cfg);
        Assert.Equal(2000, cfg.PollIntervalMs);
    }

    [Fact]
    public void NewStore_CorruptFile_FallsBackToDefaults()
    {
        var configPath = Path.Combine(_tempRoot, "FlagExercise", "Corrupt", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "{ this is not valid JSON !!!}}}");

        var store = new ConfigStore("Corrupt");
        var cfg = store.Get();

        Assert.NotNull(cfg);
        Assert.Equal(2000, cfg.PollIntervalMs);
    }

    [Fact]
    public void NewStore_EmptyFile_FallsBackToDefaults()
    {
        var configPath = Path.Combine(_tempRoot, "FlagExercise", "Empty", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "");

        var store = new ConfigStore("Empty");
        var cfg = store.Get();

        Assert.NotNull(cfg);
        Assert.Equal(2000, cfg.PollIntervalMs);
    }
}
