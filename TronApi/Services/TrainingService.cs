using Microsoft.EntityFrameworkCore;
using TronApi.Contracts;
using TronApi.Data;
using TronApi.Infrastructure;

namespace TronApi.Services;

public sealed class TrainingService(GameDb db, WriteCoordinator writes, TimeProvider clock)
{
    public Task<StatsDto> Train(int playerId, TrainingRequest request, CancellationToken ct) => writes.Execute(db, async () =>
    {
        var amount = Input.Quantity(request.Amount, GameRules.MaxEnergy);
        var stat = request.Stat?.ToLowerInvariant();
        if (stat is not ("strength" or "defense" or "speed" or "dexterity")) throw ApiException.BadRequest("Choose a valid training stat.");
        var player = await db.Players.SingleOrDefaultAsync(p => p.Id == playerId, ct) ?? throw new ApiException(401, "Please sign in again.");
        GameRules.Regenerate(player, clock.GetUtcNow().UtcDateTime);
        if (player.Energy < amount) throw ApiException.BadRequest("Not enough energy.");
        try
        {
            checked
            {
                switch (stat)
                {
                    case "strength": player.Strength += amount; break;
                    case "defense": player.Defense += amount; break;
                    case "speed": player.Speed += amount; break;
                    case "dexterity": player.Dexterity += amount; break;
                }
            }
        }
        catch (OverflowException) { throw ApiException.BadRequest("This stat has reached its limit."); }
        player.Energy -= amount;
        return Dto.Stats(player);
    }, ct);
}
