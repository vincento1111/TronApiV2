using TronApi.Data;
using TronApi.Services;

namespace TronApi.Contracts;

public sealed record StatsDto(decimal Strength, decimal Defense, decimal Speed, decimal Dexterity, int Energy, int MaxEnergy,
    DateTime? NextEnergyAt, int Money, int Level, int Health, int MaxHealth, long Experience, long XpIntoLevel,
    long XpToNextLevel, long TotalTrainingEnergy, decimal TrainingMasteryPercent, decimal TrainingBonusPercent,
    int EnergyBoostRemaining, DateTime EnergyBoostResetAt);
public sealed record TrainingResultDto(StatsDto Stats, string Stat, int EnergySpent, decimal StatGain, long XpGained, int LevelsGained);
public sealed record MeDto(int Id, string DisplayName, string Email, string Description, DateTime CreatedAt, StatsDto Stats);
public sealed record PlayerDto(int Id, string DisplayName, string Description, DateTime CreatedAt, int Level);
public sealed record DefinitionDto(int Id, string Name, string Description, string Kind, string Tracking, int Price, int BasePower, int EnergyRestore,
    string? EquipmentSlot, decimal TrainingBonusPercent, decimal TrainingBonusMinPercent, decimal TrainingBonusMaxPercent);
public sealed record InstanceDto(int Id, int DefinitionId, string Name, string DefinitionName, int Power, bool Equipped,
    string? EquipmentSlot, decimal TrainingBonusPercent);
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
    public static StatsDto Stats(Player p, int trainingBonusPpm = 0) => new(
        ProgressionRules.Stat(p.Strength), ProgressionRules.Stat(p.Defense), ProgressionRules.Stat(p.Speed), ProgressionRules.Stat(p.Dexterity),
        p.Energy, GameRules.MaxEnergy, p.Energy < GameRules.MaxEnergy ? p.EnergyUpdatedAt.Add(GameRules.EnergyInterval) : null,
        p.Money, p.Level, p.Health, ProgressionRules.MaxHealth(p.Level), p.Experience,
        p.Experience - ProgressionRules.ExperienceAtLevel(p.Level), ProgressionRules.ExperienceForNextLevel(p.Level),
        p.TotalTrainingEnergy, (ProgressionRules.MasteryMultiplier(p.TotalTrainingEnergy) - 1m) * 100m,
        ProgressionRules.BonusPercent(trainingBonusPpm), ProgressionRules.DailyEnergyBoost - p.EnergyBoostUsed,
        p.EnergyBoostDay.AddDays(1));
    public static MeDto Me(Player p, int trainingBonusPpm = 0) => new(p.Id, p.DisplayName, p.Email, p.Description, p.CreatedAt, Stats(p, trainingBonusPpm));
    public static PlayerDto PublicPlayer(Player p) => new(p.Id, p.DisplayName, p.Description, p.CreatedAt, p.Level);
    public static DefinitionDto Definition(ItemDefinition d) => new(d.Id, d.Name, d.Description, d.Kind, d.Tracking, d.Price, d.BasePower, d.EnergyRestore,
        d.EquipmentSlot, ProgressionRules.BonusPercent(d.BaseTrainingBonusPpm),
        ProgressionRules.BonusPercent(d.BaseTrainingBonusPpm - d.TrainingBonusRollRangePpm),
        ProgressionRules.BonusPercent(d.BaseTrainingBonusPpm + d.TrainingBonusRollRangePpm));
    public static InstanceDto Instance(ItemInstance i) => new(i.Id, i.DefinitionId, i.Name, i.Definition.Name, i.Power, i.Equipped,
        i.EquipmentSlot, ProgressionRules.BonusPercent(i.TrainingBonusPpm));
    public static MessageDto Message(ChatMessage m) => new(m.Id, m.SenderId, m.Sender.DisplayName, m.RecipientId, m.Content, m.CreatedAt, m.ClientMessageId);
}
