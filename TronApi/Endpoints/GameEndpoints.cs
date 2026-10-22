using TronApi.Contracts;
using TronApi.Infrastructure;
using TronApi.Services;

namespace TronApi.Endpoints;

public static class GameEndpoints
{
    public static void MapGame(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/players", (string? search, AccountService accounts, CancellationToken ct) => accounts.Search(search, ct));
        api.MapGet("/players/{id:int}", (int id, AccountService accounts, CancellationToken ct) => accounts.PublicProfile(id, ct));
        api.MapPut("/players/me", (ProfileRequest request, HttpContext context, AccountService accounts, CancellationToken ct) =>
            accounts.Update(context.User.PlayerId(), request, ct));
        api.MapPost("/game/train", (TrainingRequest request, HttpContext context, TrainingService game, CancellationToken ct) =>
            game.Train(context.User.PlayerId(), request, ct));
        api.MapGet("/items", (InventoryService inventory, CancellationToken ct) => inventory.Definitions(ct));
        api.MapGet("/inventory", (HttpContext context, InventoryService inventory, CancellationToken ct) => inventory.Read(context.User.PlayerId(), ct));
        api.MapPost("/inventory/buy", (QuantityRequest request, HttpContext context, InventoryService inventory, CancellationToken ct) =>
            inventory.Buy(context.User.PlayerId(), request, ct));
        api.MapPost("/inventory/consume", (QuantityRequest request, HttpContext context, InventoryService inventory, CancellationToken ct) =>
            inventory.Consume(context.User.PlayerId(), request, ct));
        api.MapPut("/inventory/instances/{id:int}", (int id, RenameRequest request, HttpContext context, InventoryService inventory, CancellationToken ct) =>
            inventory.Rename(context.User.PlayerId(), id, request, ct));
        api.MapPost("/inventory/instances/{id:int}/equip", (int id, HttpContext context, InventoryService inventory, CancellationToken ct) =>
            inventory.Equip(context.User.PlayerId(), id, ct));
        api.MapPost("/inventory/sell", (SellRequest request, HttpContext context, InventoryService inventory, CancellationToken ct) =>
            inventory.Sell(context.User.PlayerId(), request, ct));
    }
}
