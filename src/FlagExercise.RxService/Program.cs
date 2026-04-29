using FlagExercise.Common.Models;
using FlagExercise.Common.Services;
using FlagExercise.Common.Web;
using FlagExercise.RxService;

const string Role = "Rx";

var log = new FileLogger(Role);
log.Info($"=== {Role} service is starting (PID {Environment.ProcessId}) ===");

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
    builder.Host.UseWindowsService(options => options.ServiceName = "FlagExercise.Rx");
    builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("FLAGEX_RX_URL") ?? "http://localhost:5082");

    builder.Services.AddSingleton(configStore);
    builder.Services.AddSingleton(log);
    builder.Services.AddSingleton(notifier);
    builder.Services.AddSingleton<RxWorker>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RxWorker>());

    var app = builder.Build();
    var worker = app.Services.GetRequiredService<RxWorker>();

    app.MapGet("/", (HttpContext ctx) =>
    {
        ctx.Response.ContentType = "text/html; charset=utf-8";
        return ctx.Response.WriteAsync(EmbeddedIndex.Html(Role));
    });

    app.MapGet("/favicon.ico", () => Results.StatusCode(204));

    app.MapGet("/api/config", () => Results.Json(configStore.Get()));

    app.MapPost("/api/config", async (HttpContext ctx) =>
    {
        var cfg = await ctx.Request.ReadFromJsonAsync<AppConfig>();
        if (cfg == null)
            return Results.BadRequest(new { errors = new[] { "Invalid JSON body." } });

        var errors = cfg.Validate(isTx: false);
        if (errors.Count > 0)
            return Results.BadRequest(new { errors });

        configStore.Save(cfg);
        log.Info("Configuration updated from the UI.");
        return Results.Json(cfg);
    });

    app.MapGet("/api/status", () => Results.Json(worker.Status()));
    app.MapGet("/api/logs",   (int? n) => Results.Json(log.Tail(n ?? 200)));

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

    log.Info("Rx HTTP UI is listening.");
    app.Run();
}
catch (Exception ex)
{
    log.Error("Fatal error in Program.Main", ex);
    throw;
}
