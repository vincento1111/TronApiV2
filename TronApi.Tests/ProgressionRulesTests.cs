using TronApi.Contracts;
using TronApi.Data;
using TronApi.Services;
using Xunit;

namespace TronApi.Tests;

public sealed class ProgressionRulesTests
{
    [Fact]
    public void FractionalItemBonusIsExactAndAffectsStatGainWithoutIntegerTruncation()
    {
        Assert.Equal(3.4572m, ProgressionRules.BonusPercent(34572));
        var baseGain = ProgressionRules.GainUnits(ProgressionRules.StatScale, 0, 0, 1m);
        var gearedGain = ProgressionRules.GainUnits(ProgressionRules.StatScale, 0, 34572, 1m);
        Assert.Equal(12_500_000L, baseGain);
        Assert.Equal(12_932_150L, gearedGain);
        Assert.Equal(.012932150m, ProgressionRules.Stat(gearedGain));
    }

    [Theory]
    [InlineData(2, 500)]
    [InlineData(5, 2123)]
    [InlineData(50, 90963)]
    [InlineData(90, 991400)]
    [InlineData(100, 1629982)]
    public void LevelThresholdsFollowTheDefinedCurve(int level, long threshold)
    {
        Assert.Equal(threshold, ProgressionRules.ExperienceAtLevel(level));
        Assert.Equal(level - 1, ProgressionRules.LevelForExperience(threshold - 1));
        Assert.Equal(level, ProgressionRules.LevelForExperience(threshold));
    }

    [Fact]
    public void LevelUpIncreasesHealthCapacityWithoutRemovingExistingDamage()
    {
        var player = new Player { Experience = 490, Level = 1, Health = 70 };
        Assert.Equal(1, ProgressionRules.AddExperience(player, 10));
        Assert.Equal(2, player.Level);
        Assert.Equal(75, player.Health);
        Assert.Equal(105, Dto.Stats(player).MaxHealth);
        Assert.Equal(0, Dto.Stats(player).XpIntoLevel);
        Assert.Equal(521, Dto.Stats(player).XpToNextLevel);
    }

    [Fact]
    public void ExperienceStopsAtLevel100WhileMasteryRemainsIndependent()
    {
        var player = new Player { Level = 99, Experience = ProgressionRules.MaximumExperience - 2, Health = 500, TotalTrainingEnergy = 200_000 };
        ProgressionRules.AddExperience(player, 10);
        Assert.Equal(ProgressionRules.MaximumExperience, player.Experience);
        Assert.Equal(100, player.Level);
        Assert.Equal(505, player.Health);
        Assert.Equal(0, Dto.Stats(player).XpToNextLevel);
        Assert.Equal(200_000, player.TotalTrainingEnergy);
        Assert.InRange(Dto.Stats(player).TrainingMasteryPercent, 23m, 25m);
    }

    [Fact]
    public void EveryGainIsPositiveRoundedToNinePlacesAndCappedAt100()
    {
        foreach (var value in new[] { 1m, 5.314159265m, 50m, 99.999m })
        {
            var raw = (long)(value * ProgressionRules.StatScale);
            var minimum = ProgressionRules.GainUnits(raw, 0, 0, .97m);
            var maximum = ProgressionRules.GainUnits(raw, 10000, 34572, 1.03m);
            Assert.True(minimum > 0);
            Assert.True(maximum >= minimum);
            Assert.True(raw + maximum <= ProgressionRules.MaxStatUnits);
            Assert.Equal(ProgressionRules.Stat(maximum), decimal.Round(ProgressionRules.Stat(maximum), 9));
        }
        Assert.Equal(1, ProgressionRules.GainUnits(ProgressionRules.MaxStatUnits - 1, 0, 0, 1m));
        Assert.Equal(0, ProgressionRules.GainUnits(ProgressionRules.MaxStatUnits, 0, 0, 1m));
    }

    [Fact]
    public void DailyAllowanceResetsOnUtcDateChangeWithoutChangingNaturalEnergy()
    {
        var player = new Player { Energy = 7, EnergyBoostUsed = 24, EnergyBoostDay = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc) };
        ProgressionRules.RefreshEnergyBudget(player, new DateTime(2026, 10, 22, 23, 59, 59, DateTimeKind.Utc));
        Assert.Equal(24, player.EnergyBoostUsed);
        ProgressionRules.RefreshEnergyBudget(player, new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(0, player.EnergyBoostUsed);
        Assert.Equal(7, player.Energy);
        Assert.Equal(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc), Dto.Stats(player).EnergyBoostResetAt);
    }

    [Fact]
    public void LegacyConversionIsBoundedAndEfficientForLargeHistories()
    {
        Assert.Equal(ProgressionRules.StatScale, ProgressionRules.ConvertLegacyStat(0, 0));
        Assert.InRange(ProgressionRules.Stat(ProgressionRules.ConvertLegacyStat(100, 0)), 1.9m, 2m);
        Assert.Equal(ProgressionRules.MaxStatUnits, ProgressionRules.ConvertLegacyStat(int.MaxValue, int.MaxValue));
    }
}
