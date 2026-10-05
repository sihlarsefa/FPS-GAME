using System.Diagnostics;
using System.Text.Json;
using Harekat.Telemetry.Application;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Application.Services;
using Harekat.Telemetry.Domain.Enums;
using Harekat.Telemetry.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "HAREKÂT Telemetri & Hile Tespiti", Version = "v1" });
});
builder.Services.AddTelemetryApplication();
builder.Services.AddTelemetryInfrastructure(builder.Configuration);
builder.Services.Configure<Harekat.Telemetry.Application.Services.ClientErrorRateLimitOptions>(
    builder.Configuration.GetSection("ClientErrors:RateLimit"));
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var (status, title) = ex switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Bulunamadı"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "İşlem çakışması"),
            _ => (StatusCodes.Status500InternalServerError, "Sunucu hatası")
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = ex?.Message,
            Instance = context.Request.Path
        };
        await context.Response.WriteAsJsonAsync(problem);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", (IPerformanceMetrics metrics, IEventStreamPublisher stream) =>
{
    var snap = metrics.Snapshot();
    return Results.Ok(new
    {
        status = "ok",
        service = "Harekat.Telemetry",
        streamBackend = stream.BackendName,
        metrics = snap
    });
}).WithTags("Sistem");

app.MapPost("/events/batch", async (EventBatchRequest request, EventIngestionService ingestion, CancellationToken ct) =>
{
    var result = await ingestion.IngestAsync(request, ct);
    return Results.Ok(result);
}).WithTags("Olaylar").WithName("IngestEventBatch");

app.MapGet("/reports", async (SuspicionAnalysisService analysis, double? minScore, CancellationToken ct) =>
{
    var reports = await analysis.ListReportsAsync(minScore, ct);
    return Results.Ok(reports);
}).WithTags("Hile Tespiti");

app.MapGet("/heatmap/{matchId}", async (string matchId, HeatmapService heatmap, float? cellSize, CancellationToken ct) =>
{
    var data = await heatmap.GetHeatmapAsync(matchId, cellSize ?? 10f, ct);
    return Results.Ok(data);
}).WithTags("Isı Haritası");

app.MapGet("/heatmap/{matchId}/png", async (string matchId, HeatmapService heatmap, bool? deaths, bool? landings, string? season, CancellationToken ct) =>
{
    var png = await heatmap.RenderPngAsync(matchId, deaths ?? true, landings ?? true, season ?? "current", 512, ct);
    return Results.File(png, "image/png", $"heatmap-{matchId}.png");
}).WithTags("Isı Haritası");

app.MapGet("/heatmap/season/{seasonId}/png", async (string seasonId, HeatmapService heatmap, CancellationToken ct) =>
{
    var png = await heatmap.RenderPngAsync(null, true, true, seasonId, 512, ct);
    return Results.File(png, "image/png", $"heatmap-season-{seasonId}.png");
}).WithTags("Isı Haritası");

app.MapGet("/weapons/balance", async (WeaponBalanceService weapons, string? matchId, CancellationToken ct) =>
{
    var report = await weapons.GetReportAsync(matchId, ct);
    return Results.Ok(report);
}).WithTags("Silah Dengesi");

app.MapGet("/players/{playerId}/risk", async (string playerId, RiskScoreQueryService risk, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct) =>
{
    var series = await risk.GetSeriesAsync(playerId, from, to, ct);
    return Results.Ok(series);
}).WithTags("Risk");

app.MapGet("/review-queue", async (RiskScoreQueryService risk, ReviewQueueStatus? status, CancellationToken ct) =>
{
    var items = await risk.ListQueueAsync(status, ct);
    return Results.Ok(items);
}).WithTags("İnceleme");

app.MapPatch("/review-queue/{id:guid}", async (Guid id, UpdateReviewStatusRequest body, RiskScoreQueryService risk, CancellationToken ct) =>
{
    var updated = await risk.UpdateQueueAsync(id, body, ct);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
}).WithTags("İnceleme");

app.MapPost("/replays", async (ReplaySaveRequest request, ReplayService replays, CancellationToken ct) =>
{
    var meta = await replays.SaveAsync(request, ct);
    return Results.Created($"/replays/{meta.MatchId}", meta);
}).WithTags("Tekrar");

app.MapGet("/replays/{matchId}", async (string matchId, ReplayService replays, CancellationToken ct) =>
{
    var replay = await replays.LoadAsync(matchId, ct);
    return replay is null ? Results.NotFound() : Results.Ok(replay);
}).WithTags("Tekrar");

app.MapGet("/rules", (IRuleProvider rules) =>
{
    var snap = rules.GetCurrent();
    return Results.Ok(new { snap.Version, snap.LoadedAt, Rules = snap.Rules });
}).WithTags("Kurallar");

app.MapGet("/metrics/perf", (IPerformanceMetrics metrics) => Results.Ok(metrics.Snapshot()))
    .WithTags("Sistem");

app.MapPost("/client-errors", async (ClientErrorRequest request, ClientErrorService errors, HttpContext http, CancellationToken ct) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString();
    var (accepted, dto, error) = await errors.IngestAsync(request, ip, ct);
    if (!accepted && error == "rate_limited")
    {
        http.Response.Headers.RetryAfter = "60";
        return Results.Json(new { error = "rate_limited", message = "Çok fazla istek." }, statusCode: StatusCodes.Status429TooManyRequests);
    }
    if (!accepted)
        return Results.BadRequest(new { error = error ?? "invalid", message = "Geçersiz hata raporu." });
    return Results.Created($"/client-errors/{dto!.Id}", dto);
}).WithTags("İstemci Hataları").WithName("IngestClientError");

app.MapGet("/client-errors", async (ClientErrorService errors, int? skip, int? take, CancellationToken ct) =>
{
    var list = await errors.ListAsync(skip ?? 0, take ?? 50, ct);
    return Results.Ok(list);
}).WithTags("İstemci Hataları").WithName("ListClientErrors");

app.MapPost("/debug/bench", async (EventIngestionService ingestion, CancellationToken ct) =>
{
    var sw = Stopwatch.StartNew();
    var events = new List<MatchEventDto>();
    for (var i = 0; i < 1000; i++)
    {
        events.Add(new MatchEventDto
        {
            PlayerId = $"p{i % 40}",
            EventType = (MatchEventType)(i % 4),
            X = (i % 100) - 50,
            Y = 1,
            Z = (i % 80) - 40,
            WeaponId = "ar_mpt76",
            Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(i)
        });
    }
    var result = await ingestion.IngestAsync(new EventBatchRequest { MatchId = $"bench-{Guid.NewGuid():N}", Events = events }, ct);
    sw.Stop();
    return Results.Ok(new { ms = sw.ElapsedMilliseconds, result.Accepted, result.Rejected });
}).WithTags("Sistem");

// Kuralları API çıktı klasörüne de kopyala (hot reload için)
var rulesSrc = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "rules", "detection-rules.json"));
var rulesDstDir = Path.Combine(app.Environment.ContentRootPath, "rules");
Directory.CreateDirectory(rulesDstDir);
var rulesDst = Path.Combine(rulesDstDir, "detection-rules.json");
if (File.Exists(rulesSrc) && !File.Exists(rulesDst))
    File.Copy(rulesSrc, rulesDst);

app.Run();

public partial class Program;
