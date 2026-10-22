using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TronApi.Data;
using TronApi.Infrastructure;
using TronApi.Services;
using Xunit;

namespace TronApi.Tests;

public sealed class ProgressionTransactionTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"tron-progression-{Guid.NewGuid():N}.db");
    private readonly WriteCoordinator writes = new();
    private readonly MutableClock clock = new(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));
    private GameDb CreateDb() => new(new DbContextOptionsBuilder<GameDb>().UseSqlite($"Data Source={path};Pooling=False").Options);
    private TrainingService Training(GameDb db) => new(db, writes, clock, new PlayerStatsService(db), new FixedRandom());

    public async Task InitializeAsync()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        db.Players.Add(new() { Id = 1, Email = "test@tron.local", NormalizedEmail = "TEST@TRON.LOCAL", DisplayName = "Test",
            NormalizedDisplayName = "TEST", PasswordHash = "unused", CreatedAt = clock.Now, EnergyUpdatedAt = clock.Now, EnergyBoostDay = clock.Now.Date });
        db.ItemDefinitions.AddRange(
            new() { Id = 1, Name = "Sword", Kind = "weapon", Tracking = "instance", Price = 100, BasePower = 10, EquipmentSlot = "weapon" },
            new() { Id = 3, Name = "Soda", Kind = "consumable", Tracking = "stack", Price = 10, EnergyRestore = 6 },
            new() { Id = 4, Name = "Training gear", Kind = "training", Tracking = "instance", Price = 100, EquipmentSlot = "training", BaseTrainingBonusPpm = 34572 });
        db.InventoryStacks.Add(new() { OwnerId = 1, DefinitionId = 3, Quantity = 1000 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task OneBatchAndMultipleRequestsHaveTheSameMasteryAndProgress()
    {
        await using var db = CreateDb();
        var training = Training(db);
        var first = await training.Train(1, new("strength", 10), default);
        var firstRaw = (await db.Players.SingleAsync()).Strength;
        Assert.Equal(100, first.XpGained);
        Assert.Equal(10, first.EnergySpent);
        Assert.Equal(10, first.Stats.TotalTrainingEnergy);
        var player = await db.Players.SingleAsync();
        player.Strength = ProgressionRules.StatScale;
        player.TotalTrainingEnergy = 0;
        player.Experience = 0;
        player.Energy = 72;
        await db.SaveChangesAsync();
        for (var n = 0; n < 10; n++) await training.Train(1, new("strength", 1), default);
        Assert.Equal(firstRaw, player.Strength);
        Assert.Equal(10, player.TotalTrainingEnergy);
        Assert.Equal(100, player.Experience);
    }

    [Fact]
    public async Task GearRollSurvivesRenameAndStacksWithIndependentWeaponSlot()
    {
        await using var db = CreateDb();
        var inventory = new InventoryService(db, writes, clock);
        var sword = (await inventory.Buy(1, new(1, 1), default)).Instances.Single();
        var gear = (await inventory.Buy(1, new(4, 1), default)).Instances.Single(i => i.DefinitionId == 4);
        Assert.Equal(3.4572m, gear.TrainingBonusPercent);
        await inventory.Equip(1, sword.Id, default);
        var equipped = await inventory.Equip(1, gear.Id, default);
        Assert.Equal(2, equipped.Instances.Count(i => i.Equipped));
        await inventory.Rename(1, gear.Id, new("Precisely mine"), default);
        var trained = await Training(db).Train(1, new("strength", 1), default);
        Assert.Equal(.012932150m, trained.StatGain);
        Assert.Equal(3.4572m, trained.Stats.TrainingBonusPercent);
        Assert.Equal(10, trained.XpGained);
        var savedGear = await db.ItemInstances.SingleAsync(i => i.Id == gear.Id);
        Assert.Equal(34572, savedGear.TrainingBonusPpm);
        Assert.Equal("Precisely mine", savedGear.Name);
    }

    [Fact]
    public async Task ReachingStatCapChargesOnlyActualTrainingAndNeverChargesAtCap()
    {
        await using var db = CreateDb();
        var player = await db.Players.SingleAsync();
        player.Strength = ProgressionRules.MaxStatUnits - 1;
        await db.SaveChangesAsync();
        var result = await Training(db).Train(1, new("strength", 72), default);
        Assert.Equal(1, result.EnergySpent);
        Assert.Equal(.000000001m, result.StatGain);
        Assert.Equal(71, result.Stats.Energy);
        Assert.Equal(100m, result.Stats.Strength);
        var again = await Training(db).Train(1, new("strength", 72), default);
        Assert.Equal(0, again.EnergySpent);
        Assert.Equal(0, again.XpGained);
        Assert.Equal(71, again.Stats.Energy);
        Assert.Equal(1, again.Stats.TotalTrainingEnergy);
    }

    [Fact]
    public async Task ConcurrentConsumptionCannotExceedSharedDailyAllowance()
    {
        await using (var setup = CreateDb())
        {
            var player = await setup.Players.SingleAsync();
            player.Energy = 0;
            await setup.SaveChangesAsync();
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = CreateDb();
            try { await new InventoryService(db, writes, clock).Consume(1, new(3, 1), default); return true; }
            catch (ApiException ex) when (ex.Status == 400) { return false; }
        }));
        Assert.Equal(4, results.Count(result => result));
        await using var check = CreateDb();
        var final = await check.Players.SingleAsync();
        Assert.Equal(24, final.EnergyBoostUsed);
        Assert.Equal(24, final.Energy);
        Assert.Equal(996, (await check.InventoryStacks.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task PartialRestorationChargesActualEnergyAndOversizedQuantityChangesNothing()
    {
        await using var db = CreateDb();
        var player = await db.Players.SingleAsync();
        player.Energy = 0;
        player.EnergyBoostUsed = 23;
        await db.SaveChangesAsync();
        var inventory = new InventoryService(db, writes, clock);
        await Assert.ThrowsAsync<ApiException>(() => inventory.Consume(1, new(3, 2), default));
        await inventory.Consume(1, new(3, 1), default);
        var final = await db.Players.SingleAsync();
        Assert.Equal(1, final.Energy);
        Assert.Equal(24, final.EnergyBoostUsed);
        Assert.Equal(999, (await db.InventoryStacks.SingleAsync()).Quantity);
        clock.Now = clock.Now.AddDays(1);
        final.Energy = 0;
        final.EnergyUpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        await inventory.Consume(1, new(3, 1), default);
        Assert.Equal(6, final.EnergyBoostUsed);
        Assert.Equal(6, final.Energy);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        return Task.CompletedTask;
    }

    private sealed class MutableClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class FixedRandom : ITrainingRandom { public decimal NextMultiplier() => 1m; }
}
