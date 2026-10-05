using Harekat.Application.Abstractions;
using Harekat.Application.Common;
using Harekat.Application.Dtos;
using Harekat.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Harekat.Application.Services;

public sealed class MatchmakingService
{
    public const int DefaultTeamCount = 10;

    private readonly IMatchTicketRepository _tickets;
    private readonly ISquadRepository _squads;
    private readonly IPlayerRepository _players;
    private readonly IMatchRepository _matches;
    private readonly IGameServerRepository _servers;
    private readonly ISeasonRepository _seasons;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<MatchmakingService> _logger;

    public MatchmakingService(
        IMatchTicketRepository tickets,
        ISquadRepository squads,
        IPlayerRepository players,
        IMatchRepository matches,
        IGameServerRepository servers,
        ISeasonRepository seasons,
        IUnitOfWork uow,
        ILogger<MatchmakingService> logger)
    {
        _tickets = tickets;
        _squads = squads;
        _players = players;
        _matches = matches;
        _servers = servers;
        _seasons = seasons;
        _uow = uow;
        _logger = logger;
    }

    public async Task<QueueResponse> EnqueueAsync(Guid playerId, QueueRequest request, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        if (player.IsCurrentlyBanned)
            throw new AppException("Yasaklı hesap eşleşemez.", 403, ErrorCodes.Banned);

        var squad = await _squads.GetByMemberIdAsync(playerId, ct);
        if (squad is null)
        {
            // Solo oyuncuyu otomatik tim yap
            squad = new Squad
            {
                Name = $"{player.Username}_Tim",
                LeaderId = playerId,
                MemberIds = [playerId],
                ReadyMemberIds = [playerId],
                Region = request.Region ?? player.Region
            };
            player.SquadId = squad.Id;
            await _squads.AddAsync(squad, ct);
            await _players.UpdateAsync(player, ct);
        }

        if (squad.LeaderId != playerId)
            throw new AppException("Yalnızca tim komutanı kuyruğa girebilir.", 403, ErrorCodes.Forbidden);

        if (!squad.AllReady)
            throw new AppException("Tüm üyeler hazır olmalı.", 400, ErrorCodes.Validation);

        if (squad.IsInQueue || squad.IsInMatch)
            throw new AppException("Tim zaten kuyrukta veya maçta.", 409, ErrorCodes.Conflict);

        var existing = await _tickets.GetActiveBySquadIdAsync(squad.Id, ct);
        if (existing is not null)
            return new QueueResponse(existing.Id, existing.Status.ToString(), existing.Region, existing.EnqueuedAt);

        var members = await _players.GetByIdsAsync(squad.MemberIds, ct);
        var avgElo = members.Count == 0 ? 1000 : (int)members.Average(m => m.EloRating);

        var ticket = new MatchTicket
        {
            SquadId = squad.Id,
            Region = (request.Region ?? squad.Region).ToLowerInvariant(),
            MaxPingMs = request.MaxPingMs ?? 120,
            AverageElo = avgElo
        };

        squad.IsInQueue = true;
        await _tickets.AddAsync(ticket, ct);
        await _squads.UpdateAsync(squad, ct);
        await _uow.SaveChangesAsync(ct);

        // Anında eşleştirme dene
        await TryFormMatchesAsync(ticket.Region, ct);

        var refreshed = await _tickets.GetByIdAsync(ticket.Id, ct) ?? ticket;
        return new QueueResponse(refreshed.Id, refreshed.Status.ToString(), refreshed.Region, refreshed.EnqueuedAt);
    }

