using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TronApi.Data;
using TronApi.Hubs;

namespace TronApi.Services;

public sealed class SessionService(GameDb db, WriteCoordinator writes, TimeProvider clock, ConnectionRegistry connections)
{
    public const string Scheme = "TronSession";
    public const string SessionClaim = "tron-session";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public async Task SignIn(HttpContext context, int playerId)
    {
        // An account switch also revokes any previous session attached to this browser.
        await Revoke(context.User.FindFirstValue(SessionClaim), context.RequestAborted);
        var expires = clock.GetUtcNow().Add(Lifetime);
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await writes.Execute(db, async () =>
        {
            var expired = await db.PlayerSessions.Where(s => s.ExpiresAt <= clock.GetUtcNow().UtcDateTime).ToListAsync(context.RequestAborted);
            db.PlayerSessions.RemoveRange(expired);
            db.PlayerSessions.Add(new() { Id = id, PlayerId = playerId, ExpiresAt = expires.UtcDateTime });
            return true;
        }, context.RequestAborted);
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, playerId.ToString()), new Claim(SessionClaim, id)
        ], Scheme);
        await context.SignInAsync(Scheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            ExpiresUtc = expires, IsPersistent = false, AllowRefresh = false
        });
    }

    public async Task SignOut(HttpContext context)
    {
        await Revoke(context.User.FindFirstValue(SessionClaim), context.RequestAborted);
        await context.SignOutAsync(Scheme);
    }

    public async Task<bool> IsActive(string? id, int playerId, CancellationToken ct = default) =>
        id is not null && await db.PlayerSessions.AsNoTracking().AnyAsync(s => s.Id == id && s.PlayerId == playerId && s.ExpiresAt > clock.GetUtcNow().UtcDateTime, ct);

    private async Task Revoke(string? id, CancellationToken ct)
    {
        if (id is null) return;
        await writes.Execute(db, async () =>
        {
            var session = await db.PlayerSessions.SingleOrDefaultAsync(s => s.Id == id, ct);
            if (session is not null) db.PlayerSessions.Remove(session);
            return true;
        }, ct);
        connections.AbortSession(id);
    }

    public static async Task Validate(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(SessionClaim);
        var player = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(player, out var playerId) ||
            !await context.HttpContext.RequestServices.GetRequiredService<SessionService>().IsActive(id, playerId, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
        }
    }
}
