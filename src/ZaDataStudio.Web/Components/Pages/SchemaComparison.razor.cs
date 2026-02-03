using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.SqlClient;
using Microsoft.JSInterop;
using ZaDataStudio.Application.Common.Interfaces;
using ZaDataStudio.Application.Mapping;
using ZaDataStudio.Domain.Entities;
using ZaDataStudio.Infrastructure.Excel;
using ZaDataStudio.Infrastructure.Persistence.SqlServer;

namespace ZaDataStudio.Web.Components.Pages;


// Must be 'partial' and have the same class name as the .razor file
public partial class SchemaComparison : ComponentBase
{
    private string sourceConnectionString = string.Empty;
    private string destinationConnectionString = string.Empty;
    private bool isComparing = false;
    private bool isComparingData = false;
    private bool isTestingSource = false;
    private bool isTestingDest = false;
    private bool isLoadingTables = false;
    private bool isLoadingColumns = false;
    private string errorMessage = string.Empty;
    private ComparisonResult? comparisonResult;
    private List<DataComparisonResult> dataComparisonResults = new();
    private ConnectionTestResult? sourceTestResult;
    private ConnectionTestResult? destTestResult;
    private List<string> sourceTables = new();
    private List<string> destinationTables = new();
    private List<TableMapping> tableMappings = new();

    // Column mapping
    private bool showColumnMapping = false;
    private int currentMappingIndex = -1;
    private List<string> availableSourceColumns = new();
    private List<string> availableDestinationColumns = new();
    private HashSet<int> expandedTableIndices = new();
    private int loadingColumnsForTable = -1;
    private bool isComparingColumnTypes = false;
    private ColumnTypeComparisonResult? columnTypeResult;

    // Multi-source tables
    private bool showMultiSourceDialog = false;
    private int currentMultiSourceIndex = -1;
    private List<string> currentMultiSourceTables = new();
    private Dictionary<string, List<string>> tableColumnCache = new();

    // Excel mapping
    private DataMappingConfiguration? excelMappingConfig;
    private string excelGeneratedSQL = string.Empty;
    private string excelValidationReport = string.Empty;
    private string excelLoadMessage = string.Empty;
    private bool isComparingExcelMappings = false;
    private bool isUploadingExcel = false;
    private bool isExportingAnalysisExcel = false;
    private MappingComparisonResult? excelComparisonResult;
    private IMappingComparisonService MappingComparisonService;

    // Manual mapping SQL generation
    private string manualMappingGeneratedSQL = string.Empty;

    // Session management
    private bool showSaveDialog = false;
    private bool showLoadDialog = false;
    private string sessionName = string.Empty;
    private string saveMessage = string.Empty;
    private string loadMessage = string.Empty;
    private List<ComparisonSession> savedSessions = new();
    private ISessionRepository SessionRepository;
    private SqlServerComparisonService ComparisonService;
    private IDatabaseService DatabaseService;
    private DataComparisonService DataComparisonService;
    private ExcelMappingService ExcelMappingService;
    private IJSRuntime JSRuntime;

    public SchemaComparison(
        ISessionRepository sessionRepository, 
        SqlServerComparisonService comparisonService,
        IDatabaseService databaseService,
        DataComparisonService dataComparisonService,
        ExcelMappingService excelMappingService,
        IMappingComparisonService mappingComparisonService,
        IJSRuntime jsRuntime) 
    {
        SessionRepository = sessionRepository;
        ComparisonService = comparisonService;
        DatabaseService = databaseService;
        DataComparisonService = dataComparisonService;
        ExcelMappingService = excelMappingService;
        MappingComparisonService = mappingComparisonService;
        JSRuntime = jsRuntime;
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadSavedSessions();
    }

    private async Task LoadSavedSessions()
    {
        savedSessions = await SessionRepository.GetAllSessionsAsync();
    }

    private void ToggleSaveDialog()
    {
        showSaveDialog = !showSaveDialog;
        if (showSaveDialog)
        {
            showLoadDialog = false;
            saveMessage = string.Empty;
            sessionName = string.Empty;
        }
    }

    private async Task ToggleLoadDialog()
    {
        showLoadDialog = !showLoadDialog;
        if (showLoadDialog)
        {
            showSaveDialog = false;
            loadMessage = string.Empty;
            await LoadSavedSessions();
        }
    }

