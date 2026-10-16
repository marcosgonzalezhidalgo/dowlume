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

    public AppSettings? LoadSettings() => ReadAll<AppSettings>(StateTable.Settings).SingleOrDefault();

    public void SaveSettings(AppSettings settings)
    {
        settings.Validate();
        using SqliteConnection connection = Open();
        Write(connection, null, StateTable.Settings, null, settings);
    }

    public IReadOnlyList<TrackedFile> GetFiles() => ReadAll<TrackedFile>(StateTable.Files);
    public IReadOnlyList<OperationRecord> GetOperations() => ReadAll<OperationRecord>(StateTable.Operations);

    public OperationHistoryPage GetOperationHistory(OperationHistoryQuery query)
    {
        query.Validate();
        using SqliteConnection connection = Open();
        connection.CreateFunction("contains_text", (string? value, string text) =>
            value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true, isDeterministic: true);
        // Count, boundary, and rows must describe the same committed journal snapshot.
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(rowid), 0) FROM operations;";
        long boundary = query.ThroughSequence ?? (long)command.ExecuteScalar()!;
        const string filter = """
            FROM operations
            WHERE rowid <= $boundary
              AND ($kind IS NULL OR json_extract(payload, '$.Kind') = $kind)
              AND ($status IS NULL OR json_extract(payload, '$.Status') = $status)
              AND ($search = '' OR contains_text(json_extract(payload, '$.SourcePath'), $search)
                                OR contains_text(json_extract(payload, '$.DestinationPath'), $search))
            """;
        command.Parameters.AddWithValue("$boundary", boundary);
        command.Parameters.AddWithValue("$kind", query.Kind is { } kind ? (int)kind : DBNull.Value);
        command.Parameters.AddWithValue("$status", query.Status is { } status ? (int)status : DBNull.Value);
        command.Parameters.AddWithValue("$search", query.SearchText.Trim());
        command.CommandText = "SELECT COUNT(*) " + filter;
        int totalCount = checked((int)(long)command.ExecuteScalar()!);
        int pageCount = totalCount == 0 ? 1 : (totalCount - 1) / query.PageSize + 1;
        int pageNumber = Math.Min(query.PageNumber, pageCount);
        command.Parameters.AddWithValue("$limit", query.PageSize);
        command.Parameters.AddWithValue("$offset", (long)(pageNumber - 1) * query.PageSize);
        command.CommandText = "SELECT payload " + filter + " ORDER BY rowid DESC LIMIT $limit OFFSET $offset;";
        List<OperationRecord> items = ReadRecords<OperationRecord>(command);
        return new OperationHistoryPage(items, totalCount, pageNumber, query.PageSize, boundary);
    }

    public int CountOperationsRequiringAttention()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM operations WHERE json_extract(payload, '$.Status') IN ($pending, $attention);";
        command.Parameters.AddWithValue("$pending", (int)OperationStatus.Pending);
        command.Parameters.AddWithValue("$attention", (int)OperationStatus.NeedsAttention);
        return checked((int)(long)command.ExecuteScalar()!);
    }

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
        WriteOperation(connection, transaction, operation with
        {
            Status = OperationStatus.Completed,
            Error = null
        });
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

    private List<T> ReadAll<T>(StateTable table)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        switch (table)
        {
            case StateTable.Settings:
                command.CommandText = "SELECT payload FROM settings;";
                break;
            case StateTable.Files:
                command.CommandText = "SELECT payload FROM files ORDER BY rowid;";
                break;
            case StateTable.Operations:
                command.CommandText = "SELECT payload FROM operations ORDER BY rowid;";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(table));
        }
        return ReadRecords<T>(command);
    }

    private static List<T> ReadRecords<T>(SqliteCommand command)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read())
        {
            result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0)) ?? throw new InvalidDataException("Invalid local database record."));
        }
        return result;
    }

    private static void WriteFile(SqliteConnection connection, SqliteTransaction? transaction, TrackedFile file) =>
        Write(connection, transaction, StateTable.Files, file.Id, file);

    private static void WriteOperation(SqliteConnection connection, SqliteTransaction? transaction, OperationRecord operation) =>
        Write(connection, transaction, StateTable.Operations, operation.Id, operation);

    private static void Write<T>(SqliteConnection connection, SqliteTransaction? transaction, StateTable table, Guid? id, T value)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        switch (table)
        {
            case StateTable.Settings:
                command.CommandText = "INSERT INTO settings VALUES(1, $payload) ON CONFLICT(id) DO UPDATE SET payload=$payload;";
                break;
            case StateTable.Files:
                command.CommandText = "INSERT INTO files VALUES($id, $payload) ON CONFLICT(id) DO UPDATE SET payload=$payload;";
                break;
            case StateTable.Operations:
                command.CommandText = "INSERT INTO operations VALUES($id, $payload) ON CONFLICT(id) DO UPDATE SET payload=$payload;";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(table));
        }
        if (id.HasValue)
        {
            command.Parameters.AddWithValue("$id", id.Value.ToString());
        }
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(value));
        command.ExecuteNonQuery();
    }

    private enum StateTable
    {
        Settings, Files, Operations
    }
}
