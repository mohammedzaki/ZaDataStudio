using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;

namespace ZaDataStudio.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Manages SQL Server database connections with connection pooling and reuse
/// Ensures only one active connection per connection string during operations
/// </summary>
public class SqlServerConnectionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, SqlConnection> _connections = new();
    private bool _disposed = false;

    /// <summary>
    /// Gets or creates a connection for the given connection string
    /// Reuses existing open connections
    /// </summary>
    public async Task<SqlConnection> GetConnectionAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        // Try to get existing connection
        if (_connections.TryGetValue(connectionString, out var existingConnection))
        {
            // Check if connection is still valid
            if (existingConnection.State == System.Data.ConnectionState.Open)
            {
                return existingConnection;
            }
            else
            {
                // Connection closed, remove it
                _connections.TryRemove(connectionString, out _);
                existingConnection.Dispose();
            }
        }

        // Create new connection
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Store for reuse
        _connections.TryAdd(connectionString, connection);

        return connection;
    }

    /// <summary>
    /// Executes a SQL command and returns a SqlDataReader
    /// </summary>
    public async Task<SqlDataReader> ExecuteReaderAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null)
    {
        var connection = await GetConnectionAsync(connectionString);
        var command = connection.CreateCommand();
        command.CommandText = query;
        
        configureCommand?.Invoke(command);

        return await command.ExecuteReaderAsync(System.Data.CommandBehavior.Default);
    }

    /// <summary>
    /// Executes a scalar SQL command
    /// </summary>
    public async Task<object?> ExecuteScalarAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null)
    {
        var connection = await GetConnectionAsync(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = query;
        
        configureCommand?.Invoke(command);

        return await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// Executes a non-query SQL command
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(string connectionString, string query, Action<SqlCommand>? configureCommand = null)
    {
        var connection = await GetConnectionAsync(connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = query;
        
        configureCommand?.Invoke(command);

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Closes and removes a specific connection
    /// </summary>
    public void CloseConnection(string connectionString)
    {
        if (_connections.TryRemove(connectionString, out var connection))
        {
            connection.Dispose();
        }
    }

    /// <summary>
    /// Closes all connections
    /// </summary>
    public void CloseAllConnections()
    {
        foreach (var kvp in _connections)
        {
            kvp.Value.Dispose();
        }
        _connections.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CloseAllConnections();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
