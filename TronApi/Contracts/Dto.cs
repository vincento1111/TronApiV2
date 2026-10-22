using TronApi.Data;
using TronApi.Services;

namespace TronApi.Contracts;

public sealed record StatsDto(int Strength, int Defense, int Speed, int Dexterity, int Energy, int MaxEnergy,
    DateTime? NextEnergyAt, int Money, int Level, int Health, int MaxHealth);
public sealed record MeDto(int Id, string DisplayName, string Email, string Description, DateTime CreatedAt, StatsDto Stats);
public sealed record PlayerDto(int Id, string DisplayName, string Description, DateTime CreatedAt, int Level);
public sealed record DefinitionDto(int Id, string Name, string Description, string Kind, string Tracking, int Price, int BasePower, int EnergyRestore);
public sealed record InstanceDto(int Id, int DefinitionId, string Name, string DefinitionName, int Power, bool Equipped);
public sealed record StackDto(int DefinitionId, string Name, int Quantity, int EnergyRestore);
public sealed record InventoryDto(IReadOnlyList<InstanceDto> Instances, IReadOnlyList<StackDto> Stacks);
public sealed record MessageDto(int Id, int SenderId, string SenderName, int? RecipientId, string Content, DateTime CreatedAt, string ClientMessageId);
public sealed record MessagePage(IReadOnlyList<MessageDto> Messages, int Cursor, bool HasMore);
public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record ProfileRequest(string? DisplayName, string? Description);
public sealed record TrainingRequest(string? Stat, int Amount);
public sealed record QuantityRequest(int DefinitionId, int Quantity);
public sealed record RenameRequest(string? Name);
public sealed record SellRequest(int? InstanceId, int? DefinitionId, int? Quantity);
public sealed record SendMessageRequest(string? Content, string? ClientMessageId, int? RecipientId);

public static class Dto
{
    public static StatsDto Stats(Player p) => new(p.Strength, p.Defense, p.Speed, p.Dexterity, p.Energy,
        GameRules.MaxEnergy, p.Energy < GameRules.MaxEnergy ? p.EnergyUpdatedAt.Add(GameRules.EnergyInterval) : null,
        p.Money, p.Level, p.Health, 100);
    public static MeDto Me(Player p) => new(p.Id, p.DisplayName, p.Email, p.Description, p.CreatedAt, Stats(p));
    public static PlayerDto PublicPlayer(Player p) => new(p.Id, p.DisplayName, p.Description, p.CreatedAt, p.Level);
    public static DefinitionDto Definition(ItemDefinition d) => new(d.Id, d.Name, d.Description, d.Kind, d.Tracking, d.Price, d.BasePower, d.EnergyRestore);
    public static InstanceDto Instance(ItemInstance i) => new(i.Id, i.DefinitionId, i.Name, i.Definition.Name, i.Power, i.Equipped);
    public static MessageDto Message(ChatMessage m) => new(m.Id, m.SenderId, m.Sender.DisplayName, m.RecipientId, m.Content, m.CreatedAt, m.ClientMessageId);
}
