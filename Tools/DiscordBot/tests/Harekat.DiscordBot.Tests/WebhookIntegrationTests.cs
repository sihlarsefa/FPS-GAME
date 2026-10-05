using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Harekat.DiscordBot.Tests;

public class WebhookIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public WebhookIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Root_and_health_ok()
    {
        var root = await _client.GetAsync("/");
        root.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await root.Content.ReadAsStringAsync();
        body.Should().Contain("siralama");

        var health = await _client.GetAsync("/webhooks/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Match_result_requires_api_key()
    {
        var res = await _client.PostAsJsonAsync("/webhooks/match-result", new
        {
            matchId = "m1",
            winningSquadName = "Bozkurtlar",
            winningMembers = new[] { "A" },
            winningTeamKills = 10,
            teamCount = 4
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Match_result_announces_with_key()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/webhooks/match-result");
        req.Headers.Add("X-Api-Key", "dev-webhook-key");
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            matchId = "match-99",
            winningSquadName = "Bozkurtlar",
            winningMembers = new[] { "KomutanYilmaz", "KeskinNisanci" },
            winningTeamKills = 28,
            teamCount = 6,
            mapName = "Kuzgun Vadisi",
            xpGains = new[] { new { playerId = "p-yeni", xp = 500 } }
        }), Encoding.UTF8, "application/json");

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await res.Content.ReadAsStringAsync();
        json.Should().Contain("announced");
    }

    [Fact]
    public async Task Match_result_rejects_bad_body()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/webhooks/match-result");
        req.Headers.Add("X-Api-Key", "dev-webhook-key");
        req.Content = JsonContent.Create(new { matchId = "", winningSquadName = "", teamCount = 1 });
        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rank_promotion_webhook()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/webhooks/rank-promotion");
        req.Headers.Add("X-Api-Key", "dev-webhook-key");
        req.Content = JsonContent.Create(new { playerId = "p-yeni", xpGain = 50 });
        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var missing = new HttpRequestMessage(HttpMethod.Post, "/webhooks/rank-promotion");
        missing.Headers.Add("X-Api-Key", "dev-webhook-key");
        missing.Content = JsonContent.Create(new { playerId = "yok", xpGain = 10 });
        (await _client.SendAsync(missing)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Metrics_endpoint()
    {
        var res = await _client.GetAsync("/webhooks/metrics");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
