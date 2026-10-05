using System.Security.Claims;
using Harekat.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Harekat.Api.Hubs;

/// <summary>Tim içi lobi sohbeti ve hazır durumu.</summary>
[Authorize]
public sealed class LobbyHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var squadId = Context.GetHttpContext()?.Request.Query["squadId"].FirstOrDefault();
        if (Guid.TryParse(squadId, out var id))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(id));
        await base.OnConnectedAsync();
    }

    public async Task JoinSquad(Guid squadId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(squadId));
    }

    public async Task SendChat(Guid squadId, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 280)
            return;

        var userId = GetUserId();
        var name = Context.User?.Identity?.Name ?? "Asker";
        var payload = new LobbyChatMessage(squadId, userId, name, message.Trim(), DateTimeOffset.UtcNow);
        await Clients.Group(GroupName(squadId)).SendAsync("Chat", payload);
    }

    public async Task NotifyReady(Guid squadId, bool isReady, bool allReady)
    {
        var userId = GetUserId();
        await Clients.Group(GroupName(squadId)).SendAsync("Ready", new ReadyStatusDto(squadId, userId, isReady, allReady));
    }

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static string GroupName(Guid squadId) => $"squad:{squadId}";
}
