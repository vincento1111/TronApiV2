namespace TronApi.Data;

public sealed class Player
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string NormalizedDisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public long Strength { get; set; } = 1_000_000_000L;
    public long Defense { get; set; } = 1_000_000_000L;
    public long Speed { get; set; } = 1_000_000_000L;
    public long Dexterity { get; set; } = 1_000_000_000L;
    public int Energy { get; set; } = 72;
    public DateTime EnergyUpdatedAt { get; set; }
    public int Money { get; set; } = 1000;
    public int Level { get; set; } = 1;
    public int Health { get; set; } = 100;
    public long Experience { get; set; }
    public long TotalTrainingEnergy { get; set; }
    public int EnergyBoostUsed { get; set; }
    public DateTime EnergyBoostDay { get; set; }
}

public sealed class ItemDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Tracking { get; set; } = "";
    public int Price { get; set; }
    public int BasePower { get; set; }
    public int EnergyRestore { get; set; }
    public string? EquipmentSlot { get; set; }
    public int BaseTrainingBonusPpm { get; set; }
    public int TrainingBonusRollRangePpm { get; set; }
}

public sealed class ItemInstance
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public Player Owner { get; set; } = null!;
    public int DefinitionId { get; set; }
    public ItemDefinition Definition { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Power { get; set; }
    public bool Equipped { get; set; }
    public string? EquipmentSlot { get; set; }
    public int TrainingBonusPpm { get; set; }
}

public sealed class InventoryStack
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public Player Owner { get; set; } = null!;
    public int DefinitionId { get; set; }
    public ItemDefinition Definition { get; set; } = null!;
    public int Quantity { get; set; }
}

public sealed class ChatMessage
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public Player Sender { get; set; } = null!;
    public int? RecipientId { get; set; }
    public Player? Recipient { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string ClientMessageId { get; set; } = "";
}

public sealed class PlayerSession
{
    public string Id { get; set; } = "";
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
