using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using Harekat.Api.Hubs;
using Harekat.Api.Middleware;
using Harekat.Application;
using Harekat.Application.Abstractions;
using Harekat.Application.Dtos;
using Harekat.Application.Services;
using Harekat.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HAREKÂT Backend API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Örnek: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Harekat.Api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true)));

var app = builder.Build();

await app.Services.EnsureStorageAsync(builder.Configuration);

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SimpleRateLimitMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPrometheusScrapingEndpoint("/metrics");

var perf = new PerformanceTracker();

// ——— Health ———
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "Harekat.Api",
    utc = DateTimeOffset.UtcNow,
    perf = perf.Snapshot()
})).WithTags("Health").AllowAnonymous();

// ——— Auth ———
app.MapPost("/auth/register", async (RegisterRequest req, AuthService auth, HttpContext ctx) =>
{
    using var _ = perf.Track("auth.register");
    var ip = ctx.Connection.RemoteIpAddress?.ToString();
    return Results.Ok(await auth.RegisterAsync(req, ip));
}).WithTags("Auth").AllowAnonymous();

app.MapPost("/auth/login", async (LoginRequest req, AuthService auth, HttpContext ctx) =>
{
    using var _ = perf.Track("auth.login");
    var ip = ctx.Connection.RemoteIpAddress?.ToString();
    return Results.Ok(await auth.LoginAsync(req, ip));
}).WithTags("Auth").AllowAnonymous();

app.MapPost("/auth/steam", async (SteamAuthRequest req, AuthService auth, HttpContext ctx) =>
{
    using var _ = perf.Track("auth.steam");
    var ip = ctx.Connection.RemoteIpAddress?.ToString();
    return Results.Ok(await auth.LoginWithSteamAsync(req, ip));
}).WithTags("Auth").AllowAnonymous();

app.MapPost("/auth/refresh", async (RefreshRequest req, AuthService auth) =>
    Results.Ok(await auth.RefreshAsync(req))).WithTags("Auth").AllowAnonymous();

app.MapPost("/auth/verify-email", async (VerifyEmailRequest req, AuthService auth, ClaimsPrincipal user) =>
{
    await auth.VerifyEmailAsync(user.GetPlayerId(), req.Token);
    return Results.Ok(new { verified = true });
}).WithTags("Auth").RequireAuthorization();

app.MapPost("/auth/logout", async (AuthService auth, ClaimsPrincipal user) =>
{
    await auth.LogoutAsync(user.GetPlayerId());
    return Results.Ok(new { loggedOut = true });
}).WithTags("Auth").RequireAuthorization();

// ——— Players ———
app.MapGet("/players/me", async (PlayerService players, ClaimsPrincipal user) =>
    Results.Ok(await players.GetMeAsync(user.GetPlayerId()))).WithTags("Players").RequireAuthorization();

app.MapGet("/players/{username}", async (string username, PlayerService players) =>
    Results.Ok(await players.GetByUsernameAsync(username))).WithTags("Players").AllowAnonymous();

// ——— Squads ———
app.MapPost("/squads", async (CreateSquadRequest req, SquadService squads, ClaimsPrincipal user) =>
    Results.Ok(await squads.CreateAsync(user.GetPlayerId(), req))).WithTags("Squads").RequireAuthorization();

app.MapPost("/squads/join", async (InviteSquadRequest req, SquadService squads, ClaimsPrincipal user) =>
    Results.Ok(await squads.JoinByInviteAsync(user.GetPlayerId(), req))).WithTags("Squads").RequireAuthorization();

app.MapGet("/squads/me", async (SquadService squads, ClaimsPrincipal user) =>
{
    var s = await squads.GetMineAsync(user.GetPlayerId());
    return s is null ? Results.NotFound() : Results.Ok(s);
}).WithTags("Squads").RequireAuthorization();

app.MapGet("/squads/{id:guid}", async (Guid id, SquadService squads) =>
    Results.Ok(await squads.GetAsync(id))).WithTags("Squads").RequireAuthorization();

