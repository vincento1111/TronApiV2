using System.Security.Cryptography;
using TronApi.Data;

namespace TronApi.Services;

public static class ProgressionRules
{
    public const long StatScale = 1_000_000_000L;
    public const long MaxStatUnits = 100 * StatScale;
    public const int MaxLevel = 100;
    public const int DailyEnergyBoost = 24;
    public const decimal BaseGainPerEnergy = .0125m;
    public const double DiminishingExponent = .75d;
    private static readonly long[] Thresholds = BuildThresholds();
    public static long MaximumExperience => Thresholds[^1];

    public static decimal Stat(long units) => units / (decimal)StatScale;
    public static decimal MasteryMultiplier(long totalEnergy) => 1m + .25m * totalEnergy / (totalEnergy + 10000m);
    public static decimal BonusPercent(int ppm) => ppm / 10000m;
    public static int MaxHealth(int level) => 100 + 5 * (level - 1);
    public static long ExperienceAtLevel(int level) => Thresholds[Math.Clamp(level, 1, MaxLevel) - 1];

    public static long ExperienceForNextLevel(int level)
    {
        if (level >= MaxLevel) return 0;
        var n = (decimal)Math.Max(0, level - 1);
        return (long)decimal.Ceiling(500m + 20m * n + .0008m * n * n * n * n);
    }

    public static int LevelForExperience(long experience)
    {
        var found = Array.BinarySearch(Thresholds, Math.Clamp(experience, 0, MaximumExperience));
        return found >= 0 ? found + 1 : ~found;
    }

    public static int AddExperience(Player player, int amount)
    {
        var previousLevel = player.Level;
        player.Experience = Math.Min(MaximumExperience, player.Experience + amount);
        player.Level = LevelForExperience(player.Experience);
        var maximum = MaxHealth(player.Level);
        player.Health = Math.Clamp(player.Health + maximum - MaxHealth(previousLevel), 0, maximum);
        return player.Level - previousLevel;
    }

    public static long GainUnits(long statUnits, long totalEnergy, int bonusPpm, decimal randomMultiplier)
    {
        if (statUnits >= MaxStatUnits) return 0;
        var divisor = (decimal)Math.Pow((double)Stat(statUnits), DiminishingExponent);
        var gain = BaseGainPerEnergy / divisor * MasteryMultiplier(totalEnergy) *
            (1m + bonusPpm / 1_000_000m) * randomMultiplier;
        var units = (long)decimal.Round(gain * StatScale, 0, MidpointRounding.AwayFromZero);
        return Math.Min(MaxStatUnits - statUnits, Math.Max(1L, units));
    }

    public static void RefreshEnergyBudget(Player player, DateTime now)
    {
        if (player.EnergyBoostDay.Date == now.Date) return;
        player.EnergyBoostDay = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        player.EnergyBoostUsed = 0;
    }

    /// <summary>
    /// Converts legacy +1-per-energy work using the integral of the expected new curve.
    /// This is deterministic and constant-time even for billions of legacy training actions.
    /// The migration visits strength, defense, speed, then dexterity; historical ordering was not recorded.
    /// </summary>
    public static long ConvertLegacyStat(long energySpent, long precedingEnergy)
    {
        if (energySpent <= 0) return StatScale;
        var start = (double)precedingEnergy + 10000d;
        var masteryIntegral = 1.25d * energySpent - 2500d * Math.Log(1d + energySpent / start);
        var exponent = 1d + DiminishingExponent;
        var stat = Math.Pow(1d + exponent * (double)BaseGainPerEnergy * masteryIntegral, 1d / exponent);
        return (long)decimal.Round((decimal)Math.Min(100d, stat) * StatScale, 0, MidpointRounding.AwayFromZero);
    }

    private static long[] BuildThresholds()
    {
        var result = new long[MaxLevel];
        for (var level = 1; level < MaxLevel; level++) result[level] = result[level - 1] + ExperienceForNextLevel(level);
        return result;
    }
}

public interface ITrainingRandom
{
    decimal NextMultiplier();
}

public sealed class SecureTrainingRandom : ITrainingRandom
{
    public decimal NextMultiplier() => .97m + .06m *
        RandomNumberGenerator.GetInt32(int.MaxValue) / (int.MaxValue - 1m);
}
