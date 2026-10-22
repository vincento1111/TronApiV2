using TronApi.Data;
using TronApi.Services;
using Xunit;

namespace TronApi.Tests;

public sealed class EnergyTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RegenerationPreservesPartialIntervalAndSurvivesDowntime()
    {
        var player = new Player { Energy = 5, EnergyUpdatedAt = Now.AddMinutes(-35) };
        GameRules.Regenerate(player, Now);
        Assert.Equal(8, player.Energy);
        Assert.Equal(Now.AddMinutes(-5), player.EnergyUpdatedAt);
        GameRules.Regenerate(player, Now.AddMinutes(4));
        Assert.Equal(8, player.Energy);
        GameRules.Regenerate(player, Now.AddMinutes(5));
        Assert.Equal(9, player.Energy);
    }

    [Fact]
    public void FullEnergyDoesNotBankRecoveryForFutureSpending()
    {
        var player = new Player { Energy = 71, EnergyUpdatedAt = Now.AddDays(-10) };
        GameRules.Regenerate(player, Now);
        Assert.Equal(72, player.Energy);
        Assert.Equal(Now, player.EnergyUpdatedAt);
        player.Energy = 0;
        GameRules.Regenerate(player, Now.AddMinutes(9));
        Assert.Equal(0, player.Energy);
        GameRules.Regenerate(player, Now.AddMinutes(10));
        Assert.Equal(1, player.Energy);
    }

    [Fact]
    public void ClockMovingBackwardsDoesNotCreateOrRemoveEnergy()
    {
        var player = new Player { Energy = 30, EnergyUpdatedAt = Now };
        GameRules.Regenerate(player, Now.AddMinutes(-30));
        Assert.Equal(30, player.Energy);
        Assert.Equal(Now, player.EnergyUpdatedAt);
    }
}
