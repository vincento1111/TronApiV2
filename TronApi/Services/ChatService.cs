using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TronApi.Contracts;
using TronApi.Data;
using TronApi.Hubs;
using TronApi.Infrastructure;

namespace TronApi.Services;

public sealed class ChatService(GameDb db, WriteCoordinator writes, TimeProvider clock, IHubContext<ChatHub> hub, ILogger<ChatService> logger)
{
    public async Task<MessagePage> History(int viewerId, int? after, int? withPlayerId, int limit, CancellationToken ct)
    {
        if (after < 0) throw ApiException.BadRequest("Invalid message cursor.");
        Input.Quantity(limit, 100);
        if (withPlayerId == viewerId) throw ApiException.BadRequest("Choose another player for a direct conversation.");
        if (withPlayerId.HasValue && !await db.Players.AnyAsync(p => p.Id == withPlayerId, ct))
            throw ApiException.NotFound("Player not found.");
        var query = db.ChatMessages.AsNoTracking().Include(m => m.Sender).AsQueryable();
        query = withPlayerId is int other
            ? query.Where(m => (m.SenderId == viewerId && m.RecipientId == other) || (m.SenderId == other && m.RecipientId == viewerId))
            : query.Where(m => m.RecipientId == null);
        List<ChatMessage> messages;
        var hasMore = false;
        if (after.HasValue)
        {
            messages = await query.Where(m => m.Id > after.Value).OrderBy(m => m.Id).Take(limit + 1).ToListAsync(ct);
            hasMore = messages.Count > limit;
            if (hasMore) messages.RemoveAt(messages.Count - 1);
        }
        else
        {
            messages = await query.OrderByDescending(m => m.Id).Take(limit).ToListAsync(ct);
            messages.Reverse();
        }
        return new(messages.Select(Dto.Message).ToList(), messages.LastOrDefault()?.Id ?? after ?? 0, hasMore);
    }

    public async Task<MessageDto> Send(int senderId, SendMessageRequest request, CancellationToken ct)
    {
        var content = Input.Text(request.Content, "Message", 1, 2000);
        if (!Guid.TryParse(request.ClientMessageId, out var requestId)) throw ApiException.BadRequest("A valid clientMessageId is required.");
        if (request.RecipientId == senderId) throw ApiException.BadRequest("Choose another player for a direct conversation.");
        var clientId = requestId.ToString("D");
        var outcome = await writes.Execute(db, async () =>
        {
            var previous = await db.ChatMessages.Include(m => m.Sender).SingleOrDefaultAsync(m => m.SenderId == senderId && m.ClientMessageId == clientId, ct);
            if (previous is not null)
            {
                if (previous.Content != content || previous.RecipientId != request.RecipientId)
                    throw new ApiException(409, "This message request ID was already used for different content.");
                return (Message: Dto.Message(previous), Created: false);
            }
            if (request.RecipientId is int recipient && !await db.Players.AnyAsync(p => p.Id == recipient, ct))
                throw ApiException.NotFound("Recipient not found.");
            var sender = await db.Players.SingleOrDefaultAsync(p => p.Id == senderId, ct) ?? throw new ApiException(401, "Please sign in again.");
            var message = new ChatMessage
            {
                SenderId = senderId, Sender = sender, RecipientId = request.RecipientId,
                Content = content, ClientMessageId = clientId, CreatedAt = clock.GetUtcNow().UtcDateTime
            };
            db.ChatMessages.Add(message);
            await db.SaveChangesAsync(ct);
            return (Message: Dto.Message(message), Created: true);
        }, ct);
        if (outcome.Created)
        {
            try
            {
                var audience = outcome.Message.RecipientId is int recipient
                    ? hub.Clients.Users([senderId.ToString(), recipient.ToString()]) : hub.Clients.All;
                // Persistence is authoritative. A lost notification is recovered through the history cursor.
                await audience.SendAsync("message", outcome.Message, CancellationToken.None);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Chat notification failed for persisted message {MessageId}", outcome.Message.Id); }
        }
        return outcome.Message;
    }
}
