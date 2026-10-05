using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Harekat.DiscordBot.Configuration;
using Harekat.DiscordBot.Hosting;
using Harekat.DiscordBot.Services;
using Harekat.DiscordBot.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DiscordBotOptions>(builder.Configuration.GetSection(DiscordBotOptions.SectionName));
builder.Services.Configure<BackendOptions>(builder.Configuration.GetSection(BackendOptions.SectionName));

builder.Services.AddSingleton(sp =>
{
    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds
                         | GatewayIntents.GuildMembers
                         | GatewayIntents.GuildMessages
                         | GatewayIntents.GuildBans,
        AlwaysDownloadUsers = false,
        LogLevel = LogSeverity.Info
    };
    return new DiscordSocketClient(config);
});
builder.Services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));

builder.Services.AddSingleton<InMemoryGameDataStore>();
builder.Services.AddSingleton<PerformanceMetrics>();
builder.Services.AddSingleton<InMemoryAnnouncementSink>();
builder.Services.AddSingleton<IAnnouncementSink>(sp =>
{
    var client = sp.GetRequiredService<DiscordSocketClient>();
    var fallback = sp.GetRequiredService<InMemoryAnnouncementSink>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DiscordBotOptions>>();
    return new DiscordAnnouncementSink(client, fallback, options);
});

builder.Services.AddSingleton<IDiscordRoleSyncGateway>(sp =>
{
    var client = sp.GetRequiredService<DiscordSocketClient>();
    // Token yoksa kayıt tutan gateway; test ve yerel geliştirme için güvenli.
    var token = DiscordBotHostedService.ResolveToken(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DiscordBotOptions>>().Value);
    return string.IsNullOrWhiteSpace(token)
        ? new RecordingRoleSyncGateway()
        : new DiscordRoleSyncGateway(client);
});

builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
builder.Services.AddSingleton<IProfileService, ProfileService>();
builder.Services.AddSingleton<IServerStatusService, ServerStatusService>();
builder.Services.AddSingleton<ISquadBoardService, SquadBoardService>();
builder.Services.AddSingleton<ISquadRecruitmentService, SquadRecruitmentService>();
builder.Services.AddSingleton<IModerationService, ModerationService>();
builder.Services.AddSingleton<ITournamentService, TournamentService>();
builder.Services.AddSingleton<IRankSyncService, RankSyncService>();
builder.Services.AddSingleton<IMatchAnnouncementService, MatchAnnouncementService>();

builder.Services.AddHostedService<DiscordBotHostedService>();
builder.Services.AddHostedService<WeeklySquadKillAnnouncementService>();

var app = builder.Build();

var botOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DiscordBotOptions>>().Value;
if (botOptions.SeedDemoData)
    app.Services.GetRequiredService<InMemoryGameDataStore>().SeedDemoData();

app.MapGet("/", () => Results.Ok(new
{
    service = "HAREKÂT Discord Bot",
    commands = new[]
    {
        "/siralama", "/profil", "/tim", "/sunucu",
        "/sezon", "/haftalik-tim",
        "/tim-ara olustur|katil|liste",
        "/mod uyari|sustur|ban|log",
        "/turnuva olustur|goster|sonuc|liste",
        "/rutbe-senkron", "/hesap-bagla"
    },
    webhooks = new[] { "POST /webhooks/match-result", "POST /webhooks/rank-promotion" }
}));

app.MapHarekatWebhooks();

app.Run();

public partial class Program;
