using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Harekat.Application.Abstractions;
using Harekat.Application.Common;
using Harekat.Application.Dtos;
using Harekat.Domain.Entities;
using Harekat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harekat.Application.Services;

public sealed class AuthService
{
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_]{3,24}$", RegexOptions.Compiled);
    private readonly IPlayerRepository _players;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IEmailService _email;
    private readonly IModerationRepository _moderation;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IPlayerRepository players,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IEmailService email,
        IModerationRepository moderation,
        IUnitOfWork uow,
        ILogger<AuthService> logger)
    {
        _players = players;
        _hasher = hasher;
        _jwt = jwt;
        _email = email;
        _moderation = moderation;
        _uow = uow;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip = null, CancellationToken ct = default)
    {
        ValidateCredentials(request.Username, request.Password, request.Email);

        if (await _players.GetByUsernameAsync(request.Username, ct) is not null)
            throw new AppException("Kullanıcı adı alınmış.", 409, ErrorCodes.Conflict);
        if (await _players.GetByEmailAsync(request.Email, ct) is not null)
            throw new AppException("E-posta zaten kayıtlı.", 409, ErrorCodes.Conflict);

        var verifyToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var player = new Player
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _hasher.Hash(request.Password),
            Region = string.IsNullOrWhiteSpace(request.Region) ? "tr" : request.Region!.Trim().ToLowerInvariant(),
            EmailVerificationToken = verifyToken,
            Stats = new CareerStats()
        };

        await _players.AddAsync(player, ct);
        await _email.SendVerificationAsync(player.Email, verifyToken, ct);
        await _moderation.AddAuditAsync(new AuditLogEntry
        {
            ActorId = player.Id,
            Action = "auth.register",
            Resource = $"player:{player.Id}",
            IpAddress = ip
        }, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Oyuncu kaydı: {Username}", player.Username);
        return await PersistTokensAsync(player, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ip = null, CancellationToken ct = default)
    {
        var player = await _players.GetByUsernameAsync(request.Username, ct)
                     ?? throw new AppException("Geçersiz kullanıcı adı veya şifre.", 401, ErrorCodes.Unauthorized);

        if (!_hasher.Verify(request.Password, player.PasswordHash))
            throw new AppException("Geçersiz kullanıcı adı veya şifre.", 401, ErrorCodes.Unauthorized);

        if (player.IsCurrentlyBanned)
            throw new AppException($"Hesap yasaklı: {player.BanReason}", 403, ErrorCodes.Banned);

        player.LastSeenAt = DateTimeOffset.UtcNow;
        player.IsOnline = true;
        await _moderation.AddAuditAsync(new AuditLogEntry
        {
            ActorId = player.Id,
            Action = "auth.login",
            Resource = $"player:{player.Id}",
            IpAddress = ip
        }, ct);

        return await PersistTokensAsync(player, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var player = await _players.GetByRefreshTokenHashAsync(hash, ct)
                     ?? throw new AppException("Geçersiz refresh token.", 401, ErrorCodes.Unauthorized);

        if (player.RefreshTokenExpiresAt is null || player.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
            throw new AppException("Refresh token süresi dolmuş.", 401, ErrorCodes.Unauthorized);

        if (player.IsCurrentlyBanned)
            throw new AppException("Hesap yasaklı.", 403, ErrorCodes.Banned);

        return await PersistTokensAsync(player, ct);
    }

    public async Task VerifyEmailAsync(Guid playerId, string token, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        if (player.EmailVerified)
            return;

        if (!string.Equals(player.EmailVerificationToken, token, StringComparison.Ordinal))
            throw new AppException("Doğrulama kodu geçersiz.", 400, ErrorCodes.Validation);

        player.EmailVerified = true;
        player.EmailVerificationToken = null;
        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task LogoutAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct);
        if (player is null) return;
        player.RefreshTokenHash = null;
        player.RefreshTokenExpiresAt = null;
        player.IsOnline = false;
        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> PersistTokensAsync(Player player, CancellationToken ct)
    {
        var (access, expires) = _jwt.CreateAccessToken(player);
        var refresh = _jwt.CreateRefreshToken();
        player.RefreshTokenHash = _jwt.HashRefreshToken(refresh);
        player.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
        return new AuthResponse(access, refresh, expires, Mapper.ToDto(player));
    }

    private static void ValidateCredentials(string username, string password, string email)
    {
        if (string.IsNullOrWhiteSpace(username) || !UsernameRegex.IsMatch(username))
            throw new AppException("Kullanıcı adı 3-24 karakter, alfanümerik/alt çizgi olmalı.", 400, ErrorCodes.Validation);
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new AppException("Şifre en az 8 karakter olmalı.", 400, ErrorCodes.Validation);
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new AppException("Geçerli bir e-posta girin.", 400, ErrorCodes.Validation);
    }
}
