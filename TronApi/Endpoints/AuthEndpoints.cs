using Microsoft.AspNetCore.Antiforgery;
using TronApi.Contracts;
using TronApi.Infrastructure;
using TronApi.Services;

namespace TronApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapGet("/api/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        }).AllowAnonymous();

        var auth = app.MapGroup("/api/auth");
        auth.MapGet("/me", (HttpContext context, AccountService accounts, CancellationToken ct) =>
            accounts.Me(context.User.PlayerId(), ct)).RequireAuthorization();

        auth.MapPost("/register", async (RegisterRequest request, HttpContext context, AccountService accounts, SessionService sessions, CancellationToken ct) =>
        {
            var me = await accounts.Register(request, ct);
            await sessions.SignIn(context, me.Id);
            return Results.Ok(me);
        }).RequireRateLimiting("auth").AllowAnonymous();

        auth.MapPost("/login", async (LoginRequest request, HttpContext context, AccountService accounts, SessionService sessions, CancellationToken ct) =>
        {
            var me = await accounts.Login(request, ct);
            await sessions.SignIn(context, me.Id);
            return Results.Ok(me);
        }).RequireRateLimiting("auth").AllowAnonymous();

        auth.MapPost("/logout", async (HttpContext context, SessionService sessions) =>
        {
            await sessions.SignOut(context);
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
