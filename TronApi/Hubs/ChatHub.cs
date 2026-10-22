using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TronApi.Infrastructure;
using TronApi.Services;

namespace TronApi.Hubs;

// Clients cannot invoke a send method. REST validates CSRF and commits before broadcasting.
[Authorize]
public sealed class ChatHub(ConnectionRegistry connections, SessionService sessions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var sessionId = Context.User?.FindFirstValue(SessionService.SessionClaim);
        if (sessionId is null) { Context.Abort(); return; }
        // Register first, then recheck. Logout racing a connection cannot leave its socket subscribed.
        connections.Add(sessionId, Context.ConnectionId, Context.Abort);
        if (!await sessions.IsActive(sessionId, Context.User!.PlayerId(), Context.ConnectionAborted))
        {
            connections.Remove(sessionId, Context.ConnectionId);
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var sessionId = Context.User?.FindFirstValue(SessionService.SessionClaim);
        if (sessionId is not null) connections.Remove(sessionId, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
