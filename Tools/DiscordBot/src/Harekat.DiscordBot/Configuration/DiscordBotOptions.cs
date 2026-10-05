namespace Harekat.DiscordBot.Configuration;

public sealed class DiscordBotOptions
{
    public const string SectionName = "DiscordBot";

    /// <summary>DISCORD_BOT_TOKEN ortam değişkeni önceliklidir.</summary>
    public string? Token { get; set; }

    public ulong? GuildId { get; set; }
    public ulong? AnnouncementChannelId { get; set; }
    public ulong? LogChannelId { get; set; }
    public ulong? RankPromoChannelId { get; set; }
    public string WebhookApiKey { get; set; } = "dev-webhook-key";
    public bool RegisterCommandsGlobally { get; set; }
    public bool SeedDemoData { get; set; } = true;
}

public sealed class BackendOptions
{
    public const string SectionName = "Backend";

    public string BaseUrl { get; set; } = "http://localhost:5080";
    public string? ApiKey { get; set; }
    public bool UseInMemoryFallback { get; set; } = true;
}
