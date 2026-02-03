using Microsoft.Data.SqlClient;
using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Common.Interfaces;

/// <summary>
/// Interface for database operations - maintains Clean Architecture
/// Implementation is in Infrastructure layer
/// Provides connection reuse and centralized database operations
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Gets or creates a connection for the given connection string
    /// Returns existing open connection or creates new one
    /// </summary>
    Task<SqlConnection> GetConnectionAsync(string connectionString);

    /// <summary>
    /// Executes a SQL command and returns a SqlDataReader
    /// </summary>
    Task<SqlDataReader> ExecuteReaderAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null);

    /// <summary>
    /// Executes a scalar SQL command
    /// </summary>
    Task<object?> ExecuteScalarAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null);

    /// <summary>
    /// Executes a non-query SQL command
    /// </summary>
    Task<int> ExecuteNonQueryAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null);

    /// <summary>
    /// Gets list of table names from a database
    /// </summary>
    Task<List<string>> GetTableNamesAsync(string connectionString);

    /// <summary>
    /// Gets column names for a specific table
    /// </summary>
    Task<List<string>> GetTableColumnsAsync(string connectionString, string tableName);

    /// <summary>
    /// Gets distinct values from a column with optional filter
    /// </summary>
    Task<List<string>> GetDistinctValuesAsync(string connectionString, string tableName, string columnName, string? whereClause = null, int limit = 1000);

    /// <summary>
    /// Gets count of distinct values in a column
    /// </summary>
    Task<int> GetDistinctCountAsync(string connectionString, string tableName, string columnName, string? whereClause = null);

    /// <summary>
    /// Tests database connection
    /// </summary>
    Task<(bool IsSuccessful, string? ServerName, string? DatabaseName, double ResponseTime, string? ErrorMessage)> TestConnectionAsync(string connectionString);

    /// <summary>
    /// Executes a custom query and returns results as a list of dictionaries
    /// </summary>
    Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null);

    /// <summary>
    /// Gets column type information for a table
    /// </summary>
    Task<Dictionary<string, ColumnTypeInfo>> GetColumnTypesAsync(string connectionString, string tableName);
}