    public async Task CancelAsync(Guid playerId, CancellationToken ct = default)
    {
        var squad = await _squads.GetByMemberIdAsync(playerId, ct)
                    ?? throw new AppException("Tim bulunamadı.", 404, ErrorCodes.NotFound);
        if (squad.LeaderId != playerId)
            throw new AppException("Yalnızca komutan iptal edebilir.", 403, ErrorCodes.Forbidden);

        var ticket = await _tickets.GetActiveBySquadIdAsync(squad.Id, ct);
        if (ticket is null) return;

        ticket.Status = MatchTicketStatus.Cancelled;
        squad.IsInQueue = false;
        await _tickets.UpdateAsync(ticket, ct);
        await _squads.UpdateAsync(squad, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<Match?> TryFormMatchesAsync(string region, CancellationToken ct = default)
    {
        var queued = (await _tickets.GetQueuedByRegionAsync(region, ct))
            .OrderBy(t => t.EnqueuedAt)
            .ToList();

        if (queued.Count == 0)
            return null;

        // Elo toleransına göre grupla
        var selected = new List<MatchTicket>();
        var seed = queued[0];
        selected.Add(seed);

        foreach (var candidate in queued.Skip(1))
        {
            if (selected.Count >= DefaultTeamCount)
                break;

            var eloDiff = Math.Abs(candidate.AverageElo - seed.AverageElo);
            var tol = Math.Max(seed.EloTolerance, candidate.EloTolerance);
            if (eloDiff <= tol)
                selected.Add(candidate);
        }

        // Yeterli tim yoksa bot doldurarak maç oluştur (en az 1 gerçek tim, bekleme > 30sn veya 2+ tim)
        var oldestWait = DateTimeOffset.UtcNow - seed.EnqueuedAt;
        if (selected.Count < 2 && oldestWait.TotalSeconds < 30)
            return null;

        while (selected.Count < DefaultTeamCount)
        {
            // Bot tim placeholder — gerçek ticket değil, maç slotunda BotCount ile gösterilecek
            break;
        }

        var season = await _seasons.GetActiveAsync(ct);
        var match = new Match
        {
            Region = region,
            TargetTeamCount = DefaultTeamCount,
            SeasonNumber = season?.Number ?? 1,
            Status = MatchStatus.Allocating
        };

        foreach (var ticket in selected)
        {
            var squad = await _squads.GetByIdAsync(ticket.SquadId, ct);
            if (squad is null) continue;

            var bots = Math.Max(0, Squad.MaxMembers - squad.MemberIds.Count);
            match.Teams.Add(new MatchTeamSlot
            {
                SquadId = squad.Id,
                SquadName = squad.Name,
                PlayerIds = [.. squad.MemberIds],
                BotCount = bots
            });

            ticket.Status = MatchTicketStatus.Matched;
            ticket.MatchId = match.Id;
            squad.IsInQueue = false;
            squad.IsInMatch = true;
            await _tickets.UpdateAsync(ticket, ct);
            await _squads.UpdateAsync(squad, ct);

            if (squad.MemberIds.Count == Squad.MaxMembers)
            {
                foreach (var pid in squad.MemberIds)
                {
                    var p = await _players.GetByIdAsync(pid, ct);
                    if (p is null) continue;
                    p.AchievementProgress.TryGetValue("full_squad_matches", out var c);
                    p.AchievementProgress["full_squad_matches"] = c + 1;
                    await _players.UpdateAsync(p, ct);
                }
            }
        }

        // Eksik timleri bot tim olarak doldur
        var botIndex = 1;
        while (match.Teams.Count < DefaultTeamCount)
        {
            match.Teams.Add(new MatchTeamSlot
            {
                SquadId = Guid.Empty,
                SquadName = $"BotTim_{botIndex++}",
                PlayerIds = [],
                BotCount = Squad.MaxMembers
            });
        }

        // Sunucu tahsisi
        var servers = await _servers.GetReadyByRegionAsync(region, ct);
        var server = servers.FirstOrDefault(s => s.IsHealthy(TimeSpan.FromSeconds(30)));
        if (server is not null)
        {
            server.Status = GameServerStatus.Allocated;
            server.CurrentMatchId = match.Id;
            match.GameServerId = server.Id;
            match.ServerEndpoint = server.Endpoint;
            match.Status = MatchStatus.Starting;
            match.StartedAt = DateTimeOffset.UtcNow;
            await _servers.UpdateAsync(server, ct);
        }

        await _matches.AddAsync(match, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Maç oluşturuldu {MatchId} bölge={Region} tim={Teams}", match.Id, region, match.Teams.Count);
        return match;
    }

    public async Task<MatchDto?> GetMatchAsync(Guid matchId, CancellationToken ct = default)
    {
        var match = await _matches.GetByIdAsync(matchId, ct);
        return match is null ? null : Mapper.ToDto(match);
    }

    public async Task<QueueResponse?> GetTicketAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _tickets.GetByIdAsync(ticketId, ct);
        return ticket is null ? null : new QueueResponse(ticket.Id, ticket.Status.ToString(), ticket.Region, ticket.EnqueuedAt);
    }
}