app.MapPost("/squads/ready", async (bool ready, SquadService squads, ClaimsPrincipal user, IHubContext<LobbyHub> hub) =>
{
    var status = await squads.SetReadyAsync(user.GetPlayerId(), ready);
    await hub.Clients.Group(LobbyHub.GroupName(status.SquadId)).SendAsync("Ready", status);
    return Results.Ok(status);
}).WithTags("Squads").RequireAuthorization();

app.MapPost("/squads/leave", async (SquadService squads, ClaimsPrincipal user) =>
{
    await squads.LeaveAsync(user.GetPlayerId());
    return Results.Ok(new { left = true });
}).WithTags("Squads").RequireAuthorization();

// ——— Matchmaking ———
app.MapPost("/matchmaking/queue", async (QueueRequest? req, MatchmakingService mm, ClaimsPrincipal user) =>
{
    using var _ = perf.Track("matchmaking.queue");
    return Results.Ok(await mm.EnqueueAsync(user.GetPlayerId(), req ?? new QueueRequest()));
}).WithTags("Matchmaking").RequireAuthorization();

app.MapDelete("/matchmaking/queue", async (MatchmakingService mm, ClaimsPrincipal user) =>
{
    await mm.CancelAsync(user.GetPlayerId());
    return Results.Ok(new { cancelled = true });
}).WithTags("Matchmaking").RequireAuthorization();

app.MapGet("/matchmaking/tickets/{id:guid}", async (Guid id, MatchmakingService mm) =>
{
    var t = await mm.GetTicketAsync(id);
    return t is null ? Results.NotFound() : Results.Ok(t);
}).WithTags("Matchmaking").RequireAuthorization();

app.MapGet("/matches/{id:guid}", async (Guid id, MatchmakingService mm) =>
{
    var m = await mm.GetMatchAsync(id);
    return m is null ? Results.NotFound() : Results.Ok(m);
}).WithTags("Matches").RequireAuthorization();

// ——— Servers ———
app.MapPost("/servers/register", async (RegisterServerRequest req, GameServerService servers) =>
    Results.Ok(await servers.RegisterAsync(req))).WithTags("Servers").AllowAnonymous();

app.MapPost("/servers/heartbeat", async (ServerHeartbeatRequest req, GameServerService servers) =>
    Results.Ok(await servers.HeartbeatAsync(req))).WithTags("Servers").AllowAnonymous();

app.MapGet("/servers", async (GameServerService servers) =>
    Results.Ok(await servers.ListAsync())).WithTags("Servers").AllowAnonymous();

app.MapGet("/servers/by-host/{host}", async (string host, GameServerService servers) =>
    Results.Ok(await servers.ListByHostAsync(host))).WithTags("Servers").AllowAnonymous();

app.MapPost("/servers/release", async (ReleaseServerRequest req, GameServerService servers) =>
{
    await servers.ReleaseAsync(req.ServerId, req.ServerKey);
    return Results.Ok(new { released = true });
}).WithTags("Servers").AllowAnonymous();

// ServerManager: bekleyen maç tahsisi
app.MapGet("/matches/pending-allocation", async (string? region, MatchAllocationService alloc) =>
    Results.Ok(await alloc.ListPendingAsync(region))).WithTags("Servers").AllowAnonymous();

app.MapPost("/matches/{id:guid}/claim", async (Guid id, ClaimMatchRequest req, MatchAllocationService alloc) =>
    Results.Ok(await alloc.ClaimAsync(id, req))).WithTags("Servers").AllowAnonymous();

app.MapGet("/matchmaking/queue-depth", async (MatchAllocationService alloc) =>
    Results.Ok(new { depth = await alloc.QueueDepthAsync() })).WithTags("Matchmaking").AllowAnonymous();

