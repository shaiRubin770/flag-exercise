using FlagExercise.Common.Models;
using FlagExercise.Common.Services;
using FlagExercise.Common.Web;
using FlagExercise.TxService;

// "Tx" is the role of this service - it appears in folder names, log file names
// and the page title, and decides which fields the configuration validator requires.
const string Role = "Tx";

// Create the logger first, so we can log even errors that happen during startup.
var log = new FileLogger(Role);
log.Info($"=== {Role} service is starting (PID {Environment.ProcessId}) ===");

// Catch crashes that would otherwise kill the process silently.
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
    log.Error("UnhandledException", e.ExceptionObject as Exception);

TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    log.Error("UnobservedTaskException", e.Exception);
    e.SetObserved();
};

try
{
    var configStore = new ConfigStore(Role);
    var notifier    = new Notifier($"FlagExercise-{Role}", log);

    var builder = WebApplication.CreateBuilder(args);

    // Run as a Windows Service when installed; runs as a console app when launched with "dotnet run".
    builder.Host.UseWindowsService(options => options.ServiceName = "FlagExercise.Tx");

    // The UI is served on this URL. Override with the FLAGEX_TX_URL environment variable.
    builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("FLAGEX_TX_URL") ?? "http://localhost:5081");

    // Register the dependencies the worker needs.
    builder.Services.AddSingleton(configStore);
    builder.Services.AddSingleton(log);
    builder.Services.AddSingleton(notifier);
    builder.Services.AddSingleton<TxWorker>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TxWorker>());

    var app = builder.Build();
    var worker = app.Services.GetRequiredService<TxWorker>();

    // ---------------------- HTTP API used by the React UI ----------------------

    // Returns the React single-page UI.
    app.MapGet("/", (HttpContext ctx) =>
    {
        ctx.Response.ContentType = "text/html; charset=utf-8";
        return ctx.Response.WriteAsync(EmbeddedIndex.Html(Role));
    });

    // Silence the browser's automatic /favicon.ico request so it doesn't
    // appear as a 404 in the developer console.
    app.MapGet("/favicon.ico", () => Results.StatusCode(204));

    // Returns the current configuration as JSON.
    app.MapGet("/api/config", () => Results.Json(configStore.Get()));

    // Validates and saves a new configuration.
    app.MapPost("/api/config", async (HttpContext ctx) =>
    {
        var cfg = await ctx.Request.ReadFromJsonAsync<AppConfig>();
        if (cfg == null)
            return Results.BadRequest(new { errors = new[] { "Invalid JSON body." } });

        var errors = cfg.Validate(isTx: true);
        if (errors.Count > 0)
            return Results.BadRequest(new { errors });

        configStore.Save(cfg);
        log.Info("Configuration updated from the UI.");
        return Results.Json(cfg);
    });

    // Counters and current state for the status panel.
    app.MapGet("/api/status", () => Results.Json(worker.Status()));

    // Last N log lines for the live log viewer.
    app.MapGet("/api/logs", (int? n) => Results.Json(log.Tail(n ?? 200)));

    // Start / stop / restart the worker loop.
    app.MapPost("/api/control", async (HttpContext ctx) =>
    {
        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, string>>();
        if (body == null || !body.TryGetValue("action", out var action))
            return Results.BadRequest(new { error = "action is required" });

        try
        {
            worker.Control(action);
            return Results.Json(new { ok = true });
        }
        catch (Exception ex)
        {
            log.Error($"Control '{action}' failed.", ex);
            return Results.BadRequest(new { error = ex.Message });
        }
    });

    log.Info("Tx HTTP UI is listening.");
    app.Run();
}
catch (Exception ex)
{
    log.Error("Fatal error in Program.Main", ex);
    throw;
}
