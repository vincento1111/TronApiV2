using TronApi.Contracts;
using TronApi.Infrastructure;
using TronApi.Services;

namespace TronApi.Endpoints;

public static class ChatEndpoints
{
    public static void MapChat(this WebApplication app)
    {
        var chat = app.MapGroup("/api/chat").RequireAuthorization();
        chat.MapGet("/messages", (int? after, int? withPlayerId, int? limit, HttpContext context, ChatService service, CancellationToken ct) =>
            service.History(context.User.PlayerId(), after, withPlayerId, limit ?? 100, ct));
        chat.MapPost("/messages", (SendMessageRequest request, HttpContext context, ChatService service, CancellationToken ct) =>
            service.Send(context.User.PlayerId(), request, ct)).RequireRateLimiting("chat");
    }
}
