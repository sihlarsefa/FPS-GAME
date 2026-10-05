namespace Harekat.Application.Abstractions;

public sealed record SteamTicketIdentity(ulong SteamId, string? PersonaName);

/// <summary>
/// Steam session / Web API ticket doğrulama.
/// Üretimde Steam Web API <c>ISteamUserAuth/AuthenticateUserTicket</c> kullanılır.
/// </summary>
public interface ISteamTicketValidator
{
    Task<SteamTicketIdentity> ValidateAsync(string ticketHex, CancellationToken ct = default);
}
