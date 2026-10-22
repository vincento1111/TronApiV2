using Microsoft.EntityFrameworkCore;

namespace TronApi.Data;

public sealed class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<ItemDefinition> ItemDefinitions => Set<ItemDefinition>();
    public DbSet<ItemInstance> ItemInstances => Set<ItemInstance>();
    public DbSet<InventoryStack> InventoryStacks => Set<InventoryStack>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PlayerSession>(s =>
        {
            s.Property(x => x.Id).HasMaxLength(64);
            s.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
            s.HasIndex(x => x.ExpiresAt);
        });
        model.Entity<Player>(p =>
        {
            p.HasIndex(x => x.NormalizedEmail).IsUnique();
            p.HasIndex(x => x.NormalizedDisplayName).IsUnique();
            p.Property(x => x.Email).HasMaxLength(254);
            p.Property(x => x.DisplayName).HasMaxLength(24);
            p.Property(x => x.Description).HasMaxLength(2000);
            p.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Player_Balances", "Money >= 0 AND Energy >= 0 AND Energy <= 72");
                t.HasCheckConstraint("CK_Player_Stats", "Strength BETWEEN 1000000000 AND 100000000000 AND Defense BETWEEN 1000000000 AND 100000000000 AND Speed BETWEEN 1000000000 AND 100000000000 AND Dexterity BETWEEN 1000000000 AND 100000000000");
                t.HasCheckConstraint("CK_Player_Progression", "Experience BETWEEN 0 AND 1629982 AND TotalTrainingEnergy >= 0 AND EnergyBoostUsed BETWEEN 0 AND 24 AND Level BETWEEN 1 AND 100 AND Health BETWEEN 0 AND 595");
            });
        });
        model.Entity<ItemDefinition>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Definition_Values", "Price >= 0 AND BasePower >= 0 AND EnergyRestore >= 0");
            t.HasCheckConstraint("CK_Definition_Tracking", "Tracking IN ('instance','stack')");
            t.HasCheckConstraint("CK_Definition_Training", "BaseTrainingBonusPpm >= 0 AND TrainingBonusRollRangePpm >= 0 AND (EquipmentSlot IS NULL OR EquipmentSlot IN ('weapon','training'))");
        });
        model.Entity<ItemInstance>(i =>
        {
            i.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            i.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
            i.HasIndex(x => new { x.OwnerId, x.EquipmentSlot }).IsUnique().HasFilter("Equipped = 1");
            i.Property(x => x.Name).HasMaxLength(48);
            i.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Instance_Power", "Power >= 0");
                t.HasCheckConstraint("CK_Instance_Training", "TrainingBonusPpm >= 0 AND (EquipmentSlot IS NULL OR EquipmentSlot IN ('weapon','training')) AND (Equipped = 0 OR EquipmentSlot IS NOT NULL)");
            });
        });
        model.Entity<InventoryStack>(s =>
        {
            s.HasIndex(x => new { x.OwnerId, x.DefinitionId }).IsUnique();
            s.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            s.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
            s.ToTable(t => t.HasCheckConstraint("CK_Stack_Quantity", "Quantity > 0"));
        });
        model.Entity<ChatMessage>(m =>
        {
            m.HasIndex(x => new { x.SenderId, x.ClientMessageId }).IsUnique();
            m.HasIndex(x => new { x.RecipientId, x.Id });
            m.HasIndex(x => new { x.SenderId, x.RecipientId, x.Id });
            m.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
            m.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
            m.Property(x => x.Content).HasMaxLength(2000);
            m.Property(x => x.ClientMessageId).HasMaxLength(36);
        });
        // Store UTC ticks explicitly, avoiding SQLite's unspecified DateTime kind on reads.
        foreach (var property in model.Model.GetEntityTypes().SelectMany(e => e.GetProperties()).Where(p => p.ClrType == typeof(DateTime)))
            property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, long>(
                value => value.Ticks, value => new DateTime(value, DateTimeKind.Utc)));
    }
}
