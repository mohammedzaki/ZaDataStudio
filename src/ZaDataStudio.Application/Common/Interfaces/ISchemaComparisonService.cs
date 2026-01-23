using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Common.Interfaces;

public interface ISchemaComparisonService
{
    Task<ConnectionTestResult> TestConnectionAsync(string connectionString);
    Task<List<string>> GetTableNamesAsync(string connectionString);
    Task<List<TableSchema>> GetTableSchemasAsync(string connectionString);
    Task<List<TableSchema>> GetTableSchemasAsync(string connectionString, List<string> tableNames);
    ComparisonResult CompareSchemas(List<TableSchema> sourceTables, List<TableSchema> destinationTables);
    ComparisonResult CompareMappedSchemas(List<TableSchema> sourceTables, List<TableSchema> destTables, List<TableMapping> mappings);
    Task<List<ColumnTypeInfo>> GetColumnTypesAsync(string connectionString, string tableName);
    ColumnTypeComparisonResult CompareColumnTypes(List<ColumnTypeInfo> sourceColumns, List<ColumnTypeInfo> destColumns, List<ColumnMapping> mappings);
}