// ——— Match result (server key) ———
app.MapPost("/matches/{id:guid}/result", async (
    Guid id,
    MatchResultRequest req,
    HttpRequest http,
    MatchResultService results,
    GameServerService servers) =>
{
    using var _ = perf.Track("matches.result");
    var serverIdHeader = http.Headers["X-Server-Id"].FirstOrDefault();
    var serverKey = http.Headers["X-Server-Key"].FirstOrDefault();
    if (string.IsNullOrEmpty(serverIdHeader) || string.IsNullOrEmpty(serverKey) ||
        !Guid.TryParse(serverIdHeader, out var serverId))
        return Results.Unauthorized();

    var server = await servers.AuthenticateServerAsync(serverId, serverKey);
    if (server is null)
        return Results.Unauthorized();

    return Results.Ok(await results.SubmitResultAsync(id, req, serverId));
}).WithTags("Matches").AllowAnonymous();

// ——— Leaderboards ———
app.MapGet("/leaderboards", async (string? metric, int? take, LeaderboardService lb) =>
    Results.Ok(await lb.GetAsync(metric ?? "experience", take ?? 50))).WithTags("Leaderboards").AllowAnonymous();

app.MapGet("/leaderboards/season", async (int? season, int? take, LeaderboardService lb) =>
    Results.Ok(await lb.GetSeasonAsync(season, take ?? 50))).WithTags("Leaderboards").AllowAnonymous();

// ——— Friends ———
app.MapPost("/friends/request", async (FriendRequestDto req, FriendshipService friends, ClaimsPrincipal user) =>
    Results.Ok(await friends.SendRequestAsync(user.GetPlayerId(), req.TargetPlayerId)))
    .WithTags("Friends").RequireAuthorization();

app.MapPost("/friends/{id:guid}/accept", async (Guid id, FriendshipService friends, ClaimsPrincipal user) =>
    Results.Ok(await friends.AcceptAsync(user.GetPlayerId(), id)))
    .WithTags("Friends").RequireAuthorization();

app.MapGet("/friends", async (FriendshipService friends, ClaimsPrincipal user) =>
    Results.Ok(await friends.ListAsync(user.GetPlayerId())))
    .WithTags("Friends").RequireAuthorization();

// ——— Seasons ———
app.MapGet("/seasons/active", async (SeasonService seasons) =>
{
    var s = await seasons.GetActiveAsync();
    return s is null ? Results.NotFound() : Results.Ok(s);
}).WithTags("Seasons").AllowAnonymous();

app.MapGet("/seasons/{number:int}/archive", async (int number, SeasonService seasons) =>
    Results.Ok(await seasons.GetArchiveAsync(number))).WithTags("Seasons").AllowAnonymous();

// ——— Achievements & Cosmetics ———
app.MapGet("/achievements/me", async (IPlayerRepository players, AchievementService ach, ClaimsPrincipal user) =>
{
    var me = await players.GetByIdAsync(user.GetPlayerId())
             ?? throw new Harekat.Application.Common.AppException("Oyuncu bulunamadı.", 404);
    return Results.Ok(ach.ListForPlayer(me));
}).WithTags("Achievements").RequireAuthorization();

app.MapGet("/cosmetics/me", async (CosmeticService cosmetics, ClaimsPrincipal user) =>
    Results.Ok(await cosmetics.ListAsync(user.GetPlayerId()))).WithTags("Cosmetics").RequireAuthorization();

app.MapPost("/cosmetics/equip", async (EquipCosmeticRequest req, CosmeticService cosmetics, ClaimsPrincipal user) =>
    Results.Ok(await cosmetics.EquipAsync(user.GetPlayerId(), req.CosmeticId))).WithTags("Cosmetics").RequireAuthorization();

// ——— Moderation ———
app.MapPost("/moderation/report", async (ReportPlayerRequest req, ModerationService mod, ClaimsPrincipal user) =>
{
    await mod.ReportAsync(user.GetPlayerId(), req);
    return Results.Ok(new { reported = true });
}).WithTags("Moderation").RequireAuthorization();

app.MapPost("/moderation/ban", async (BanPlayerRequest req, ModerationService mod, ClaimsPrincipal user) =>
{
    await mod.BanAsync(user.GetPlayerId(), req);
    return Results.Ok(new { banned = true });
}).WithTags("Moderation").RequireAuthorization("Moderator");

