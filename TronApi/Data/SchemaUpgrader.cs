using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TronApi.Services;

namespace TronApi.Data;

/// <summary>Explicit, backed-up upgrades for the first local SQLite save format.</summary>
public static class SchemaUpgrader
{
    public const int CurrentVersion = 2;
    private static readonly string[] Tables = ["Players", "ItemDefinitions", "ItemInstances", "InventoryStacks", "ChatMessages", "PlayerSessions"];

    public static async Task Initialize(GameDb db, DateTime now, ILogger logger)
    {
        await db.Database.OpenConnectionAsync();
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var version = await ScalarLong(connection, "PRAGMA user_version");
        if (version > CurrentVersion || version < 0)
            throw new InvalidOperationException($"Database schema {version} is newer than this application. Use a compatible application; the save was not changed.");
        var tableCount = await ScalarLong(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'");
        if (tableCount == 0)
        {
            if (version != 0) throw new InvalidOperationException("The save has a schema version but no tables. Restore a verified backup.");
            await using var create = connection.BeginTransaction(deferred: false);
            await Execute(connection, db.Database.GenerateCreateScript(), create);
            await AddGuards(connection, create);
            await Execute(connection, $"PRAGMA user_version = {CurrentVersion}", create);
            await create.CommitAsync();
            return;
        }

        foreach (var table in Tables)
            if (!await HasColumn(connection, table, table == "PlayerSessions" ? "ExpiresAt" : "Id"))
                throw new InvalidOperationException($"Unrecognized save schema: required table {table} is missing or incomplete. The save was not changed.");

        if (version == CurrentVersion)
        {
            if (!await HasColumn(connection, "Players", "TotalTrainingEnergy") ||
                !await HasColumn(connection, "ItemInstances", "TrainingBonusPpm"))
                throw new InvalidOperationException("The save's schema version does not match its columns. Restore a verified backup.");
            return;
        }
        if (await HasColumn(connection, "Players", "Experience") || await HasColumn(connection, "ItemInstances", "EquipmentSlot"))
            throw new InvalidOperationException("Unrecognized partially upgraded save. The save was not changed.");
        await VerifyIntegrity(connection);

        var backupPath = await CreateBackup(connection, now);
        logger.LogInformation("Saved pre-upgrade SQLite backup at {BackupPath}", backupPath);
        await using var transaction = connection.BeginTransaction(deferred: false);
        try
        {
            if (await ScalarLong(connection, "PRAGMA user_version", transaction) != version)
                throw new InvalidOperationException("The save changed while preparing its upgrade. Stop other Tron City processes and restart.");
            // Prepare each DDL statement only after its predecessor has changed the schema.
            foreach (var statement in UpgradeColumns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                await Execute(connection, statement, transaction);
            var players = new List<LegacyPlayer>();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT Id, Strength, Defense, Speed, Dexterity, Health FROM Players ORDER BY Id";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    players.Add(new(reader.GetInt32(0), [reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4)], reader.GetInt32(5)));
            }
            foreach (var player in players)
            {
                var raw = new long[4];
                long total = 0;
                for (var index = 0; index < raw.Length; index++)
                {
                    var spent = Math.Max(0L, player.Stats[index] - 10L);
                    raw[index] = ProgressionRules.ConvertLegacyStat(spent, total);
                    total = checked(total + spent);
                }
                var experience = total >= ProgressionRules.MaximumExperience / 10 + 1
                    ? ProgressionRules.MaximumExperience : Math.Min(ProgressionRules.MaximumExperience, total * 10);
                var level = ProgressionRules.LevelForExperience(experience);
                var health = Math.Clamp((long)player.Health + ProgressionRules.MaxHealth(level) - 100, 0, ProgressionRules.MaxHealth(level));
                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE Players SET Strength=$s, Defense=$d, Speed=$sp, Dexterity=$dx,
                    Experience=$xp, TotalTrainingEnergy=$total, Level=$level, Health=$health, EnergyBoostDay=$day
                    WHERE Id=$id
                    """;
                update.Parameters.AddWithValue("$s", raw[0]);
                update.Parameters.AddWithValue("$d", raw[1]);
                update.Parameters.AddWithValue("$sp", raw[2]);
                update.Parameters.AddWithValue("$dx", raw[3]);
                update.Parameters.AddWithValue("$xp", experience);
                update.Parameters.AddWithValue("$total", total);
                update.Parameters.AddWithValue("$level", level);
                update.Parameters.AddWithValue("$health", health);
                update.Parameters.AddWithValue("$day", now.Date.Ticks);
                update.Parameters.AddWithValue("$id", player.Id);
                await update.ExecuteNonQueryAsync();
            }
            await AddGuards(connection, transaction);
            await VerifyIntegrity(connection, transaction);
            await Execute(connection, $"PRAGMA user_version = {CurrentVersion}", transaction);
            await transaction.CommitAsync();
            logger.LogInformation("Upgraded local save to schema {Version}; all accounts, messages and inventory rows were retained.", CurrentVersion);
        }
        catch
        {
            await transaction.RollbackAsync();
            logger.LogError("Save upgrade rolled back. The original snapshot is retained at {BackupPath}.", backupPath);
            throw;
        }
    }

    private static async Task<string> CreateBackup(SqliteConnection source, DateTime now)
    {
        if (source.DataSource == ":memory:" || string.IsNullOrWhiteSpace(source.DataSource))
            throw new InvalidOperationException("An existing save must have a file path so it can be backed up before upgrading.");
        var fullPath = Path.GetFullPath(source.DataSource);
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(fullPath)!, "backups"));
        var path = Path.Combine(directory.FullName, $"{Path.GetFileNameWithoutExtension(fullPath)}.pre-v2.{now:yyyyMMdd-HHmmss}.{Guid.NewGuid():N}.db");
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await destination.OpenAsync();
        // SQLite's backup API includes committed WAL content; copying only the .db file would not.
        source.BackupDatabase(destination);
        await VerifyIntegrity(destination);
        return path;
    }

    private static async Task<bool> HasColumn(SqliteConnection connection, string table, string column)
    {
        // Table names come only from the fixed schema list above.
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) if (reader.GetString(1) == column) return true;
        return false;
    }

    private static async Task VerifyIntegrity(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA quick_check";
        if (!string.Equals(await command.ExecuteScalarAsync() as string, "ok", StringComparison.Ordinal))
            throw new InvalidOperationException("SQLite integrity check failed. The save was not upgraded.");
        command.CommandText = "PRAGMA foreign_key_check";
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync()) throw new InvalidOperationException("The save contains broken item/account references. Restore a verified backup.");
    }

    private static async Task<long> ScalarLong(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AddGuards(SqliteConnection connection, SqliteTransaction transaction)
    {
        // Additive SQLite migrations cannot replace a table-level CHECK without rebuilding every FK.
        // These guards apply the same new invariants to upgraded and newly created saves.
        const string playerCheck = """
            NEW.Strength BETWEEN 1000000000 AND 100000000000 AND NEW.Defense BETWEEN 1000000000 AND 100000000000
            AND NEW.Speed BETWEEN 1000000000 AND 100000000000 AND NEW.Dexterity BETWEEN 1000000000 AND 100000000000
            AND NEW.Experience BETWEEN 0 AND 1629982 AND NEW.TotalTrainingEnergy >= 0
            AND NEW.EnergyBoostUsed BETWEEN 0 AND 24 AND NEW.Level BETWEEN 1 AND 100 AND NEW.Health BETWEEN 0 AND 595
            """;
        const string instanceCheck = """
            NEW.TrainingBonusPpm >= 0 AND (NEW.EquipmentSlot IS NULL OR NEW.EquipmentSlot IN ('weapon','training'))
            AND (NEW.Equipped = 0 OR NEW.EquipmentSlot IS NOT NULL)
            """;
        foreach (var operation in new[] { "INSERT", "UPDATE" })
        {
            await Execute(connection, $"""
                CREATE TRIGGER IF NOT EXISTS TR_Players_Progression_{operation} BEFORE {operation} ON Players
                WHEN NOT ({playerCheck}) BEGIN SELECT RAISE(ABORT, 'Invalid player progression'); END;
                CREATE TRIGGER IF NOT EXISTS TR_ItemInstances_Progression_{operation} BEFORE {operation} ON ItemInstances
                WHEN NOT ({instanceCheck}) BEGIN SELECT RAISE(ABORT, 'Invalid item progression'); END;
                """, transaction);
        }
    }

    private sealed record LegacyPlayer(int Id, long[] Stats, int Health);

    private const string UpgradeColumns = """
        ALTER TABLE Players ADD COLUMN Experience INTEGER NOT NULL DEFAULT 0 CHECK (Experience BETWEEN 0 AND 1629982);
        ALTER TABLE Players ADD COLUMN TotalTrainingEnergy INTEGER NOT NULL DEFAULT 0 CHECK (TotalTrainingEnergy >= 0);
        ALTER TABLE Players ADD COLUMN EnergyBoostUsed INTEGER NOT NULL DEFAULT 0 CHECK (EnergyBoostUsed BETWEEN 0 AND 24);
        ALTER TABLE Players ADD COLUMN EnergyBoostDay INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE ItemDefinitions ADD COLUMN EquipmentSlot TEXT NULL CHECK (EquipmentSlot IS NULL OR EquipmentSlot IN ('weapon','training'));
        ALTER TABLE ItemDefinitions ADD COLUMN BaseTrainingBonusPpm INTEGER NOT NULL DEFAULT 0 CHECK (BaseTrainingBonusPpm >= 0);
        ALTER TABLE ItemDefinitions ADD COLUMN TrainingBonusRollRangePpm INTEGER NOT NULL DEFAULT 0 CHECK (TrainingBonusRollRangePpm >= 0);
        ALTER TABLE ItemInstances ADD COLUMN EquipmentSlot TEXT NULL CHECK (EquipmentSlot IS NULL OR EquipmentSlot IN ('weapon','training'));
        ALTER TABLE ItemInstances ADD COLUMN TrainingBonusPpm INTEGER NOT NULL DEFAULT 0 CHECK (TrainingBonusPpm >= 0);
        UPDATE ItemDefinitions SET EquipmentSlot = CASE Kind WHEN 'weapon' THEN 'weapon' WHEN 'training' THEN 'training' ELSE NULL END;
        UPDATE ItemInstances SET EquipmentSlot = (SELECT EquipmentSlot FROM ItemDefinitions WHERE ItemDefinitions.Id = ItemInstances.DefinitionId);
        DROP INDEX IF EXISTS IX_ItemInstances_OwnerId;
        CREATE UNIQUE INDEX IX_ItemInstances_OwnerId_EquipmentSlot ON ItemInstances (OwnerId, EquipmentSlot) WHERE Equipped = 1;
        """;
}
