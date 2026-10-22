using System.Security.Cryptography;
using TronApi.Data;
using TronApi.Infrastructure;

namespace TronApi.Services;

public static class GameRules
{
    public const int MaxEnergy = 72;
    public static readonly TimeSpan EnergyInterval = TimeSpan.FromMinutes(10);

    public static void Regenerate(Player player, DateTime now)
    {
        if (player.Energy >= MaxEnergy)
        {
            player.EnergyUpdatedAt = now;
            return;
        }
        var ticks = Math.Max(0L, (now.Ticks - player.EnergyUpdatedAt.Ticks) / EnergyInterval.Ticks);
        if (ticks == 0) return;
        player.Energy = (int)Math.Min(MaxEnergy, player.Energy + ticks);
        player.EnergyUpdatedAt = player.Energy == MaxEnergy ? now : player.EnergyUpdatedAt.AddTicks(ticks * EnergyInterval.Ticks);
    }

    public static int RollPower(ItemDefinition definition) =>
        definition.BasePower + RandomNumberGenerator.GetInt32(0, Math.Max(2, definition.BasePower / 2 + 1));

    public static int RollTrainingBonus(ItemDefinition definition) => definition.BaseTrainingBonusPpm == 0 ? 0 :
        RandomNumberGenerator.GetInt32(definition.BaseTrainingBonusPpm - definition.TrainingBonusRollRangePpm,
            definition.BaseTrainingBonusPpm + definition.TrainingBonusRollRangePpm + 1);

    public static void AddMoney(Player player, long amount)
    {
        var result = player.Money + amount;
        if (result < 0) throw ApiException.BadRequest("Not enough money.");
        if (result > int.MaxValue) throw ApiException.BadRequest("Your wallet cannot hold any more money.");
        player.Money = (int)result;
    }
}
