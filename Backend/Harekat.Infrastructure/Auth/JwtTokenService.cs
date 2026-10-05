using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Harekat.Application.Abstractions;
using Harekat.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Harekat.Infrastructure.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;
    private readonly TimeSpan _accessLifetime;

    public JwtTokenService(IConfiguration config)
    {
        _issuer = config["Jwt:Issuer"] ?? "harekat";
        _audience = config["Jwt:Audience"] ?? "harekat-clients";
        var secret = config["Jwt:Secret"] ?? "HarekatDevSecretKey_ChangeInProduction_Min32Chars!";
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _accessLifetime = TimeSpan.FromMinutes(int.TryParse(config["Jwt:AccessMinutes"], out var m) ? m : 60);
    }

    public (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(Player player)
    {
        var expires = DateTimeOffset.UtcNow.Add(_accessLifetime);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, player.Username),
            new(ClaimTypes.Role, player.Role),
            new("rank", ((int)player.Stats.Rank).ToString())
        };

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}
