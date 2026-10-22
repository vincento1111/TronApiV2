using Microsoft.EntityFrameworkCore;
using TronApi.Contracts;
using TronApi.Data;
using TronApi.Infrastructure;

namespace TronApi.Services;

public sealed class InventoryService(GameDb db, WriteCoordinator writes, TimeProvider clock)
{
    public async Task<IReadOnlyList<DefinitionDto>> Definitions(CancellationToken ct) =>
        (await db.ItemDefinitions.AsNoTracking().OrderBy(d => d.Id).ToListAsync(ct)).Select(Dto.Definition).ToList();

    public async Task<InventoryDto> Read(int ownerId, CancellationToken ct)
    {
        var instances = await db.ItemInstances.AsNoTracking().Include(i => i.Definition).Where(i => i.OwnerId == ownerId).OrderBy(i => i.Id).ToListAsync(ct);
        var stacks = await db.InventoryStacks.AsNoTracking().Include(s => s.Definition).Where(s => s.OwnerId == ownerId).OrderBy(s => s.DefinitionId).ToListAsync(ct);
        return new(instances.Select(Dto.Instance).ToList(),
            stacks.Select(s => new StackDto(s.DefinitionId, s.Definition.Name, s.Quantity, s.Definition.EnergyRestore)).ToList());
    }

    public async Task<InventoryDto> Buy(int ownerId, QuantityRequest request, CancellationToken ct)
    {
        await writes.Execute(db, async () =>
        {
            var definition = await Definition(request.DefinitionId, ct);
            var quantity = Input.Quantity(request.Quantity, definition.Tracking == "instance" ? 20 : 1000);
            var player = await Player(ownerId, ct);
            GameRules.Regenerate(player, clock.GetUtcNow().UtcDateTime);
            GameRules.AddMoney(player, -(long)definition.Price * quantity);
            if (definition.Tracking == "stack")
            {
                var stack = await db.InventoryStacks.SingleOrDefaultAsync(s => s.OwnerId == ownerId && s.DefinitionId == definition.Id, ct);
                if (stack is null) db.InventoryStacks.Add(new() { OwnerId = ownerId, DefinitionId = definition.Id, Quantity = quantity });
                else
                {
                    if ((long)stack.Quantity + quantity > int.MaxValue) throw ApiException.BadRequest("This stack is full.");
                    stack.Quantity += quantity;
                }
            }
            else
                for (var n = 0; n < quantity; n++)
                    db.ItemInstances.Add(new() { OwnerId = ownerId, DefinitionId = definition.Id, Name = definition.Name,
                        Power = definition.Kind == "weapon" ? GameRules.RollPower(definition) : 0,
                        EquipmentSlot = definition.EquipmentSlot, TrainingBonusPpm = GameRules.RollTrainingBonus(definition) });
            return true;
        }, ct);
        return await Read(ownerId, ct);
    }

