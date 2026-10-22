using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TronApi.Contracts;
using TronApi.Data;
using TronApi.Infrastructure;

namespace TronApi.Services;

public sealed class AccountService(GameDb db, WriteCoordinator writes, IPasswordHasher<Player> passwords, TimeProvider clock, PlayerStatsService stats)
{
    public Task<MeDto> Register(RegisterRequest request, CancellationToken ct) => writes.Execute(db, async () =>
    {
        var email = Input.Email(request.Email);
        var name = Input.DisplayName(request.DisplayName);
        var password = Input.Password(request.Password);
        var normalizedEmail = email.ToUpperInvariant();
        var normalizedName = name.ToUpperInvariant();
        if (await db.Players.AnyAsync(p => p.NormalizedEmail == normalizedEmail, ct))
            throw new ApiException(409, "An account already uses that email.");
        if (await db.Players.AnyAsync(p => p.NormalizedDisplayName == normalizedName, ct))
            throw new ApiException(409, "That display name is already taken.");
        var now = clock.GetUtcNow().UtcDateTime;
        var player = new Player
        {
            Email = email, NormalizedEmail = normalizedEmail, DisplayName = name,
            NormalizedDisplayName = normalizedName, CreatedAt = now, EnergyUpdatedAt = now, EnergyBoostDay = now.Date
        };
        player.PasswordHash = passwords.HashPassword(player, password);
        db.Players.Add(player);
        await db.SaveChangesAsync(ct);
        return Dto.Me(player);
    }, ct);

    public Task<MeDto> Login(LoginRequest request, CancellationToken ct) => writes.Execute(db, async () =>
    {
        var normalizedEmail = (request.Email?.Trim() ?? "").ToUpperInvariant();
        if (normalizedEmail.Length > 254 || request.Password is null || request.Password.Length > 128)
            throw new ApiException(401, "Email or password is incorrect.");
        var player = await db.Players.SingleOrDefaultAsync(p => p.NormalizedEmail == normalizedEmail, ct);
        if (player is null || passwords.VerifyHashedPassword(player, player.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw new ApiException(401, "Email or password is incorrect.");
        GameRules.Regenerate(player, clock.GetUtcNow().UtcDateTime);
        ProgressionRules.RefreshEnergyBudget(player, clock.GetUtcNow().UtcDateTime);
        return Dto.Me(player, await stats.TrainingBonus(player.Id, ct));
    }, ct);

    public Task<MeDto> Me(int id, CancellationToken ct) => writes.Execute(db, async () =>
    {
        var player = await Find(id, ct);
        GameRules.Regenerate(player, clock.GetUtcNow().UtcDateTime);
        ProgressionRules.RefreshEnergyBudget(player, clock.GetUtcNow().UtcDateTime);
        return Dto.Me(player, await stats.TrainingBonus(player.Id, ct));
    }, ct);

    public Task<MeDto> Update(int id, ProfileRequest request, CancellationToken ct) => writes.Execute(db, async () =>
    {
        var player = await Find(id, ct);
        var name = Input.DisplayName(request.DisplayName);
        var normalized = name.ToUpperInvariant();
        if (await db.Players.AnyAsync(p => p.Id != id && p.NormalizedDisplayName == normalized, ct))
            throw new ApiException(409, "That display name is already taken.");
        player.DisplayName = name;
        player.NormalizedDisplayName = normalized;
        player.Description = Input.Text(request.Description, "Description", 0, 2000);
        GameRules.Regenerate(player, clock.GetUtcNow().UtcDateTime);
        ProgressionRules.RefreshEnergyBudget(player, clock.GetUtcNow().UtcDateTime);
        return Dto.Me(player, await stats.TrainingBonus(player.Id, ct));
    }, ct);

    public async Task<IReadOnlyList<PlayerDto>> Search(string? search, CancellationToken ct)
    {
        var query = (search?.Trim() ?? "").ToUpperInvariant();
        if (query.Length > 100) throw ApiException.BadRequest("Search is too long.");
        return await db.Players.AsNoTracking().Where(p => p.NormalizedDisplayName.Contains(query))
            .OrderBy(p => p.DisplayName).Take(50).Select(p => new PlayerDto(p.Id, p.DisplayName, p.Description, p.CreatedAt, p.Level)).ToListAsync(ct);
    }

    public async Task<PlayerDto> PublicProfile(int id, CancellationToken ct) =>
        Dto.PublicPlayer(await db.Players.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ApiException.NotFound("Player not found."));

    private async Task<Player> Find(int id, CancellationToken ct) =>
        await db.Players.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new ApiException(401, "Please sign in again.");
}