app.MapPost("/moderation/mute", async (MutePlayerRequest req, ModerationService mod, ClaimsPrincipal user) =>
{
    await mod.MuteAsync(user.GetPlayerId(), req);
    return Results.Ok(new { muted = true });
}).WithTags("Moderation").RequireAuthorization("Moderator");

app.MapGet("/moderation/reports", async (ModerationService mod) =>
    Results.Ok(await mod.GetOpenReportsAsync())).WithTags("Moderation").RequireAuthorization("Moderator");

// ——— Client / Launcher (F3-8) ———
app.MapGet("/news", async (string? lang, int? take, ClientContentService content) =>
    Results.Ok(await content.ListNewsAsync(lang, take ?? 20))).WithTags("Client").AllowAnonymous();

app.MapGet("/client/version", async (string? channel, ClientContentService content) =>
    Results.Ok(await content.GetVersionAsync(channel))).WithTags("Client").AllowAnonymous();

app.MapGet("/admin/news", async (int? take, ClientContentService content) =>
    Results.Ok(await content.ListAllNewsAdminAsync(take ?? 50))).WithTags("Admin").RequireAuthorization("Admin");

app.MapPost("/admin/news", async (UpsertNewsRequest req, ClientContentService content) =>
    Results.Ok(await content.CreateNewsAsync(req))).WithTags("Admin").RequireAuthorization("Admin");

app.MapPut("/admin/news/{id:guid}", async (Guid id, UpsertNewsRequest req, ClientContentService content) =>
    Results.Ok(await content.UpdateNewsAsync(id, req))).WithTags("Admin").RequireAuthorization("Admin");

app.MapDelete("/admin/news/{id:guid}", async (Guid id, ClientContentService content) =>
{
    await content.DeleteNewsAsync(id);
    return Results.Ok(new { deleted = true });
}).WithTags("Admin").RequireAuthorization("Admin");

app.MapPut("/admin/client/version", async (UpsertClientVersionRequest req, ClientContentService content) =>
    Results.Ok(await content.UpsertVersionAsync(req))).WithTags("Admin").RequireAuthorization("Admin");

app.MapHub<LobbyHub>("/hubs/lobby");

app.Run();

public partial class Program { }

/// <summary>Basit uç nokta gecikme ölçümü.</summary>
public sealed class PerformanceTracker
{
    private readonly ConcurrentDictionary<string, LatencyStats> _stats = new();

    public IDisposable Track(string name) => new Scope(this, name);

    public object Snapshot() => _stats.ToDictionary(
        kv => kv.Key,
        kv => new { kv.Value.Count, kv.Value.AvgMs, kv.Value.P95Ms, kv.Value.MaxMs });

    private void Record(string name, double ms)
    {
        _stats.AddOrUpdate(name,
            _ => new LatencyStats().Add(ms),
            (_, s) => s.Add(ms));
    }

    private sealed class Scope : IDisposable
    {
        private readonly PerformanceTracker _t;
        private readonly string _name;
        private readonly long _start = Stopwatch.GetTimestamp();
        public Scope(PerformanceTracker t, string name) { _t = t; _name = name; }
        public void Dispose()
        {
            var ms = Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
            _t.Record(_name, ms);
        }
    }

    private sealed class LatencyStats
    {
        private readonly List<double> _samples = [];
        public int Count => _samples.Count;
        public double AvgMs => _samples.Count == 0 ? 0 : _samples.Average();
        public double MaxMs => _samples.Count == 0 ? 0 : _samples.Max();
        public double P95Ms
        {
            get
            {
                if (_samples.Count == 0) return 0;
                var sorted = _samples.OrderBy(x => x).ToList();
                var idx = (int)Math.Clamp(Math.Ceiling(sorted.Count * 0.95) - 1, 0, sorted.Count - 1);
                return sorted[idx];
            }
        }
        public LatencyStats Add(double ms)
        {
            lock (_samples)
            {
                _samples.Add(ms);
                if (_samples.Count > 1000)
                    _samples.RemoveRange(0, _samples.Count - 1000);
            }
            return this;
        }
    }
}
