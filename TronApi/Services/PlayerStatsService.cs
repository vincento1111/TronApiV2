using Microsoft.EntityFrameworkCore;
using TronApi.Data;

namespace TronApi.Services;

public sealed class PlayerStatsService(GameDb db)
{
    public Task<int> TrainingBonus(int ownerId, CancellationToken ct) =>
        db.ItemInstances.Where(i => i.OwnerId == ownerId && i.Equipped && i.EquipmentSlot == "training")
            .Select(i => i.TrainingBonusPpm).SingleOrDefaultAsync(ct);
}
