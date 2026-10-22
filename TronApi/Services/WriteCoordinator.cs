using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TronApi.Data;

namespace TronApi.Services;

/// <summary>Serialize complete read/check/write operations, not merely individual SQL statements.</summary>
public sealed class WriteCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<T> Execute<T>(GameDb db, Func<Task<T>> operation, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            // BEGIN IMMEDIATE also protects against a second process using the same SQLite file.
            await using var transaction = connection.BeginTransaction(deferred: false);
            await using var enlistment = await db.Database.UseTransactionAsync(transaction, ct);
            try
            {
                var result = await operation();
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
        finally { gate.Release(); }
    }
}
