using ZaDataStudio.Domain.Entities;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Interface for analyzing lookup column mappings and comparing source/destination values
/// </summary>
public interface ILookupColumnAnalyzer
{
    /// <summary>
    /// Analyze lookup column using mapping rule
    /// </summary>
    Task<LookupColumnAnalysis> AnalyzeLookupColumnAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString,
        IProgress<AnalysisProgress>? progress);

    /// <summary>
    /// Analyze lookup column with specification format
    /// Handles format: [ValueColumnName].[TableName].[ColumnName] = Value [ON [JoinColumnName]]
    /// </summary>
    Task<LookupColumnAnalysis> AnalyzeLookupColumnWithSpecAsync(
        DataColumnMapping columnMapping,
        string sourceConnectionString,
        string destinationConnectionString,
        IProgress<AnalysisProgress>? progress);
}
