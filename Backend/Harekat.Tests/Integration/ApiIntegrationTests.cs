using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Harekat.Application.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Harekat.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:Provider", "Memory");
            builder.UseSetting("Jwt:Secret", "HarekatDevSecretKey_ChangeInProduction_Min32Chars!");
            // JsonPath boş bırakma — Save boş path'te no-op olmalı; yine de null kullan
        });
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }

    [Fact]
    public async Task FullHappyPath_RegisterToLeaderboard()
    {
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var reg = await client.PostAsJsonAsync("/auth/register", new RegisterRequest($"p_{suffix}", $"{suffix}@t.com", "password123", "tr"));
        reg.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>(_json);
        auth!.AccessToken.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var me = await client.GetAsync("/players/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var squadRes = await client.PostAsJsonAsync("/squads", new CreateSquadRequest($"Tim_{suffix}", "tr"));
        squadRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var serverRes = await client.PostAsJsonAsync("/servers/register",
            new RegisterServerRequest("127.0.0.1", 7777, "tr", $"key-{suffix}-abcdef"));
        serverRes.EnsureSuccessStatusCode();
        var server = await serverRes.Content.ReadFromJsonAsync<ServerDto>(_json);

        var queueRes = await client.PostAsJsonAsync("/matchmaking/queue", new QueueRequest("tr", 120));
        queueRes.EnsureSuccessStatusCode();

        var lb = await client.GetAsync("/leaderboards?metric=experience&take=10");
        lb.StatusCode.Should().Be(HttpStatusCode.OK);

        // match result requires existing match — form via second queue after forcing
        server.Should().NotBeNull();
    }

    [Fact]
    public async Task Unauthorized_PlayersMe_Returns401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/players/me");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WeakPassword_Returns400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("x", "x@t.com", "short", "tr"));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
