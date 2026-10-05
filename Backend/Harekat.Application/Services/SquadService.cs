using Harekat.Application.Abstractions;
using Harekat.Application.Common;
using Harekat.Application.Dtos;
using Harekat.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Harekat.Application.Services;

public sealed class PlayerService
{
    private readonly IPlayerRepository _players;
    private readonly IUnitOfWork _uow;

    public PlayerService(IPlayerRepository players, IUnitOfWork uow)
    {
        _players = players;
        _uow = uow;
    }

    public async Task<PlayerDto> GetMeAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);
        return Mapper.ToDto(player);
    }

    public async Task<PlayerDto> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var player = await _players.GetByUsernameAsync(username, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);
        return Mapper.ToDto(player);
    }

    public async Task SetPresenceAsync(Guid playerId, bool online, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct);
        if (player is null) return;
        player.IsOnline = online;
        player.LastSeenAt = DateTimeOffset.UtcNow;
        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
    }
}

public sealed class SquadService
{
    private readonly ISquadRepository _squads;
    private readonly IPlayerRepository _players;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SquadService> _logger;

    public SquadService(ISquadRepository squads, IPlayerRepository players, IUnitOfWork uow, ILogger<SquadService> logger)
    {
        _squads = squads;
        _players = players;
        _uow = uow;
        _logger = logger;
    }

    public async Task<SquadDto> CreateAsync(Guid leaderId, CreateSquadRequest request, CancellationToken ct = default)
    {
        var leader = await _players.GetByIdAsync(leaderId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        if (leader.IsCurrentlyBanned)
            throw new AppException("Yasaklı hesap tim kuramaz.", 403, ErrorCodes.Banned);

        if (leader.SquadId is not null)
            throw new AppException("Zaten bir timdesiniz.", 409, ErrorCodes.Conflict);

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length is < 2 or > 32)
            throw new AppException("Tim adı 2-32 karakter olmalı.", 400, ErrorCodes.Validation);

        var squad = new Squad
        {
            Name = request.Name.Trim(),
            LeaderId = leaderId,
            MemberIds = [leaderId],
            ReadyMemberIds = [leaderId],
            Region = string.IsNullOrWhiteSpace(request.Region) ? leader.Region : request.Region!
        };

        leader.SquadId = squad.Id;
        IncrementAchievement(leader, "squads_created", 1);

        await _squads.AddAsync(squad, ct);
        await _players.UpdateAsync(leader, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Tim kuruldu: {Squad} by {Leader}", squad.Name, leader.Username);
        return Mapper.ToDto(squad);
    }

    public async Task<SquadDto> JoinByInviteAsync(Guid playerId, InviteSquadRequest request, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        if (player.SquadId is not null)
            throw new AppException("Zaten bir timdesiniz.", 409, ErrorCodes.Conflict);

        var squad = await _squads.GetByInviteCodeAsync(request.InviteCode.Trim().ToUpperInvariant(), ct)
                    ?? throw new AppException("Davet kodu geçersiz.", 404, ErrorCodes.NotFound);

        if (squad.IsInQueue || squad.IsInMatch)
            throw new AppException("Tim kuyrukta veya maçta; katılınamaz.", 409, ErrorCodes.Conflict);

        if (!squad.TryAddMember(playerId))
            throw new AppException("Tim dolu.", 409, ErrorCodes.Conflict);

        player.SquadId = squad.Id;
        await _squads.UpdateAsync(squad, ct);
        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
        return Mapper.ToDto(squad);
    }

    public async Task<SquadDto> GetAsync(Guid squadId, CancellationToken ct = default)
    {
        var squad = await _squads.GetByIdAsync(squadId, ct)
                    ?? throw new AppException("Tim bulunamadı.", 404, ErrorCodes.NotFound);
        return Mapper.ToDto(squad);
    }

    public async Task<SquadDto?> GetMineAsync(Guid playerId, CancellationToken ct = default)
    {
        var squad = await _squads.GetByMemberIdAsync(playerId, ct);
        return squad is null ? null : Mapper.ToDto(squad);
    }

    public async Task<ReadyStatusDto> SetReadyAsync(Guid playerId, bool ready, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);
        var squad = await _squads.GetByMemberIdAsync(playerId, ct)
                    ?? throw new AppException("Timde değilsiniz.", 404, ErrorCodes.NotFound);

        if (ready)
            squad.ReadyMemberIds.Add(playerId);
        else
            squad.ReadyMemberIds.Remove(playerId);

        await _squads.UpdateAsync(squad, ct);
        await _uow.SaveChangesAsync(ct);
        return new ReadyStatusDto(squad.Id, playerId, ready, squad.AllReady);
    }

    public async Task LeaveAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);
        var squad = await _squads.GetByMemberIdAsync(playerId, ct)
                    ?? throw new AppException("Timde değilsiniz.", 404, ErrorCodes.NotFound);

        if (squad.IsInQueue || squad.IsInMatch)
            throw new AppException("Kuyruk/maç sırasında ayrılamazsınız.", 409, ErrorCodes.Conflict);

        squad.RemoveMember(playerId);
        player.SquadId = null;

        if (squad.MemberIds.Count == 0)
            await _squads.DeleteAsync(squad.Id, ct);
        else
            await _squads.UpdateAsync(squad, ct);

        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static void IncrementAchievement(Player player, string metric, int delta)
    {
        player.AchievementProgress.TryGetValue(metric, out var cur);
        player.AchievementProgress[metric] = cur + delta;
    }
}
