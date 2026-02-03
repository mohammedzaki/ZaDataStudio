using Microsoft.Data.SqlClient;
using System.Data;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Centralized service for all SQL Server database operations
/// Uses SqlServerConnectionManager for efficient connection management
/// </summary>
public class SqlServerDatabaseService : IDatabaseService
{
    internal readonly SqlServerConnectionManager _connectionManager;

    public SqlServerDatabaseService(SqlServerConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// Gets or creates a connection for the given connection string
    /// </summary>
    public async Task<SqlConnection> GetConnectionAsync(string connectionString)
    {
        return await _connectionManager.GetConnectionAsync(connectionString);
    }

    /// <summary>
    /// Executes a SQL command and returns a SqlDataReader
    /// </summary>
    public async Task<SqlDataReader> ExecuteReaderAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null)
    {
        return await _connectionManager.ExecuteReaderAsync(connectionString, query, configureCommand);
    }

    /// <summary>
    /// Executes a scalar SQL command
    /// </summary>
    public async Task<object?> ExecuteScalarAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null)
    {
        return await _connectionManager.ExecuteScalarAsync(connectionString, query, configureCommand);
    }

    /// <summary>
    /// Executes a non-query SQL command
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null)
    {
        return await _connectionManager.ExecuteNonQueryAsync(connectionString, query, configureCommand);
    }

    /// <summary>
    /// Tests database connection
    /// </summary>
    public async Task<(bool IsSuccessful, string? ServerName, string? DatabaseName, double ResponseTime, string? ErrorMessage)> TestConnectionAsync(string connectionString)
    {
        var startTime = DateTime.Now;

        try
        {
            var connection = await GetConnectionAsync(connectionString);

            // Get server and database info
            var serverName = connection.DataSource;
            var databaseName = connection.Database;

            // Test with a simple query
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync();

            var responseTime = (DateTime.Now - startTime).TotalMilliseconds;

            return (true, serverName, databaseName, responseTime, null);
        }
        catch (Exception ex)
        {
            var responseTime = (DateTime.Now - startTime).TotalMilliseconds;
            return (false, null, null, responseTime, ex.Message);
        }
    }

    /// <summary>
    /// Executes a custom query and returns results as a list of dictionaries
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null)
    {
        var results = new List<Dictionary<string, object?>>();

        var connection = await GetConnectionAsync(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = query;

        configureCommand?.Invoke(command);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Gets list of table names from a database
    /// </summary>
    public async Task<List<string>> GetTableNamesAsync(string connectionString)
    {
        var tables = new List<string>();

        var query = @"
            SELECT TABLE_SCHEMA + '.' + TABLE_NAME as TableName
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

        var connection = await _connectionManager.GetConnectionAsync(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = query;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>
    /// Gets column names for a specific table
    /// </summary>
    public async Task<List<string>> GetTableColumnsAsync(string connectionString, string tableName)
    {
        var columns = new List<string>();

        var query = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA + '.' + TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION";

        var connection = await _connectionManager.GetConnectionAsync(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    /// <summary>
    /// Gets column type information for a table
    /// </summary>
    public async Task<Dictionary<string, ColumnTypeInfo>> GetColumnTypesAsync(string connectionString, string tableName)
    {
        var columns = new Dictionary<string, ColumnTypeInfo>();

        var query = @"
            SELECT 
                COLUMN_NAME,
                DATA_TYPE,
                CHARACTER_MAXIMUM_LENGTH,
                NUMERIC_PRECISION,
                NUMERIC_SCALE,
                IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA + '.' + TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION";

        var connection = await _connectionManager.GetConnectionAsync(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(0);
            var info = new ColumnTypeInfo
            {
                ColumnName = columnName,
                DataType = reader.GetString(1),
                MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                NumericPrecision = reader.IsDBNull(3) ? null : reader.GetByte(3),
                NumericScale = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                IsNullable = reader.GetString(5) == "YES"
            };
            columns[columnName] = info;
        }

        return columns;
    }

    /// <summary>
    /// Gets distinct values from a column with optional filter
    /// </summary>
    public async Task<List<string>> GetDistinctValuesAsync(string connectionString, string tableName, string columnName, string? whereClause = null, int limit = 1000)
    {
        var values = new List<string>();

        var query = $@"
            SELECT DISTINCT TOP {limit} CAST([{columnName}] AS NVARCHAR(MAX)) as Value
            FROM {tableName}
            {(string.IsNullOrWhiteSpace(whereClause) ? "" : $"WHERE {whereClause}")}
            ORDER BY Value";

        var connection = await _connectionManager.GetConnectionAsync(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = query;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    /// <summary>
    /// Gets count of distinct values in a column
    /// </summary>
    public async Task<int> GetDistinctCountAsync(string connectionString, string tableName, string columnName, string? whereClause = null)
    {
        var query = $@"
            SELECT COUNT(DISTINCT [{columnName}])
            FROM {tableName}
            {(string.IsNullOrWhiteSpace(whereClause) ? "" : $"WHERE {whereClause}")}";

        var result = await _connectionManager.ExecuteScalarAsync(connectionString, query);
        return result != null ? Convert.ToInt32(result) : 0;
    }
}
