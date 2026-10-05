using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Domain.Catalogs;
using Harekat.Telemetry.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Harekat.Telemetry.Tests;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Ok()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("Harekat.Telemetry", json);
    }

    [Fact]
    public async Task Batch_Reports_Heatmap_Weapons_Flow()
    {
        var matchId = $"api-{Guid.NewGuid():N}";
        var events = new List<MatchEventDto>();
        for (var i = 0; i < 22; i++)
            events.Add(new MatchEventDto { PlayerId = "api-suspect", EventType = MatchEventType.Shot, WeaponId = WeaponIds.Pmt76 });
        for (var i = 0; i < 20; i++)
            events.Add(new MatchEventDto
            {
                PlayerId = "api-suspect",
                EventType = MatchEventType.Hit,
                IsHeadshot = true,
                ThroughWall = true,
                WeaponId = WeaponIds.Pmt76,
                X = 10, Y = 1, Z = 10
            });
        events.Add(new MatchEventDto { PlayerId = "victim", EventType = MatchEventType.Death, X = 12, Y = 1, Z = 12, WeaponId = WeaponIds.Pmt76 });
        events.Add(new MatchEventDto { PlayerId = "drop", EventType = MatchEventType.Landing, X = -100, Y = 50, Z = 200 });

        var batch = await _client.PostAsJsonAsync("/events/batch", new EventBatchRequest { MatchId = matchId, Events = events });
        Assert.Equal(HttpStatusCode.OK, batch.StatusCode);
        var body = await batch.Content.ReadFromJsonAsync<EventBatchResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Accepted > 0);

        var reports = await _client.GetFromJsonAsync<List<SuspicionReportDto>>("/reports?minScore=40");
        Assert.NotNull(reports);
        Assert.Contains(reports!, r => r.PlayerId == "api-suspect");

        var hm = await _client.GetFromJsonAsync<HeatmapResponse>($"/heatmap/{matchId}");
        Assert.NotNull(hm);
        Assert.Equal(10f, hm!.CellSizeMeters);

        var png = await _client.GetAsync($"/heatmap/{matchId}/png");
        Assert.Equal(HttpStatusCode.OK, png.StatusCode);
        Assert.Equal("image/png", png.Content.Headers.ContentType?.MediaType);

        var weapons = await _client.GetFromJsonAsync<WeaponBalanceReportDto>($"/weapons/balance?matchId={matchId}");
        Assert.NotNull(weapons);
        Assert.Contains(weapons!.Weapons, w => w.WeaponId == WeaponIds.Pmt76);

        var risk = await _client.GetFromJsonAsync<RiskSeriesDto>("/players/api-suspect/risk");
        Assert.NotNull(risk);
        Assert.NotEmpty(risk!.Points);

        var queue = await _client.GetFromJsonAsync<List<ReviewQueueItemDto>>("/review-queue");
        Assert.NotNull(queue);

        var rules = await _client.GetAsync("/rules");
        Assert.Equal(HttpStatusCode.OK, rules.StatusCode);

        var perf = await _client.GetAsync("/metrics/perf");
        Assert.Equal(HttpStatusCode.OK, perf.StatusCode);
    }

    [Fact]
    public async Task Replay_Endpoint()
    {
        var matchId = $"rep-{Guid.NewGuid():N}";
        var req = new ReplaySaveRequest
        {
            MatchId = matchId,
            Frames =
            [
                new ReplayFrameDto { Sequence = 0, PlayerId = "p", X = 1, Y = 1, Z = 1, WeaponId = WeaponIds.Escort },
                new ReplayFrameDto { Sequence = 1, PlayerId = "p", X = 2, Y = 1, Z = 2, TimeSeconds = 0.1f }
            ]
        };
        var created = await _client.PostAsJsonAsync("/replays", req);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var loaded = await _client.GetAsync($"/replays/{matchId}");
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
    }

    [Fact]
    public async Task BosBatch_400()
    {
        var res = await _client.PostAsJsonAsync("/events/batch", new EventBatchRequest { MatchId = "x", Events = [] });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ClientErrors_Ingest_And_List()
    {
        var req = new ClientErrorRequest
        {
            Trigger = "exception",
            Version = "0.1.0",
            Scene = "KuzgunVadisi",
            Platform = "OSXEditor",
            DeviceModel = "test",
            OperatingSystem = "TestOS",
            Message = "NullReferenceException",
            StackTrace = "at Test.Foo()",
            RecentLogs = ["[log] hello", "[error] boom"],
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("o")
        };

        var created = await _client.PostAsJsonAsync("/client-errors", req);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await _client.GetFromJsonAsync<ClientErrorListResponse>("/client-errors?take=10");
        Assert.NotNull(list);
        Assert.True(list!.Total >= 1);
        Assert.Contains(list.Items, i => i.Message.Contains("NullReference", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ClientErrors_Empty_Is_400()
    {
        var res = await _client.PostAsJsonAsync("/client-errors", new ClientErrorRequest());
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
