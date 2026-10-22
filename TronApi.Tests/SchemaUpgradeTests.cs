using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TronApi.Data;
using TronApi.Services;
using Xunit;

namespace TronApi.Tests;

public sealed class SchemaUpgradeTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"tron-upgrade-{Guid.NewGuid():N}");
    private string PathToDb => Path.Combine(directory, "save.db");
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private GameDb CreateDb() => new(new DbContextOptionsBuilder<GameDb>().UseSqlite($"Data Source={PathToDb};Pooling=False").Options);

    public Task InitializeAsync() { Directory.CreateDirectory(directory); return Task.CompletedTask; }

    [Fact]
    public async Task UpgradePreservesIdentityChatsItemsAndDamageWithAnOriginalReadableBackup()
    {
        await using var legacy = await CreateLegacy();
        await using var db = CreateDb();
        await SchemaUpgrader.Initialize(db, Now, NullLogger.Instance);
        var player = await db.Players.SingleAsync(p => p.Id == 42);
        Assert.Equal("legacy@tron.local", player.Email);
        Assert.Equal("kept-password-hash", player.PasswordHash);
        Assert.Equal("Existing character", player.Description);
        Assert.Equal(Now.AddDays(-2), player.CreatedAt);
        Assert.Equal(4321, player.Money);
        Assert.Equal(17, player.Energy);
        Assert.Equal(Now.AddMinutes(-3), player.EnergyUpdatedAt);
        Assert.Equal(300, player.TotalTrainingEnergy);
        Assert.Equal(3000, player.Experience);
        Assert.Equal(6, player.Level);
        Assert.Equal(102, player.Health); // The old 23 points of damage remain.
        Assert.InRange(ProgressionRules.Stat(player.Strength), 1.9m, 2m);
        Assert.True(player.Defense > player.Strength);
        Assert.Equal(ProgressionRules.StatScale, player.Speed);
        Assert.Equal(ProgressionRules.StatScale, (await db.Players.SingleAsync(p => p.Id == 43)).Strength);
        Assert.Equal(24, TronApi.Contracts.Dto.Stats(player).EnergyBoostRemaining);
        var item = await db.ItemInstances.SingleAsync(i => i.Id == 501);
        Assert.Equal("Legacy Blade", item.Name);
        Assert.Equal(13, item.Power);
        Assert.True(item.Equipped);
        Assert.Equal("weapon", item.EquipmentSlot);
        Assert.Equal(42, item.OwnerId);
        Assert.Equal(1000, (await db.InventoryStacks.SingleAsync(s => s.Id == 601)).Quantity);
        Assert.Equal(2, await db.ChatMessages.CountAsync());
        var direct = await db.ChatMessages.SingleAsync(m => m.Id == 702);
        Assert.Equal(42, direct.SenderId);
        Assert.Equal(43, direct.RecipientId);
        Assert.Equal("Saved private message", direct.Content);
        Assert.Equal("00000000-0000-0000-0000-000000000002", direct.ClientMessageId);
        Assert.Equal(Now.AddHours(2), (await db.PlayerSessions.SingleAsync()).ExpiresAt);
        Assert.Equal(1, await Scalar(legacy, "SELECT \"unique\" FROM pragma_index_list('ItemInstances') WHERE name='IX_ItemInstances_OwnerId_EquipmentSlot'"));
        await Assert.ThrowsAsync<SqliteException>(() => Command(legacy,
            "INSERT INTO ItemInstances(OwnerId,DefinitionId,Name,Power,Equipped,EquipmentSlot,TrainingBonusPpm) VALUES(42,1,'Second equipped weapon',14,1,'weapon',0)"));
        await Assert.ThrowsAsync<SqliteException>(() => Command(legacy, "UPDATE Players SET Strength=0 WHERE Id=42"));

        var backupPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "backups"), "*.db"));
        await using var backup = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly;Pooling=False");
        await backup.OpenAsync();
        Assert.Equal(0, await Scalar(backup, "PRAGMA user_version"));
        Assert.Equal(110, await Scalar(backup, "SELECT Strength FROM Players WHERE Id=42"));
        Assert.Equal(2, await Scalar(backup, "SELECT COUNT(*) FROM ChatMessages"));
        Assert.Equal(1000, await Scalar(backup, "SELECT Quantity FROM InventoryStacks WHERE Id=601"));

        var migratedStrength = player.Strength;
        await SchemaUpgrader.Initialize(db, Now.AddDays(1), NullLogger.Instance);
        db.ChangeTracker.Clear();
        Assert.Equal(migratedStrength, (await db.Players.SingleAsync(p => p.Id == 42)).Strength);
        Assert.Single(Directory.GetFiles(Path.Combine(directory, "backups"), "*.db"));
        Assert.Equal(2, await Scalar((SqliteConnection)db.Database.GetDbConnection(), "PRAGMA user_version"));
    }

    [Fact]
    public async Task FailureHalfwayThroughUpgradeRollsBackColumnsValuesAndIndexes()
    {
        await using var legacy = await CreateLegacy();
        await Command(legacy, "CREATE INDEX IX_ItemInstances_OwnerId_EquipmentSlot ON ItemInstances(Power)");
        Assert.Equal(1, await Scalar(legacy, "SELECT COUNT(*) FROM sqlite_master WHERE name='IX_ItemInstances_OwnerId_EquipmentSlot'"));
        await using var db = CreateDb();
        await Assert.ThrowsAsync<SqliteException>(() => SchemaUpgrader.Initialize(db, Now, NullLogger.Instance));
        Assert.Equal(0, await Scalar(legacy, "PRAGMA user_version"));
        Assert.Equal(110, await Scalar(legacy, "SELECT Strength FROM Players WHERE Id=42"));
        Assert.Equal(1, await Scalar(legacy, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_ItemInstances_OwnerId'"));
        Assert.Equal(0, await Scalar(legacy, "SELECT COUNT(*) FROM pragma_table_info('Players') WHERE name='Experience'"));
        Assert.Equal(0, await Scalar(legacy, "SELECT COUNT(*) FROM pragma_table_info('ItemInstances') WHERE name='EquipmentSlot'"));
        Assert.Single(Directory.GetFiles(Path.Combine(directory, "backups"), "*.db"));
    }

    [Fact]
    public async Task NewerSchemaIsRefusedWithoutChangingTheSave()
    {
        await using var legacy = await CreateLegacy();
        await Command(legacy, "PRAGMA user_version=99");
        await using var db = CreateDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() => SchemaUpgrader.Initialize(db, Now, NullLogger.Instance));
        Assert.Equal(99, await Scalar(legacy, "PRAGMA user_version"));
        Assert.Equal(110, await Scalar(legacy, "SELECT Strength FROM Players WHERE Id=42"));
        Assert.False(Directory.Exists(Path.Combine(directory, "backups")));
    }

    [Fact]
    public async Task EmptySaveIsCreatedAtLatestVersionWithoutCreatingAnUnnecessaryBackup()
    {
        await using var db = CreateDb();
        await SchemaUpgrader.Initialize(db, Now, NullLogger.Instance);
        Assert.Equal(2, await Scalar((SqliteConnection)db.Database.GetDbConnection(), "PRAGMA user_version"));
        Assert.Equal(0, await db.Players.CountAsync());
        Assert.False(Directory.Exists(Path.Combine(directory, "backups")));
        await SchemaUpgrader.Initialize(db, Now, NullLogger.Instance);
    }

    private async Task<SqliteConnection> CreateLegacy()
    {
        var connection = new SqliteConnection($"Data Source={PathToDb};Foreign Keys=True;Pooling=False");
        await connection.OpenAsync();
        await Command(connection, LegacySchema);
        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            INSERT INTO Players(Id,Email,NormalizedEmail,PasswordHash,DisplayName,NormalizedDisplayName,Description,CreatedAt,Strength,Defense,Speed,Dexterity,Energy,EnergyUpdatedAt,Money,Level,Health)
            VALUES(42,'legacy@tron.local','LEGACY@TRON.LOCAL','kept-password-hash','Legacy','LEGACY','Existing character',$created,110,210,10,10,17,$energyTime,4321,1,77),
                  (43,'other@tron.local','OTHER@TRON.LOCAL','another-hash','Other','OTHER','',$created,10,10,10,10,72,$energyTime,1000,1,100);
            INSERT INTO ItemDefinitions(Id,Name,Description,Kind,Tracking,Price,BasePower,EnergyRestore)
            VALUES(1,'Iron Sword','old description','weapon','instance',150,10,0),(3,'Soda','old soda','consumable','stack',10,0,6);
            INSERT INTO ItemInstances(Id,OwnerId,DefinitionId,Name,Power,Equipped) VALUES(501,42,1,'Legacy Blade',13,1);
            INSERT INTO InventoryStacks(Id,OwnerId,DefinitionId,Quantity) VALUES(601,42,3,1000);
            INSERT INTO ChatMessages(Id,SenderId,RecipientId,Content,CreatedAt,ClientMessageId)
            VALUES(701,42,NULL,'Saved public message',$created,'00000000-0000-0000-0000-000000000001'),
                  (702,42,43,'Saved private message',$created,'00000000-0000-0000-0000-000000000002');
            INSERT INTO PlayerSessions(Id,PlayerId,ExpiresAt) VALUES('kept-session',42,$expires);
            """;
        seed.Parameters.AddWithValue("$created", Now.AddDays(-2).Ticks);
        seed.Parameters.AddWithValue("$energyTime", Now.AddMinutes(-3).Ticks);
        seed.Parameters.AddWithValue("$expires", Now.AddHours(2).Ticks);
        await seed.ExecuteNonQueryAsync();
        return connection;
    }

    private static async Task Command(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> Scalar(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        return Task.CompletedTask;
    }

    // Frozen schema fixture from the first local release, intentionally independent of the current EF model.
    private const string LegacySchema = """
        PRAGMA journal_mode=WAL;
        CREATE TABLE Players(
            Id INTEGER PRIMARY KEY AUTOINCREMENT, Email TEXT NOT NULL, NormalizedEmail TEXT NOT NULL,
            PasswordHash TEXT NOT NULL, DisplayName TEXT NOT NULL, NormalizedDisplayName TEXT NOT NULL,
            Description TEXT NOT NULL, CreatedAt INTEGER NOT NULL,
            Strength INTEGER NOT NULL, Defense INTEGER NOT NULL, Speed INTEGER NOT NULL, Dexterity INTEGER NOT NULL,
            Energy INTEGER NOT NULL, EnergyUpdatedAt INTEGER NOT NULL, Money INTEGER NOT NULL, Level INTEGER NOT NULL, Health INTEGER NOT NULL,
            CONSTRAINT CK_Player_Balances CHECK(Money>=0 AND Energy>=0 AND Energy<=72),
            CONSTRAINT CK_Player_Stats CHECK(Strength>=0 AND Defense>=0 AND Speed>=0 AND Dexterity>=0));
        CREATE UNIQUE INDEX IX_Players_NormalizedEmail ON Players(NormalizedEmail);
        CREATE UNIQUE INDEX IX_Players_NormalizedDisplayName ON Players(NormalizedDisplayName);
        CREATE TABLE ItemDefinitions(
            Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Description TEXT NOT NULL,
            Kind TEXT NOT NULL, Tracking TEXT NOT NULL, Price INTEGER NOT NULL, BasePower INTEGER NOT NULL, EnergyRestore INTEGER NOT NULL);
        CREATE TABLE ItemInstances(
            Id INTEGER PRIMARY KEY AUTOINCREMENT, OwnerId INTEGER NOT NULL REFERENCES Players(Id),
            DefinitionId INTEGER NOT NULL REFERENCES ItemDefinitions(Id), Name TEXT NOT NULL, Power INTEGER NOT NULL, Equipped INTEGER NOT NULL);
        CREATE UNIQUE INDEX IX_ItemInstances_OwnerId ON ItemInstances(OwnerId) WHERE Equipped=1;
        CREATE TABLE InventoryStacks(
            Id INTEGER PRIMARY KEY AUTOINCREMENT, OwnerId INTEGER NOT NULL REFERENCES Players(Id),
            DefinitionId INTEGER NOT NULL REFERENCES ItemDefinitions(Id), Quantity INTEGER NOT NULL CHECK(Quantity>0));
        CREATE UNIQUE INDEX IX_InventoryStacks_OwnerId_DefinitionId ON InventoryStacks(OwnerId,DefinitionId);
        CREATE TABLE ChatMessages(
            Id INTEGER PRIMARY KEY AUTOINCREMENT, SenderId INTEGER NOT NULL REFERENCES Players(Id),
            RecipientId INTEGER NULL REFERENCES Players(Id), Content TEXT NOT NULL, CreatedAt INTEGER NOT NULL, ClientMessageId TEXT NOT NULL);
        CREATE UNIQUE INDEX IX_ChatMessages_SenderId_ClientMessageId ON ChatMessages(SenderId,ClientMessageId);
        CREATE TABLE PlayerSessions(Id TEXT PRIMARY KEY, PlayerId INTEGER NOT NULL REFERENCES Players(Id), ExpiresAt INTEGER NOT NULL);
        """;
}
