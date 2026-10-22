using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TronApi.Services;

namespace TronApi.Data;

public static class DatabaseInitializer
{
    public static async Task Initialize(IServiceProvider services, bool seedDemo)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        // This is a new local schema; the retired Azure schema is deliberately never connected.
        await db.Database.EnsureCreatedAsync();
        var writes = scope.ServiceProvider.GetRequiredService<WriteCoordinator>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Player>>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        await writes.Execute(db, async () =>
        {
            if (!await db.ItemDefinitions.AnyAsync())
            {
                db.ItemDefinitions.AddRange(
                    new() { Id = 1, Name = "Iron Sword", Description = "A dependable blade. Every sword has its own permanent power roll.", Kind = "weapon", Tracking = "instance", Price = 150, BasePower = 10 },
                    new() { Id = 2, Name = "Steel Sword", Description = "A stronger blade forged for the next chapter of your story.", Kind = "weapon", Tracking = "instance", Price = 400, BasePower = 24 },
                    new() { Id = 3, Name = "Soda", Description = "Restore 6 energy. Identical cans share one inventory stack.", Kind = "consumable", Tracking = "stack", Price = 10, EnergyRestore = 6 }
                );
                await db.SaveChangesAsync();
            }
            if (seedDemo)
            {
                foreach (var name in new[] { "Vince", "Nova" })
                {
                    var email = $"{name.ToLowerInvariant()}@tron.local";
                    if (await db.Players.AnyAsync(p => p.NormalizedEmail == email.ToUpperInvariant())) continue;
                    var now = clock.GetUtcNow().UtcDateTime;
                    var player = new Player { Email = email, NormalizedEmail = email.ToUpperInvariant(),
                        DisplayName = name, NormalizedDisplayName = name.ToUpperInvariant(), CreatedAt = now,
                        EnergyUpdatedAt = now, Money = 2500, Description = $"A new arrival in Tron City. This is {name}'s local demo profile." };
                    player.PasswordHash = passwords.HashPassword(player, "TronLocal!2026");
                    db.Players.Add(player);
                    await db.SaveChangesAsync();
                    db.InventoryStacks.Add(new() { OwnerId = player.Id, DefinitionId = 3, Quantity = 1000 });
                    db.ItemInstances.AddRange(
                        new() { OwnerId = player.Id, DefinitionId = 1, Name = "First Light", Power = 12, Equipped = true },
                        new() { OwnerId = player.Id, DefinitionId = 1, Name = "Night Runner", Power = 15 }
                    );
                }
            }
            return true;
        });
    }
}
