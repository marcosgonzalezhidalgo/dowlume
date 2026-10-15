using System.Text.Json;
using Dowlume.Core;
using Microsoft.Data.Sqlite;

namespace Dowlume.Infrastructure;

public sealed class SqliteStateStore : IStateStore
{
    private readonly string _connectionString;

    public SqliteStateStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        long version = (long)command.ExecuteScalar()!;
        if (version > 1)
        {
            throw new InvalidOperationException("This database belongs to a newer version of Dowlume.");
        }
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS settings (id INTEGER PRIMARY KEY CHECK(id = 1), payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS files (id TEXT PRIMARY KEY, payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS operations (id TEXT PRIMARY KEY, payload TEXT NOT NULL);
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
    }

    public AppSettings? LoadSettings() => ReadAll<AppSettings>("SELECT payload FROM settings;").SingleOrDefault();

    public void SaveSettings(AppSettings settings)
    {
        settings.Validate();
        using SqliteConnection connection = Open();
        Write(connection, null, "INSERT INTO settings VALUES(1, $payload) ON CONFLICT(id) DO UPDATE SET payload=$payload;", null, settings);
    }

    public IReadOnlyList<TrackedFile> GetFiles() => ReadAll<TrackedFile>("SELECT payload FROM files ORDER BY rowid;");
    public IReadOnlyList<OperationRecord> GetOperations() => ReadAll<OperationRecord>("SELECT payload FROM operations ORDER BY rowid;");

    public void SaveFile(TrackedFile file)
    {
        using SqliteConnection connection = Open();
        WriteFile(connection, null, file);
    }

    public void SaveOperation(OperationRecord operation)
    {
        using SqliteConnection connection = Open();
        WriteOperation(connection, null, operation);
    }

    public void CompleteOperation(OperationRecord operation)
    {
        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        WriteFile(connection, transaction, operation.After);
        WriteOperation(connection, transaction, operation with { Status = OperationStatus.Completed, Error = null });
        transaction.Commit();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private List<T> ReadAll<T>(string query)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        using SqliteDataReader reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read())
        {
            result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0)) ?? throw new InvalidDataException("Invalid local database record."));
        }
        return result;
    }

    private static void WriteFile(SqliteConnection connection, SqliteTransaction? transaction, TrackedFile file) =>
        Write(connection, transaction, "INSERT INTO files VALUES($id, $payload) ON CONFLICT(id) DO UPDATE SET payload=$payload;", file.Id, file);

    private static void WriteOperation(SqliteConnection connection, SqliteTransaction? transaction, OperationRecord operation) =>
        Write(connection, transaction, "INSERT INTO operations VALUES($id, $payload) ON CONFLICT(id) DO UPDATE SET payload=$payload;", operation.Id, operation);

    private static void Write<T>(SqliteConnection connection, SqliteTransaction? transaction, string sql, Guid? id, T value)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        if (id.HasValue)
        {
            command.Parameters.AddWithValue("$id", id.Value.ToString());
        }
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(value));
        command.ExecuteNonQuery();
    }
}
