using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TronApi.Contracts;
using TronApi.Data;
using TronApi.Infrastructure;
using TronApi.Services;
using Xunit;

namespace TronApi.Tests;

public sealed class InventoryTransactionTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"tron-invariants-{Guid.NewGuid():N}.db");
    private readonly WriteCoordinator writes = new();
    private int playerId;
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly TimeProvider clock = new FixedClock(Now);

    private GameDb CreateDb() => new(new DbContextOptionsBuilder<GameDb>().UseSqlite($"Data Source={path};Pooling=False").Options);

    public async Task InitializeAsync()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var player = new Player { DisplayName = "Tester", NormalizedDisplayName = "TESTER", Email = "test@tron.local",
            NormalizedEmail = "TEST@TRON.LOCAL", PasswordHash = "not-used", CreatedAt = Now, EnergyUpdatedAt = Now };
        db.Players.Add(player);
        db.ItemDefinitions.AddRange(
            new() { Id = 1, Name = "Sword", Kind = "weapon", Tracking = "instance", Price = 150, BasePower = 10 },
            new() { Id = 3, Name = "Soda", Kind = "consumable", Tracking = "stack", Price = 10, EnergyRestore = 6 });
        await db.SaveChangesAsync();
        playerId = player.Id;
    }

    [Fact]
    public async Task ParallelPurchasesCannotSpendTheSameBalanceTwice()
    {
        var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var db = CreateDb();
            try { await new InventoryService(db, writes, clock).Buy(playerId, new(1, 1), default); return true; }
            catch (ApiException ex) when (ex.Status == 400) { return false; }
        }));
        Assert.Equal(6, attempts.Count(success => success));
        await using var check = CreateDb();
        Assert.Equal(100, (await check.Players.SingleAsync()).Money);
        Assert.Equal(6, await check.ItemInstances.CountAsync());
    }

    [Fact]
    public async Task ParallelTrainingCannotSpendTheSameEnergyTwice()
    {
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = CreateDb();
            try { await new TrainingService(db, writes, clock).Train(playerId, new("strength", 50), default); return true; }
            catch (ApiException ex) when (ex.Status == 400) { return false; }
        }));
        Assert.Single(attempts, success => success);
        await using var check = CreateDb();
        var player = await check.Players.SingleAsync();
        Assert.Equal(22, player.Energy);
        Assert.Equal(60, player.Strength);
    }

    [Fact]
    public async Task ThousandSodasUseOneRowAndEmptyStackIsRemoved()
    {
        await using var db = CreateDb();
        var player = await db.Players.SingleAsync();
        player.Money = 20000;
        await db.SaveChangesAsync();
        var inventory = new InventoryService(db, writes, clock);
        var bought = await inventory.Buy(playerId, new(3, 1000), default);
        Assert.Equal(1000, Assert.Single(bought.Stacks).Quantity);
        Assert.Equal(1, await db.InventoryStacks.CountAsync());
        Assert.Empty(bought.Instances);
        var sold = await inventory.Sell(playerId, new(null, 3, 1000), default);
        Assert.Empty(sold.Stacks);
        Assert.Equal(0, await db.InventoryStacks.CountAsync());
        Assert.Equal(15000, (await db.Players.SingleAsync()).Money);
    }

    [Fact]
    public async Task EquippingSecondWeaponReplacesFirstWithoutChangingEitherRoll()
    {
        await using var db = CreateDb();
        var inventory = new InventoryService(db, writes, clock);
        var owned = await inventory.Buy(playerId, new(1, 2), default);
        var first = owned.Instances[0];
        var second = owned.Instances[1];
        await inventory.Equip(playerId, first.Id, default);
        var equipped = await inventory.Equip(playerId, second.Id, default);
        Assert.Equal(second.Id, Assert.Single(equipped.Instances, i => i.Equipped).Id);
        Assert.Equal(first.Power, equipped.Instances.Single(i => i.Id == first.Id).Power);
        Assert.Equal(second.Power, equipped.Instances.Single(i => i.Id == second.Id).Power);
        var renamed = await inventory.Rename(playerId, second.Id, new("A personal name"), default);
        Assert.Equal(second.Power, renamed.Power);
        Assert.Equal(second.DefinitionId, renamed.DefinitionId);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateStackAndNegativeBalance()
    {
        await using var db = CreateDb();
        db.InventoryStacks.AddRange(new() { OwnerId = playerId, DefinitionId = 3, Quantity = 1 },
            new() { OwnerId = playerId, DefinitionId = 3, Quantity = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var player = await db.Players.SingleAsync();
        player.Money = -1;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(path + suffix)) File.Delete(path + suffix);
        return Task.CompletedTask;
    }

    private sealed class FixedClock(DateTime time) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(time);
    }
}