    public async Task<InventoryDto> Consume(int ownerId, QuantityRequest request, CancellationToken ct)
    {
        await writes.Execute(db, async () =>
        {
            var quantity = Input.Quantity(request.Quantity);
            var stack = await db.InventoryStacks.Include(s => s.Definition).SingleOrDefaultAsync(s => s.OwnerId == ownerId && s.DefinitionId == request.DefinitionId, ct)
                ?? throw ApiException.NotFound("You do not own that consumable.");
            if (stack.Quantity < quantity) throw ApiException.BadRequest("Not enough items in this stack.");
            if (stack.Definition.Kind != "consumable" || stack.Definition.EnergyRestore <= 0)
                throw ApiException.BadRequest("This item cannot be consumed.");
            var player = await Player(ownerId, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            GameRules.Regenerate(player, now);
            ProgressionRules.RefreshEnergyBudget(player, now);
            if (player.Energy == GameRules.MaxEnergy) throw ApiException.BadRequest("Your energy is already full.");
            var available = Math.Min(GameRules.MaxEnergy - player.Energy, ProgressionRules.DailyEnergyBoost - player.EnergyBoostUsed);
            if (available == 0) throw ApiException.BadRequest("Your 24-energy daily consumable allowance resets at midnight UTC.");
            var maximumUseful = (available + stack.Definition.EnergyRestore - 1) / stack.Definition.EnergyRestore;
            if (quantity > maximumUseful) throw ApiException.BadRequest($"Only {maximumUseful} would help right now. Choose a smaller quantity.");
            var restored = (int)Math.Min(available, (long)stack.Definition.EnergyRestore * quantity);
            player.Energy += restored;
            player.EnergyBoostUsed += restored;
            if (player.Energy == GameRules.MaxEnergy) player.EnergyUpdatedAt = now;
            stack.Quantity -= quantity;
            if (stack.Quantity == 0) db.InventoryStacks.Remove(stack);
            return true;
        }, ct);
        return await Read(ownerId, ct);
    }

    public Task<InstanceDto> Rename(int ownerId, int instanceId, RenameRequest request, CancellationToken ct) => writes.Execute(db, async () =>
    {
        var instance = await OwnedInstance(ownerId, instanceId, ct);
        instance.Name = Input.Text(request.Name, "Item name", 1, 48);
        return Dto.Instance(instance);
    }, ct);

    public async Task<InventoryDto> Equip(int ownerId, int instanceId, CancellationToken ct)
    {
        await writes.Execute(db, async () =>
        {
            var instance = await OwnedInstance(ownerId, instanceId, ct);
            if (instance.EquipmentSlot is not ("weapon" or "training")) throw ApiException.BadRequest("This item has no equipment slot.");
            if (instance.Equipped) instance.Equipped = false;
            else
            {
                // Persist the removal before setting the replacement to satisfy the unique equipped index.
                var previous = await db.ItemInstances.Where(i => i.OwnerId == ownerId && i.Equipped && i.EquipmentSlot == instance.EquipmentSlot).ToListAsync(ct);
                foreach (var item in previous) item.Equipped = false;
                await db.SaveChangesAsync(ct);
                instance.Equipped = true;
            }
            return true;
        }, ct);
        return await Read(ownerId, ct);
    }

    public async Task<InventoryDto> Sell(int ownerId, SellRequest request, CancellationToken ct)
    {
        if (request.InstanceId.HasValue == request.DefinitionId.HasValue)
            throw ApiException.BadRequest("Select either an individual item or a stack.");
        await writes.Execute(db, async () =>
        {
            var player = await Player(ownerId, ct);
            if (request.InstanceId is int instanceId)
            {
                if (request.Quantity is not null && request.Quantity != 1) throw ApiException.BadRequest("Sell one individual item at a time.");
                var item = await OwnedInstance(ownerId, instanceId, ct);
                GameRules.AddMoney(player, item.Definition.Price / 2);
                db.ItemInstances.Remove(item);
            }
            else
            {
                var quantity = Input.Quantity(request.Quantity ?? 1);
                var stack = await db.InventoryStacks.Include(s => s.Definition).SingleOrDefaultAsync(s => s.OwnerId == ownerId && s.DefinitionId == request.DefinitionId, ct)
                    ?? throw ApiException.NotFound("You do not own that stack.");
                if (stack.Quantity < quantity) throw ApiException.BadRequest("Not enough items in this stack.");
                GameRules.AddMoney(player, (long)(stack.Definition.Price / 2) * quantity);
                stack.Quantity -= quantity;
                if (stack.Quantity == 0) db.InventoryStacks.Remove(stack);
            }
            return true;
        }, ct);
        return await Read(ownerId, ct);
    }

    private async Task<Player> Player(int id, CancellationToken ct) =>
        await db.Players.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new ApiException(401, "Please sign in again.");
    private async Task<ItemDefinition> Definition(int id, CancellationToken ct) =>
        await db.ItemDefinitions.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw ApiException.NotFound("Item type not found.");
    private async Task<ItemInstance> OwnedInstance(int ownerId, int id, CancellationToken ct) =>
        await db.ItemInstances.Include(i => i.Definition).SingleOrDefaultAsync(i => i.Id == id && i.OwnerId == ownerId, ct)
            ?? throw ApiException.NotFound("You do not own that item.");
}
