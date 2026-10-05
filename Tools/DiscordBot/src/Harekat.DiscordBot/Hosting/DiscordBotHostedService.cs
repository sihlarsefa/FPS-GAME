using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Harekat.DiscordBot.Configuration;
using Microsoft.Extensions.Options;

namespace Harekat.DiscordBot.Hosting;

public sealed class DiscordBotHostedService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly DiscordBotOptions _options;
    private readonly ILogger<DiscordBotHostedService> _logger;
    private readonly string? _token;

    public DiscordBotHostedService(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        IOptions<DiscordBotOptions> options,
        IConfiguration configuration,
        ILogger<DiscordBotHostedService> logger)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _options = options.Value;
        _logger = logger;
        _token = ResolveToken(configuration, _options);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            _logger.LogWarning(
                "DISCORD_BOT_TOKEN tanımlı değil. Bot Discord'a bağlanmayacak; webhook ve komut servisleri çalışır.");
            return;
        }

        _client.Log += msg =>
        {
            var level = msg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                _ => LogLevel.Debug
            };
            _logger.Log(level, msg.Exception, "[Discord] {Message}", msg.Message);
            return Task.CompletedTask;
        };

        _client.Ready += OnReadyAsync;
        _client.InteractionCreated += OnInteractionCreatedAsync;

        await _interactions.AddModulesAsync(typeof(DiscordBotHostedService).Assembly, _services)
            .ConfigureAwait(false);

        await _client.LoginAsync(TokenType.Bot, _token).ConfigureAwait(false);
        await _client.StartAsync().ConfigureAwait(false);
        _logger.LogInformation("Discord bot başlatıldı.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client.LoginState == LoginState.LoggedIn)
        {
            await _client.StopAsync().ConfigureAwait(false);
            await _client.LogoutAsync().ConfigureAwait(false);
        }
    }

    private async Task OnReadyAsync()
    {
        try
        {
            if (_options.GuildId is > 0)
            {
                await _interactions.RegisterCommandsToGuildAsync(_options.GuildId.Value).ConfigureAwait(false);
                _logger.LogInformation("Slash komutları guild {GuildId} için kaydedildi.", _options.GuildId);
            }
            else if (_options.RegisterCommandsGlobally)
            {
                await _interactions.RegisterCommandsGloballyAsync().ConfigureAwait(false);
                _logger.LogInformation("Slash komutları global olarak kaydedildi.");
            }
            else
            {
                _logger.LogWarning("GuildId yok ve global kayıt kapalı; slash komutları kaydedilmedi.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slash komut kaydı başarısız.");
        }
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interactions.ExecuteCommandAsync(ctx, _services).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Etkileşim işlenemedi.");
            if (interaction.Type == InteractionType.ApplicationCommand && !interaction.HasResponded)
                await interaction.RespondAsync("Komut işlenirken hata oluştu.", ephemeral: true).ConfigureAwait(false);
        }
    }

    internal static string? ResolveToken(IConfiguration configuration, DiscordBotOptions options)
    {
        var fromEnv = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var fromConfigEnv = configuration["DISCORD_BOT_TOKEN"];
        if (!string.IsNullOrWhiteSpace(fromConfigEnv))
            return fromConfigEnv.Trim();

        return string.IsNullOrWhiteSpace(options.Token) ? null : options.Token.Trim();
    }
}
