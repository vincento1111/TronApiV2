using Microsoft.EntityFrameworkCore;
using TronApi.Contracts;
using TronApi.Data;
using TronApi.Infrastructure;

namespace TronApi.Services;

public sealed class TrainingService(GameDb db, WriteCoordinator writes, TimeProvider clock, PlayerStatsService stats, ITrainingRandom random)
{
    public Task<TrainingResultDto> Train(int playerId, TrainingRequest request, CancellationToken ct) => writes.Execute(db, async () =>
    {
        var amount = Input.Quantity(request.Amount, GameRules.MaxEnergy);
        var stat = request.Stat?.ToLowerInvariant();
        if (stat is not ("strength" or "defense" or "speed" or "dexterity")) throw ApiException.BadRequest("Choose a valid training stat.");
        var player = await db.Players.SingleOrDefaultAsync(p => p.Id == playerId, ct) ?? throw new ApiException(401, "Please sign in again.");
        var now = clock.GetUtcNow().UtcDateTime;
        GameRules.Regenerate(player, now);
        ProgressionRules.RefreshEnergyBudget(player, now);
        var bonus = await stats.TrainingBonus(playerId, ct);
        var beforeStat = ReadStat(player, stat);
        var beforeXp = player.Experience;
        var beforeLevel = player.Level;
        if (beforeStat < ProgressionRules.MaxStatUnits && player.Energy < amount) throw ApiException.BadRequest("Not enough energy.");
        var spent = 0;
        var current = beforeStat;
        for (; spent < amount && current < ProgressionRules.MaxStatUnits; spent++)
        {
            if (player.TotalTrainingEnergy == long.MaxValue) throw ApiException.BadRequest("Your training history has reached its limit.");
            current += ProgressionRules.GainUnits(current, player.TotalTrainingEnergy, bonus, random.NextMultiplier());
            player.TotalTrainingEnergy++;
            player.Energy--;
            ProgressionRules.AddExperience(player, 10);
        }
        WriteStat(player, stat, current);
        return new TrainingResultDto(Dto.Stats(player, bonus), stat, spent, ProgressionRules.Stat(current - beforeStat),
            player.Experience - beforeXp, player.Level - beforeLevel);
    }, ct);

    private static long ReadStat(Player p, string stat) => stat switch
    {
        "strength" => p.Strength, "defense" => p.Defense, "speed" => p.Speed, _ => p.Dexterity
    };
    private static void WriteStat(Player p, string stat, long value)
    {
        switch (stat)
        {
            case "strength": p.Strength = value; break;
            case "defense": p.Defense = value; break;
            case "speed": p.Speed = value; break;
            default: p.Dexterity = value; break;
        }
    }
}