    private async Task SaveSession()
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            sessionName = $"Session {DateTime.Now:yyyy-MM-dd HH:mm}";
        }

        var session = new ComparisonSession
        {
            Name = sessionName,
            SourceConnectionString = sourceConnectionString,
            DestinationConnectionString = destinationConnectionString,

            // Save table mappings with all details
            TableMappings = tableMappings.Select(m => new TableMapping
            {
                SourceTable = m.SourceTable,
                SourceTables = new List<string>(m.SourceTables),
                DestinationTable = m.DestinationTable,
                IsSelected = m.IsSelected,
                CompareData = m.CompareData,
                ColumnMappings = m.ColumnMappings.Select(cm => new ColumnMapping
                {
                    SourceTable = cm.SourceTable,
                    SourceColumn = cm.SourceColumn,
                    DestinationColumn = cm.DestinationColumn,
                    IsKey = cm.IsKey
                }).ToList(),
                AvailableSourceColumns = new List<string>(m.AvailableSourceColumns),
                AvailableDestinationColumns = new List<string>(m.AvailableDestinationColumns)
            }).ToList(),

            // Save loaded tables
            SourceTables = new List<string>(sourceTables),
            DestinationTables = new List<string>(destinationTables),

            // Save column cache
            TableColumnCache = new Dictionary<string, List<string>>(tableColumnCache),

            // Save expanded table indices
            ExpandedTableIndices = new HashSet<int>(expandedTableIndices),

            // Save connection test results
            SourceTestResult = sourceTestResult,
            DestinationTestResult = destTestResult
        };

        await SessionRepository.SaveSessionAsync(session);
        saveMessage = $"Session '{session.DisplayName}' saved successfully!";

        await Task.Delay(2000);
        showSaveDialog = false;
        saveMessage = string.Empty;
    }

    private async Task LoadSession(string sessionId)
    {
        var session = await SessionRepository.GetSessionAsync(sessionId);
        if (session != null)
        {
            // Restore connection strings
            sourceConnectionString = session.SourceConnectionString;
            destinationConnectionString = session.DestinationConnectionString;

            // Restore table mappings with all details
            tableMappings = session.TableMappings.Select(m => new TableMapping
            {
                SourceTable = m.SourceTable,
                SourceTables = new List<string>(m.SourceTables ?? new List<string>()),
                DestinationTable = m.DestinationTable,
                IsSelected = m.IsSelected,
                CompareData = m.CompareData,
                ColumnMappings = m.ColumnMappings?.Select(cm => new ColumnMapping
                {
                    SourceTable = cm.SourceTable ?? string.Empty,
                    SourceColumn = cm.SourceColumn,
                    DestinationColumn = cm.DestinationColumn,
                    IsKey = cm.IsKey
                }).ToList() ?? new List<ColumnMapping>(),
                AvailableSourceColumns = new List<string>(m.AvailableSourceColumns ?? new List<string>()),
                AvailableDestinationColumns = new List<string>(m.AvailableDestinationColumns ?? new List<string>())
            }).ToList();

            // Restore loaded tables
            sourceTables = new List<string>(session.SourceTables ?? new List<string>());
            destinationTables = new List<string>(session.DestinationTables ?? new List<string>());

            // Restore column cache
            tableColumnCache = new Dictionary<string, List<string>>(
                session.TableColumnCache ?? new Dictionary<string, List<string>>());

            // Restore expanded table indices
            expandedTableIndices = new HashSet<int>(session.ExpandedTableIndices ?? new HashSet<int>());

            // Restore connection test results
            sourceTestResult = session.SourceTestResult;
            destTestResult = session.DestinationTestResult;

            // Clear comparison results (these are not persisted)
            comparisonResult = null;
            dataComparisonResults.Clear();
            errorMessage = string.Empty;

            // Build detailed load message
            var loadedItems = new List<string>();
            loadedItems.Add($"{tableMappings.Count} table mapping(s)");

            var totalColumns = tableMappings.Sum(m => m.ColumnMappings.Count);
            if (totalColumns > 0)
                loadedItems.Add($"{totalColumns} column mapping(s)");

            if (sourceTables.Any())
                loadedItems.Add($"{sourceTables.Count} source table(s)");

            if (destinationTables.Any())
                loadedItems.Add($"{destinationTables.Count} destination table(s)");

            if (tableColumnCache.Any())
                loadedItems.Add($"{tableColumnCache.Count} table(s) in column cache");

            if (sourceTestResult?.IsSuccessful == true)
                loadedItems.Add("source connection verified");

            if (destTestResult?.IsSuccessful == true)
                loadedItems.Add("destination connection verified");

            loadMessage = $"Session '{session.DisplayName}' loaded successfully! Restored: {string.Join(", ", loadedItems)}.";

            showLoadDialog = false;

            // Auto-dismiss message after 5 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                loadMessage = string.Empty;
                await InvokeAsync(StateHasChanged);
            });

            StateHasChanged();
        }
    }

    private async Task DeleteSession(string sessionId)
    {
        await SessionRepository.DeleteSessionAsync(sessionId);
        await LoadSavedSessions();
    }

    private bool CanCompare =>
        sourceTestResult?.IsSuccessful == true &&
        destTestResult?.IsSuccessful == true &&
        tableMappings.Any(m => m.IsSelected && !string.IsNullOrEmpty(m.SourceTable) && !string.IsNullOrEmpty(m.DestinationTable)) &&
        !isComparing;

    private bool CanCompareData =>
        sourceTestResult?.IsSuccessful == true &&
        destTestResult?.IsSuccessful == true &&
        tableMappings.Any(m => m.IsSelected && m.CompareData && !string.IsNullOrEmpty(m.SourceTable) && !string.IsNullOrEmpty(m.DestinationTable)) &&
        !isComparingData;

    private void OpenColumnMapping(int index)
    {
        currentMappingIndex = index;
        showColumnMapping = true;
    }

    private void CloseColumnMapping()
    {
        showColumnMapping = false;
        currentMappingIndex = -1;
        availableSourceColumns.Clear();
        availableDestinationColumns.Clear();
        columnTypeResult = null;
    }


    private async Task LoadColumns()
    {
        if (currentMappingIndex < 0 || currentMappingIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[currentMappingIndex];
        isLoadingColumns = true;

        try
        {
            var sourceColumns = await GetTableColumnsAsync(sourceConnectionString, mapping.SourceTable);
            var destColumns = await GetTableColumnsAsync(destinationConnectionString, mapping.DestinationTable);

            // Store available columns for dropdowns
            availableSourceColumns = sourceColumns;
            availableDestinationColumns = destColumns;

            // Don't auto-create mappings - let user decide
            if (!mapping.ColumnMappings.Any())
            {
                mapping.ColumnMappings.Clear();
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading columns: {ex.Message}";
        }
        finally
        {
            isLoadingColumns = false;
        }
    }

    private async Task<List<string>> GetTableColumnsAsync(string connectionString, string tableName)
    {
        return await DatabaseService.GetTableColumnsAsync(connectionString, tableName);
    }

    private void AutoMapColumns()
    {
        if (currentMappingIndex < 0 || currentMappingIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[currentMappingIndex];

        // If there are existing mappings, just fill in empty destination columns
        if (mapping.ColumnMappings.Any())
        {
            foreach (var colMapping in mapping.ColumnMappings)
            {
                if (string.IsNullOrEmpty(colMapping.DestinationColumn))
                {
                    colMapping.DestinationColumn = colMapping.SourceColumn;
                }
            }
        }
        else
        {
            // Create new mappings from available columns
            mapping.ColumnMappings.Clear();

            // Auto-map matching columns
            foreach (var sourceCol in availableSourceColumns)
            {
                var destCol = availableDestinationColumns.FirstOrDefault(d => d == sourceCol) ?? string.Empty;
                mapping.ColumnMappings.Add(new ColumnMapping
                {
                    SourceColumn = sourceCol,
                    DestinationColumn = destCol,
                    IsKey = false
                });
            }

            // Add destination-only columns
            var sourceColNames = availableSourceColumns.ToHashSet();
            foreach (var destCol in availableDestinationColumns.Where(d => !sourceColNames.Contains(d)))
            {
                mapping.ColumnMappings.Add(new ColumnMapping
                {
                    SourceColumn = string.Empty,
                    DestinationColumn = destCol,
                    IsKey = false
                });
            }
        }
    }

    private void AddColumnMapping()
    {
        if (currentMappingIndex < 0 || currentMappingIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[currentMappingIndex];
        mapping.ColumnMappings.Add(new ColumnMapping
        {
            SourceColumn = string.Empty,
            DestinationColumn = string.Empty,
            IsKey = false
        });
    }

    private void RemoveColumnMapping(int index)
    {
        if (currentMappingIndex < 0 || currentMappingIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[currentMappingIndex];
        if (index >= 0 && index < mapping.ColumnMappings.Count)
        {
            mapping.ColumnMappings.RemoveAt(index);
        }
    }

    private void ToggleTableExpansion(int tableIndex)
    {
        if (expandedTableIndices.Contains(tableIndex))
        {
            expandedTableIndices.Remove(tableIndex);
        }
        else
        {
            expandedTableIndices.Add(tableIndex);
        }
        StateHasChanged();
    }

    private async Task LoadColumnsForTable(int tableIndex)
    {
        if (tableIndex < 0 || tableIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[tableIndex];
        if (!mapping.GetAllSourceTables().Any() || string.IsNullOrEmpty(mapping.DestinationTable))
            return;

        loadingColumnsForTable = tableIndex;
        errorMessage = string.Empty;

        try
        {
            // Load columns from all source tables
            var allSourceColumns = new List<string>();
            foreach (var sourceTable in mapping.GetAllSourceTables())
            {
                if (!tableColumnCache.ContainsKey(sourceTable))
                {
                    var columns = await GetTableColumnsAsync(sourceConnectionString, sourceTable);
                    tableColumnCache[sourceTable] = columns;
                }
                allSourceColumns.AddRange(tableColumnCache[sourceTable]);
            }

            // Load destination columns
            var destColumns = await GetTableColumnsAsync(destinationConnectionString, mapping.DestinationTable);

            mapping.AvailableSourceColumns = allSourceColumns.Distinct().ToList();
            mapping.AvailableDestinationColumns = destColumns;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading columns: {ex.Message}";
        }
        finally
        {
            loadingColumnsForTable = -1;
        }
    }

    private void AutoMapColumnsForTable(int tableIndex)
    {
        if (tableIndex < 0 || tableIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[tableIndex];
        mapping.ColumnMappings.Clear();

        var commonColumns = mapping.AvailableSourceColumns.Intersect(mapping.AvailableDestinationColumns).ToList();

        foreach (var column in commonColumns)
        {
            mapping.ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = column,
                DestinationColumn = column,
                IsKey = false
            });
        }
    }

    private void AddColumnMappingForTable(int tableIndex)
    {
        if (tableIndex < 0 || tableIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[tableIndex];
        var defaultSourceTable = mapping.GetAllSourceTables().FirstOrDefault() ?? string.Empty;

        mapping.ColumnMappings.Add(new ColumnMapping
        {
            SourceTable = defaultSourceTable,
            SourceColumn = string.Empty,
            DestinationColumn = string.Empty,
            IsKey = false
        });
    }

    private void RemoveColumnMappingForTable(int tableIndex, int columnIndex)
    {
        if (tableIndex < 0 || tableIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[tableIndex];
        if (columnIndex >= 0 && columnIndex < mapping.ColumnMappings.Count)
        {
            mapping.ColumnMappings.RemoveAt(columnIndex);
        }
    }

    // Multi-source table methods
    private void HandleSourceTableChange(int index, string selectedTable)
    {
        if (index < 0 || index >= tableMappings.Count)
            return;

        var mapping = tableMappings[index];
        mapping.SourceTable = selectedTable;

        // Initialize SourceTables list if primary table is selected
        if (!string.IsNullOrEmpty(selectedTable) && !mapping.SourceTables.Contains(selectedTable))
        {
            mapping.SourceTables.Clear();
            mapping.SourceTables.Add(selectedTable);
        }
    }

    private void ShowMultiSourceDialog(int index)
    {
        if (index < 0 || index >= tableMappings.Count)
            return;

        currentMultiSourceIndex = index;
        var mapping = tableMappings[index];

        // Initialize current selection
        currentMultiSourceTables = new List<string>(mapping.GetAllSourceTables());

        showMultiSourceDialog = true;
    }

    private void CloseMultiSourceDialog()
    {
        showMultiSourceDialog = false;
        currentMultiSourceIndex = -1;
        currentMultiSourceTables.Clear();
    }

    private void ToggleMultiSourceTable(string table, bool isChecked)
    {
        if (isChecked && !currentMultiSourceTables.Contains(table))
        {
            currentMultiSourceTables.Add(table);
        }
        else if (!isChecked && currentMultiSourceTables.Contains(table))
        {
            currentMultiSourceTables.Remove(table);
        }
    }

    private async Task ApplyMultiSourceTables()
    {
        if (currentMultiSourceIndex < 0 || currentMultiSourceIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[currentMultiSourceIndex];
        mapping.SourceTables = new List<string>(currentMultiSourceTables);

        // Set primary source table if not set
        if (string.IsNullOrEmpty(mapping.SourceTable) && mapping.SourceTables.Any())
        {
            mapping.SourceTable = mapping.SourceTables.First();
        }

        // Load columns for all source tables and cache them
        foreach (var sourceTable in mapping.SourceTables)
        {
            if (!tableColumnCache.ContainsKey(sourceTable))
            {
                try
                {
                    var columns = await GetTableColumnsAsync(sourceConnectionString, sourceTable);
                    tableColumnCache[sourceTable] = columns;
                }
                catch (Exception ex)
                {
                    errorMessage = $"Error loading columns from {sourceTable}: {ex.Message}";
                }
            }
        }

        CloseMultiSourceDialog();
        StateHasChanged();
    }

    private async Task OnSourceTableColumnChange(int tableIndex, int colIndex, string selectedSourceTable)
    {
        if (tableIndex < 0 || tableIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[tableIndex];
        if (colIndex < 0 || colIndex >= mapping.ColumnMappings.Count)
            return;

        var colMapping = mapping.ColumnMappings[colIndex];
        colMapping.SourceTable = selectedSourceTable;

        // Load columns for this table if not cached
        if (!string.IsNullOrEmpty(selectedSourceTable) && !tableColumnCache.ContainsKey(selectedSourceTable))
        {
            try
            {
                var columns = await GetTableColumnsAsync(sourceConnectionString, selectedSourceTable);
                tableColumnCache[selectedSourceTable] = columns;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                errorMessage = $"Error loading columns from {selectedSourceTable}: {ex.Message}";
            }
        }
    }

    private async Task CompareColumnTypes()
    {
        if (currentMappingIndex < 0 || currentMappingIndex >= tableMappings.Count)
            return;

        var mapping = tableMappings[currentMappingIndex];

        if (!mapping.ColumnMappings.Any())
        {
            errorMessage = "Please add column mappings first.";
            return;
        }

        isComparingColumnTypes = true;
        columnTypeResult = null;

        try
        {
            var sourceColumns = await ComparisonService.GetColumnTypesAsync(sourceConnectionString, mapping.SourceTable);
            var destColumns = await ComparisonService.GetColumnTypesAsync(destinationConnectionString, mapping.DestinationTable);

            columnTypeResult = ComparisonService.CompareColumnTypes(sourceColumns, destColumns, mapping.ColumnMappings);
        }
        catch (Exception ex)
        {
            errorMessage = $"Error comparing column types: {ex.Message}";
        }
        finally
        {
            isComparingColumnTypes = false;
        }
    }




    private async Task LoadTables()
    {
        isLoadingTables = true;
        errorMessage = string.Empty;

        try
        {
            sourceTables = await ComparisonService.GetTableNamesAsync(sourceConnectionString);
            destinationTables = await ComparisonService.GetTableNamesAsync(destinationConnectionString);

            // Don't auto-map - let user decide
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading tables: {ex.Message}";
        }
        finally
        {
            isLoadingTables = false;
        }
    }

    private void AutoMapTables()
    {
        tableMappings.Clear();

        var commonTables = sourceTables.Intersect(destinationTables).ToList();

        foreach (var table in commonTables)
        {
            tableMappings.Add(new TableMapping
            {
                SourceTable = table,
                DestinationTable = table,
                IsSelected = true
            });
        }

        var sourceOnlyTables = sourceTables.Except(destinationTables).ToList();
        foreach (var table in sourceOnlyTables)
        {
            tableMappings.Add(new TableMapping
            {
                SourceTable = table,
                DestinationTable = string.Empty,
                IsSelected = false
            });
        }

        var destOnlyTables = destinationTables.Except(sourceTables).ToList();
        foreach (var table in destOnlyTables)
        {
            tableMappings.Add(new TableMapping
            {
                SourceTable = string.Empty,
                DestinationTable = table,
                IsSelected = false
            });
        }
    }

    private void AddMapping()
    {
        tableMappings.Add(new TableMapping
        {
            SourceTable = string.Empty,
            DestinationTable = string.Empty,
            IsSelected = true
        });
    }

    private void RemoveUnselectedMappings()
    {
        tableMappings.RemoveAll(m => !m.IsSelected);
    }

    private void SelectAllTables()
    {
        foreach (var mapping in tableMappings)
        {
            mapping.IsSelected = true;
        }
    }

    private void DeselectAllTables()
    {
        foreach (var mapping in tableMappings)
        {
            mapping.IsSelected = false;
        }
    }

    private void ToggleAllTableSelection(ChangeEventArgs e)
    {
        var isChecked = (bool)(e.Value ?? false);
        foreach (var mapping in tableMappings)
        {
            mapping.IsSelected = isChecked;
        }
    }

    private async Task TestConnection(bool isSource)
    {
        var connectionString = isSource ? sourceConnectionString : destinationConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (isSource)
        {
            isTestingSource = true;
            sourceTestResult = null;
        }
        else
        {
            isTestingDest = true;
            destTestResult = null;
        }

        try
        {
            var result = await ComparisonService.TestConnectionAsync(connectionString);

            if (isSource)
                sourceTestResult = result;
            else
                destTestResult = result;
        }
        finally
        {
            if (isSource)
                isTestingSource = false;
            else
                isTestingDest = false;
        }
    }

    private async Task TestBothConnections()
    {
        if (string.IsNullOrWhiteSpace(sourceConnectionString) && string.IsNullOrWhiteSpace(destinationConnectionString))
        {
            return;
        }

        isTestingSource = true;
        isTestingDest = true;
        sourceTestResult = null;
        destTestResult = null;

        try
        {
            // Test both connections in parallel for better performance
            var tasks = new List<Task>();

            if (!string.IsNullOrWhiteSpace(sourceConnectionString))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        sourceTestResult = await ComparisonService.TestConnectionAsync(sourceConnectionString);
                    }
                    catch (Exception ex)
                    {
                        sourceTestResult = new ConnectionTestResult
                        {
                            IsSuccessful = false,
                            ErrorMessage = ex.Message
                        };
                    }
                }));
            }

            if (!string.IsNullOrWhiteSpace(destinationConnectionString))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        destTestResult = await ComparisonService.TestConnectionAsync(destinationConnectionString);
                    }
                    catch (Exception ex)
                    {
                        destTestResult = new ConnectionTestResult
                        {
                            IsSuccessful = false,
                            ErrorMessage = ex.Message
                        };
                    }
                }));
            }

            await Task.WhenAll(tasks);
            StateHasChanged();
        }
        finally
        {
            isTestingSource = false;
            isTestingDest = false;
        }
    }

    private async Task CompareSchemas()
    {
        errorMessage = string.Empty;
        comparisonResult = null;

        var selectedMappings = tableMappings
            .Where(m => m.IsSelected && !string.IsNullOrEmpty(m.SourceTable) && !string.IsNullOrEmpty(m.DestinationTable))
            .ToList();

        if (!selectedMappings.Any())
        {
            errorMessage = "Please select at least one table mapping with both source and destination tables specified.";
            return;
        }

        isComparing = true;

        try
        {
            var sourceTableNames = selectedMappings.Select(m => m.SourceTable).Distinct().ToList();
            var destTableNames = selectedMappings.Select(m => m.DestinationTable).Distinct().ToList();

            var sourceTableSchemas = await ComparisonService.GetTableSchemasAsync(sourceConnectionString, sourceTableNames);
            var destTableSchemas = await ComparisonService.GetTableSchemasAsync(destinationConnectionString, destTableNames);

            comparisonResult = ComparisonService.CompareMappedSchemas(sourceTableSchemas, destTableSchemas, selectedMappings);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isComparing = false;
        }
    }

    private async Task CompareData()
    {
        errorMessage = string.Empty;
        dataComparisonResults.Clear();

        var selectedMappings = tableMappings
            .Where(m => m.IsSelected && m.CompareData && !string.IsNullOrEmpty(m.SourceTable) && !string.IsNullOrEmpty(m.DestinationTable))
            .ToList();

        if (!selectedMappings.Any())
        {
            errorMessage = "Please select at least one table with 'Compare Data' enabled.";
            return;
        }

        isComparingData = true;

        try
        {
            foreach (var mapping in selectedMappings)
            {
                var result = await DataComparisonService.CompareTableDataAsync(
                    sourceConnectionString,
                    destinationConnectionString,
                    mapping);

                dataComparisonResults.Add(result);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isComparingData = false;
        }
    }

    private async Task DownloadMigrationSQL(DataComparisonResult result)
    {
        var mapping = tableMappings.FirstOrDefault(m => m.SourceTable == result.SourceTable && m.DestinationTable == result.DestinationTable);
        if (mapping == null) return;

        var sql = DataComparisonService.GenerateMigrationSQL(result, mapping);
        var fileName = $"Migration_{result.DestinationTable.Replace(".", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";

        var bytes = System.Text.Encoding.UTF8.GetBytes(sql);
        var base64 = Convert.ToBase64String(bytes);

        await JSRuntime.InvokeVoidAsync("eval", $@"
            const link = document.createElement('a');
            link.download = '{fileName}';
            link.href = 'data:text/plain;base64,{base64}';
            link.click();
        ");
    }

    private async Task CopyMigrationSQL(DataComparisonResult result)
    {
        var mapping = tableMappings.FirstOrDefault(m => m.SourceTable == result.SourceTable && m.DestinationTable == result.DestinationTable);
        if (mapping == null) return;

        var sql = DataComparisonService.GenerateMigrationSQL(result, mapping);

        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", sql);
    }

    // Excel Mapping Methods
    private async Task DownloadExcelTemplate()
    {
        var template = ExcelMappingService.GenerateSampleTemplate();
        var fileName = $"MappingTemplate_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        var base64 = Convert.ToBase64String(template);

        await JSRuntime.InvokeVoidAsync("eval", $@"
            const link = document.createElement('a');
            link.download = '{fileName}';
            link.href = 'data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,{base64}';
            link.click();
        ");
    }

    private async Task HandleExcelUpload(InputFileChangeEventArgs e)
    {
        isUploadingExcel = true;
        errorMessage = string.Empty;

        try
        {
            var file = e.File;
            if (file == null) return;

            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB max
            excelMappingConfig = await ExcelMappingService.ParseMappingExcelAsync(stream);
            excelGeneratedSQL = string.Empty;
            excelValidationReport = string.Empty;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error uploading Excel file: {ex.Message}";
        }
        finally
        {
            isUploadingExcel = false;
        }
    }

    private void GenerateExcelSQL()
    {
        if (excelMappingConfig == null)
        {
            errorMessage = "Please upload an Excel mapping file first.";
            return;
        }

        try
        {
            excelGeneratedSQL = ExcelMappingService.GenerateMigrationSQL(excelMappingConfig);
        }
        catch (Exception ex)
        {
            errorMessage = $"Error generating SQL: {ex.Message}";
        }
    }

    private void GenerateValidationReport()
    {
        if (excelMappingConfig == null)
        {
            errorMessage = "Please upload an Excel mapping file first.";
            return;
        }

        try
        {
            excelValidationReport = ExcelMappingService.GenerateValidationReport(excelMappingConfig);
        }
        catch (Exception ex)
        {
            errorMessage = $"Error generating validation report: {ex.Message}";
        }
    }

    private async Task DownloadExcelSQL()
    {
        if (string.IsNullOrEmpty(excelGeneratedSQL)) return;

        var fileName = $"ExcelMigration_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
        var bytes = System.Text.Encoding.UTF8.GetBytes(excelGeneratedSQL);
        var base64 = Convert.ToBase64String(bytes);

        await JSRuntime.InvokeVoidAsync("eval", $@"
            const link = document.createElement('a');
            link.download = '{fileName}';
            link.href = 'data:text/plain;base64,{base64}';
            link.click();
        ");
    }

    private async Task CopyExcelSQL()
    {
        if (string.IsNullOrEmpty(excelGeneratedSQL)) return;
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", excelGeneratedSQL);
    }

    private async Task DownloadAnalysisExcel()
    {
        if (excelMappingConfig == null || excelComparisonResult == null)
        {
            errorMessage = "No analysis results available to export.";
            return;
        }

        isExportingAnalysisExcel = true;
        errorMessage = string.Empty;
        StateHasChanged(); // Force UI update to show spinner

        try
        {
            // Run the Excel generation on a background task to allow UI to update
            var excelBytes = await Task.Run(() => ExcelMappingService.GenerateAnalysisExcel(
                excelMappingConfig, 
                excelComparisonResult,
                excelComparisonResult.DatatypeComparisons));

            var fileName = $"MappingAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var base64 = Convert.ToBase64String(excelBytes);

            await JSRuntime.InvokeVoidAsync("eval", $@"
                const link = document.createElement('a');
                link.download = '{fileName}';
                link.href = 'data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,{base64}';
                link.click();
            ");
        }
        catch (Exception ex)
        {
            errorMessage = $"Error exporting analysis to Excel: {ex.Message}";
        }
        finally
        {
            isExportingAnalysisExcel = false;
            StateHasChanged(); // Force UI update to hide spinner
        }
    }

    // Manual Mapping SQL Generation Methods
    private void GenerateManualMappingSQL()
    {
        var selectedMappings = tableMappings
            .Where(m => m.IsSelected && !string.IsNullOrEmpty(m.DestinationTable))
            .ToList();

        if (!selectedMappings.Any())
        {
            errorMessage = "Please select at least one table mapping with a destination table.";
            return;
        }

        try
        {
            var sqlBuilder = new System.Text.StringBuilder();
            sqlBuilder.AppendLine("-- Migration SQL Generated from Table Mappings");
            sqlBuilder.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sqlBuilder.AppendLine($"-- Total Mappings: {selectedMappings.Count}");
            sqlBuilder.AppendLine();

            foreach (var mapping in selectedMappings)
            {
                if (!mapping.ColumnMappings.Any())
                {
                    sqlBuilder.AppendLine($"-- WARNING: No column mappings defined for {mapping.DestinationTable}");
                    sqlBuilder.AppendLine();
                    continue;
                }

                sqlBuilder.AppendLine($"-- ============================================");
                sqlBuilder.AppendLine($"-- Mapping: {string.Join(", ", mapping.GetAllSourceTables())} → {mapping.DestinationTable}");
                sqlBuilder.AppendLine($"-- ============================================");
                sqlBuilder.AppendLine();

                // Generate INSERT statement
                var destColumns = mapping.ColumnMappings
                    .Where(cm => !string.IsNullOrEmpty(cm.DestinationColumn))
                    .Select(cm => $"[{cm.DestinationColumn}]")
                    .ToList();

                if (!destColumns.Any())
                {
                    sqlBuilder.AppendLine($"-- WARNING: No destination columns mapped for {mapping.DestinationTable}");
                    sqlBuilder.AppendLine();
                    continue;
                }

                sqlBuilder.AppendLine($"INSERT INTO {FormatTableName(mapping.DestinationTable)}");
                sqlBuilder.AppendLine($"    ({string.Join(", ", destColumns)})");
                sqlBuilder.AppendLine("SELECT");

                // Generate SELECT columns
                var selectColumns = new List<string>();
                foreach (var cm in mapping.ColumnMappings.Where(cm => !string.IsNullOrEmpty(cm.DestinationColumn)))
                {
                    if (string.IsNullOrEmpty(cm.SourceColumn))
                    {
                        // No source mapping - use NULL or default
                        selectColumns.Add($"    NULL AS [{cm.DestinationColumn}] -- No source mapping");
                    }
                    else
                    {
                        var sourceTablePrefix = !string.IsNullOrEmpty(cm.SourceTable)
                            ? GetTableAlias(cm.SourceTable) + "."
                            : "";
                        selectColumns.Add($"    {sourceTablePrefix}[{cm.SourceColumn}] AS [{cm.DestinationColumn}]");
                    }
                }

                sqlBuilder.AppendLine(string.Join($",{Environment.NewLine}", selectColumns));

                // Generate FROM clause
                var sourceTables = mapping.GetAllSourceTables().ToList();
                if (sourceTables.Any())
                {
                    var primaryTable = sourceTables.First();
                    sqlBuilder.AppendLine($"FROM {FormatTableName(primaryTable)} AS {GetTableAlias(primaryTable)}");

                    // Add JOINs for additional source tables
                    for (int i = 1; i < sourceTables.Count; i++)
                    {
                        var joinTable = sourceTables[i];
                        var joinAlias = GetTableAlias(joinTable);

                        // Find key columns for JOIN condition
                        var keyColumns = mapping.ColumnMappings
                            .Where(cm => cm.IsKey && cm.SourceTable == joinTable)
                            .ToList();

                        if (keyColumns.Any())
                        {
                            var joinConditions = keyColumns
                                .Select(cm => $"{GetTableAlias(primaryTable)}.[{cm.SourceColumn}] = {joinAlias}.[{cm.SourceColumn}]")
                                .ToList();

                            sqlBuilder.AppendLine($"    LEFT JOIN {FormatTableName(joinTable)} AS {joinAlias}");
                            sqlBuilder.AppendLine($"        ON {string.Join(" AND ", joinConditions)}");
                        }
                        else
                        {
                            sqlBuilder.AppendLine($"    CROSS JOIN {FormatTableName(joinTable)} AS {joinAlias} -- WARNING: No key columns defined, using CROSS JOIN");
                        }
                    }
                }
                else
                {
                    sqlBuilder.AppendLine("-- WARNING: No source tables defined");
                }

                // Add WHERE clause to exclude existing records
                var keyMappings = mapping.ColumnMappings.Where(cm => cm.IsKey).ToList();
                if (keyMappings.Any())
                {
                    sqlBuilder.AppendLine("WHERE NOT EXISTS (");
                    sqlBuilder.AppendLine($"    SELECT 1 FROM {FormatTableName(mapping.DestinationTable)} dest");

                    var whereConditions = keyMappings
                        .Where(cm => !string.IsNullOrEmpty(cm.SourceColumn) && !string.IsNullOrEmpty(cm.DestinationColumn))
                        .Select(cm =>
                        {
                            var sourceTablePrefix = !string.IsNullOrEmpty(cm.SourceTable)
            ? GetTableAlias(cm.SourceTable) + "."
            : "";
                            return $"dest.[{cm.DestinationColumn}] = {sourceTablePrefix}[{cm.SourceColumn}]";
                        })
                        .ToList();

                    if (whereConditions.Any())
                    {
                        sqlBuilder.AppendLine($"    WHERE {string.Join(" AND ", whereConditions)}");
                    }

                    sqlBuilder.AppendLine(");");
                }
                else
                {
                    sqlBuilder.AppendLine(";");
                    sqlBuilder.AppendLine();
                    sqlBuilder.AppendLine("-- WARNING: No key columns marked. All source rows will be inserted without duplicate checking.");
                }

                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine($"-- Records inserted: @@ROWCOUNT");
                sqlBuilder.AppendLine();
            }

            sqlBuilder.AppendLine("-- ============================================");
            sqlBuilder.AppendLine("-- End of Migration SQL");
            sqlBuilder.AppendLine("-- ============================================");

            manualMappingGeneratedSQL = sqlBuilder.ToString();
            errorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error generating SQL: {ex.Message}";
            manualMappingGeneratedSQL = string.Empty;
        }
    }

    private string GetTableAlias(string tableName)
    {
        // Create simple alias from table name
        // e.g., "dbo.Users" -> "u", "[dbo].[OrderDetails]" -> "od"

        // Remove brackets first
        var cleanName = tableName.Replace("[", "").Replace("]", "");
        var parts = cleanName.Split('.');
        var name = parts.Length > 1 ? parts[1] : parts[0];

        // Take first letter of each capital letter or first letter if no capitals
        var alias = string.Concat(name.Where(char.IsUpper).Select(char.ToLower));
        if (string.IsNullOrEmpty(alias))
        {
            alias = name.Substring(0, Math.Min(2, name.Length)).ToLower();
        }

        return alias;
    }

    private string FormatTableName(string tableName)
    {
        // Ensures table name is properly bracketed for SQL Server
        // e.g., "dbo.Users" -> "[dbo].[Users]"
        // e.g., "[dbo].[Users]" -> "[dbo].[Users]" (unchanged)

        if (string.IsNullOrWhiteSpace(tableName))
            return tableName;

        // If already fully bracketed, return as-is
        if (tableName.StartsWith("[") && tableName.Contains("].["))
            return tableName;

        // Remove any existing brackets
        var cleanName = tableName.Replace("[", "").Replace("]", "");

        // Split by dot and wrap each part in brackets
        var parts = cleanName.Split('.');
        return string.Join(".", parts.Select(p => $"[{p}]"));
    }

    private async Task DownloadManualMappingSQL()
    {
        if (string.IsNullOrEmpty(manualMappingGeneratedSQL)) return;

        var fileName = $"ManualMigration_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
        var bytes = System.Text.Encoding.UTF8.GetBytes(manualMappingGeneratedSQL);
        var base64 = Convert.ToBase64String(bytes);

        await JSRuntime.InvokeVoidAsync("eval", $@"
            const link = document.createElement('a');
            link.download = '{fileName}';
            link.href = 'data:text/plain;base64,{base64}';
            link.click();
        ");
    }

    private async Task CopyManualMappingSQL()
    {
        if (string.IsNullOrEmpty(manualMappingGeneratedSQL)) return;
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", manualMappingGeneratedSQL);
    }

    private async Task LoadExcelMappingsToTables()
    {
        if (excelMappingConfig == null)
        {
            errorMessage = "Please upload an Excel mapping file first.";
            return;
        }

        try
        {
            // Load tables if not already loaded
            if (!sourceTables.Any() || !destinationTables.Any())
            {
                sourceTables = await ComparisonService.GetTableNamesAsync(sourceConnectionString);
                destinationTables = await ComparisonService.GetTableNamesAsync(destinationConnectionString);
            }

            // Create table mappings from Excel configuration
            tableMappings.Clear();

            foreach (var tableGroup in excelMappingConfig.GroupedByTable)
            {
                var destTableName = tableGroup.Key;

                // Get unique source tables for this destination table (excluding N/A and null values)
                var uniqueSourceTables = tableGroup.Value
                    .Where(m => !string.IsNullOrWhiteSpace(m.OldTableName) &&
                               !m.OldTableName.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.OldTableName)
                    .Distinct()
                    .ToList();

                // Check if there are any columns without source mapping (N/A or null)
                var nullMappings = tableGroup.Value
                    .Where(m => string.IsNullOrWhiteSpace(m.OldTableName) ||
                               m.OldTableName.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                               string.IsNullOrWhiteSpace(m.OldColumn) ||
                               m.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // If no source tables specified, create empty mapping
                if (!uniqueSourceTables.Any() && !nullMappings.Any())
                {
                    tableMappings.Add(new TableMapping
                    {
                        SourceTable = string.Empty,
                        DestinationTable = destTableName,
                        IsSelected = true,
                        CompareData = false
                    });
                    continue;
                }

                // Create ONE mapping per destination table with ALL source tables
                var mapping = new TableMapping
                {
                    SourceTable = uniqueSourceTables.Any() ? uniqueSourceTables.First() : string.Empty, // Primary source table
                    SourceTables = new List<string>(uniqueSourceTables), // All source tables
                    DestinationTable = destTableName,
                    IsSelected = true,
                    CompareData = false
                };

                // Add ALL column mappings from ALL source tables
                var allColumnMappings = new List<ColumnMapping>();

                // Add mapped columns from source tables
                foreach (var sourceTableName in uniqueSourceTables)
                {
                    var columnMappingsForSource = tableGroup.Value
                        .Where(m => m.OldTableName == sourceTableName &&
                                   !string.IsNullOrWhiteSpace(m.OldColumn) &&
                                   !m.OldColumn.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                        .Select(m => new ColumnMapping
                        {
                            SourceTable = sourceTableName, // Set source table for each column
                            SourceColumn = m.OldColumn,
                            DestinationColumn = m.NewColumn,
                            IsKey = false // Keys need to be set manually or detected
                        })
                        .ToList();

                    allColumnMappings.AddRange(columnMappingsForSource);
                }

                // Add NULL mappings for columns without source (N/A or null values)
                foreach (var nullMapping in nullMappings)
                {
                    allColumnMappings.Add(new ColumnMapping
                    {
                        SourceTable = string.Empty, // Empty = will generate NULL
                        SourceColumn = string.Empty, // Empty = will generate NULL
                        DestinationColumn = nullMapping.NewColumn,
                        IsKey = false
                    });
                }

                mapping.ColumnMappings = allColumnMappings;
                tableMappings.Add(mapping);
            }

            errorMessage = string.Empty;

            var totalSources = tableMappings.Sum(m => m.SourceTables.Count);
            var multiSourceCount = tableMappings.Count(m => m.SourceTables.Count > 1);
            var nullColumnCount = tableMappings.Sum(m => m.ColumnMappings.Count(cm => string.IsNullOrEmpty(cm.SourceColumn)));

            excelLoadMessage = $"✅ Loaded {tableMappings.Count} table mapping(s) from Excel with {tableMappings.Sum(m => m.ColumnMappings.Count)} column mapping(s)!";

            if (multiSourceCount > 0)
            {
                excelLoadMessage += $" ({multiSourceCount} mapping(s) with multiple source tables consolidated)";
            }

            if (nullColumnCount > 0)
            {
                excelLoadMessage += $" ({nullColumnCount} column(s) will be set to NULL)";
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading Excel mappings to table mappings: {ex.Message}";
        }
    }

    private async Task CompareExcelMappings()
    {
        if (excelMappingConfig == null)
        {
            errorMessage = "No Excel mappings loaded.";
            return;
        }

        isComparingExcelMappings = true;
        excelComparisonResult = new MappingComparisonResult();

        try
        {
            excelComparisonResult = await MappingComparisonService.CompareMappingsAsync(excelMappingConfig, 
                sourceConnectionString, destinationConnectionString);
        }
        catch (Exception ex)
        {
            errorMessage = $"Error analyzing Excel mappings: {ex.Message}";
        }
        finally
        {
            isComparingExcelMappings = false;
        }
    }
}