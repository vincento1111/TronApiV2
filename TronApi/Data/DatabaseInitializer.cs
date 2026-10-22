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
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        await SchemaUpgrader.Initialize(db, clock.GetUtcNow().UtcDateTime,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseUpgrade"));
        var writes = scope.ServiceProvider.GetRequiredService<WriteCoordinator>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Player>>();
        await writes.Execute(db, async () =>
        {
            var definitions = new ItemDefinition[]
            {
                new() { Id = 1, Name = "Iron Sword", Description = "A dependable blade. Every sword has its own permanent power roll.", Kind = "weapon", Tracking = "instance", Price = 150, BasePower = 10, EquipmentSlot = "weapon" },
                new() { Id = 2, Name = "Steel Sword", Description = "A stronger blade forged for the next chapter of your story.", Kind = "weapon", Tracking = "instance", Price = 400, BasePower = 24, EquipmentSlot = "weapon" },
                new() { Id = 3, Name = "Soda", Description = "Restore up to 6 energy, within your shared 24-energy daily consumable allowance.", Kind = "consumable", Tracking = "stack", Price = 10, EnergyRestore = 6 },
                new() { Id = 4, Name = "Focus Band", Description = "A personal training aid with a permanent bonus roll.", Kind = "training", Tracking = "instance", Price = 250, EquipmentSlot = "training", BaseTrainingBonusPpm = 10000, TrainingBonusRollRangePpm = 1000 },
                new() { Id = 5, Name = "Weighted Vest", Description = "Extra resistance for every training session. Shares the training slot.", Kind = "training", Tracking = "instance", Price = 750, EquipmentSlot = "training", BaseTrainingBonusPpm = 20000, TrainingBonusRollRangePpm = 2000 },
                new() { Id = 6, Name = "Precision Rig", Description = "A finely tuned training rig. Its exact bonus stays with this individual item.", Kind = "training", Tracking = "instance", Price = 1800, EquipmentSlot = "training", BaseTrainingBonusPpm = 35000, TrainingBonusRollRangePpm = 5000 },
                new() { Id = 7, Name = "Coffee", Description = "Restore up to 3 energy, within the shared daily consumable allowance.", Kind = "consumable", Tracking = "stack", Price = 6, EnergyRestore = 3 },
                new() { Id = 8, Name = "Trail Mix", Description = "Restore up to 12 energy, within the shared daily consumable allowance.", Kind = "consumable", Tracking = "stack", Price = 25, EnergyRestore = 12 }
            };
            foreach (var definition in definitions)
                if (!await db.ItemDefinitions.AnyAsync(d => d.Id == definition.Id)) db.ItemDefinitions.Add(definition);
            await db.SaveChangesAsync();
            if (seedDemo)
            {
                foreach (var name in new[] { "Vince", "Nova" })
                {
                    var email = $"{name.ToLowerInvariant()}@tron.local";
                    if (await db.Players.AnyAsync(p => p.NormalizedEmail == email.ToUpperInvariant())) continue;
                    var now = clock.GetUtcNow().UtcDateTime;
                    var player = new Player { Email = email, NormalizedEmail = email.ToUpperInvariant(),
                        DisplayName = name, NormalizedDisplayName = name.ToUpperInvariant(), CreatedAt = now,
                        EnergyUpdatedAt = now, EnergyBoostDay = now.Date, Money = 2500,
                        Description = $"A new arrival in Tron City. This is {name}'s local demo profile." };
                    player.PasswordHash = passwords.HashPassword(player, "TronLocal!2026");
                    db.Players.Add(player);
                    await db.SaveChangesAsync();
                    db.InventoryStacks.Add(new() { OwnerId = player.Id, DefinitionId = 3, Quantity = 1000 });
                    db.ItemInstances.AddRange(
                        new() { OwnerId = player.Id, DefinitionId = 1, Name = "First Light", Power = 12, Equipped = true, EquipmentSlot = "weapon" },
                        new() { OwnerId = player.Id, DefinitionId = 1, Name = "Night Runner", Power = 15, EquipmentSlot = "weapon" }
                    );
                }
            }
            return true;
        });
    }
}
