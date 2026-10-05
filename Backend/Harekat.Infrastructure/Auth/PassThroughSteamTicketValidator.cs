using Harekat.Application.Abstractions;

namespace Harekat.Infrastructure.Auth;

/// <summary>Test / yerel stub — ticket içinden SteamId üretir (<c>DEV:{id}:{name}</c> veya ham sayı).</summary>
public sealed class PassThroughSteamTicketValidator : ISteamTicketValidator
{
    public Task<SteamTicketIdentity> ValidateAsync(string ticketHex, CancellationToken ct = default)
    {
        var t = (ticketHex ?? string.Empty).Trim();
        if (t.StartsWith("DEV:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = t[4..];
            var colon = rest.IndexOf(':');
            var idPart = colon >= 0 ? rest[..colon] : rest;
            var name = colon >= 0 ? rest[(colon + 1)..] : null;
            if (!ulong.TryParse(idPart, out var id))
                throw new Harekat.Application.Common.AppException("Geçersiz DEV Steam ticket.", 400, "validation");
            return Task.FromResult(new SteamTicketIdentity(id, string.IsNullOrWhiteSpace(name) ? null : name));
        }

        if (ulong.TryParse(t, out var raw))
            return Task.FromResult(new SteamTicketIdentity(raw, null));

        throw new Harekat.Application.Common.AppException("Steam ticket doğrulanamadı.", 401, "unauthorized");
    }
}
