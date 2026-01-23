# ZaDataStudio - SQL Server Schema Comparison Tool

A powerful **Blazor Server** application for comparing SQL Server database schemas and migrating data between databases. Built with **.NET 10** and designed for database administrators, developers, and data migration specialists.

---

## 🚀 Features

### 1. **Schema Comparison**
- Compare table structures between source and destination databases
- Identify differences in columns, data types, nullability, and constraints
- Visual diff display with detailed breakdowns
- Support for custom table-to-table mappings

### 2. **Data Comparison & Migration**
- Row-by-row data comparison
- Intelligent lookup table detection
- Generate INSERT, UPDATE, and DELETE SQL statements
- Distinct value analysis for reference data validation
- Safe-by-default DELETE statements (commented out)

### 3. **Excel-Based Mapping**
- Define complex data mappings using Excel templates
- Single-sheet format with 12 columns for complete metadata
- Upload and parse Excel mapping files
- Generate migration SQL directly from Excel
- Load Excel mappings into interactive UI for review/editing

### 4. **Column Mapping**
- Map columns when names differ between databases
- Mark key columns for accurate data comparison
- Compare datatypes, lengths, precision, and scale
- Dropdown-based column selection
- Auto-map matching column names

### 5. **Session Management**
- Save and load comparison configurations
- Store connection strings and table mappings
- Browser-based localStorage persistence
- Up to 20 most recent sessions retained

### 6. **Excel Mapping Analysis** ⭐ NEW!
- Validate lookup column values before migration
- Compare datatypes and detect potential data loss
- Identify mismatched values in reference data
- Check for truncation risks and precision loss

---

## 📋 Prerequisites

- **.NET 10 SDK** or later
- **SQL Server** (source and destination databases)
- Modern web browser (Chrome, Edge, Firefox, Safari)
- **Visual Studio 2022** or **VS Code** (for development)

---

## 🛠️ Installation

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/ZaDataStudio.git
cd ZaDataStudio
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build the Project

```bash
dotnet build
```

### 4. Run the Application

```bash
cd ZaDataStudio.Web
dotnet run
```

The application will start at `https://localhost:5001` (or the port shown in console).

---

## 📖 Usage Guide

### Quick Start: Schema Comparison

1. **Enter Connection Strings**
   - Source: Your source database connection string
   - Destination: Your target database connection string

2. **Test Connections**
   - Click "Test Connection" for both servers
   - Verify successful connection before proceeding

3. **Load Tables**
   - Click "Load Tables" to retrieve table lists
   - Use "Auto-Map Matching Names" or manually map tables

4. **Compare**
   - Click "Compare Schemas" to analyze structural differences
   - Review detailed diff results

### Excel-Based Migration

1. **Download Template**
   - Click "Download Excel Template"
   - Open in Excel/Google Sheets

2. **Fill in Mappings**
   - Define source and destination tables
   - Map columns and specify datatypes
   - Mark lookup columns and approval status

3. **Upload & Analyze**
   - Upload completed Excel file
   - Click "Analyze Excel Mappings" to validate
   - Review lookup values and datatype compatibility

4. **Generate SQL**
   - Click "Generate Migration SQL" for direct SQL export
   - OR "Load to Table Mappings" to edit in UI
   - Download or copy migration scripts

### Data Comparison & Migration

1. **Enable Data Comparison**
   - Toggle "Compare Data" for tables needing migration
   - Ensure column mappings are configured

2. **Mark Key Columns**
   - Open column mapping dialog
   - Check "Key Column" for primary keys

3. **Compare Data**
   - Click "Compare Data & Generate SQL"
   - Review rows needing INSERT, UPDATE, or DELETE

4. **Execute Migration**
   - Download generated SQL scripts
   - Test in non-production environment
   - Execute INSERT/UPDATE statements
   - Manually review DELETE statements before execution

---

## 🎯 Key Components

### Services

- **`SqlServerComparisonService`** - Schema comparison and metadata retrieval
- **`DataComparisonService`** - Row-by-row data comparison and SQL generation
- **`ExcelMappingService`** - Excel template generation and parsing
- **`SessionPersistenceService`** - Browser localStorage for session management

### Pages

- **`SchemaComparison.razor`** - Main comparison interface
- **`Home.razor`** - Landing page

---

## 📊 Excel Template Format

### DataMapping Sheet (12 Columns)

| # | Column | Description | Example |
|---|--------|-------------|---------|
| 1 | New Table Name | Destination table | `dbo.destination` |
| 2 | New Column | Destination column | `DestinationId` |
| 3 | New DataType | Destination type | `INT` |
| 4 | New Column Nullable | Can be NULL? | `NO` |
| 5 | Has lookup | Requires lookup? | `YES` |
| 6 | New Column Description | Documentation | `Primary key` |
| 7 | Old System Table Name | Source table | `OldSystem.Person` |
| 8 | Old Column | Source column/expression | `PersonId` |
| 9 | Old DataType | Source type | `INT` |
| 10 | Old Column Nullable | Source allows NULL? | `NO` |
| 11 | Mapping Status | Approval status | `Approved` |
| 12 | Notes | Implementation notes | `Direct mapping` |

---

## 🔒 Security Considerations

- ⚠️ **Connection strings stored in browser localStorage**
- ⚠️ **Not suitable for production connection strings with sensitive credentials**
- ✅ Use Windows Authentication or Azure AD when possible
- ✅ Test migration scripts in non-production environments first
- ✅ Review all DELETE statements before execution
- ✅ Always backup databases before migration

---

## 🏗️ Architecture

### Technology Stack

- **Frontend**: Blazor Server (.NET 10)
- **UI Framework**: Bootstrap 5 with Bootstrap Icons
- **Data Access**: Microsoft.Data.SqlClient
- **Excel Processing**: ClosedXML
- **Storage**: Browser localStorage (via JSInterop)

### Design Patterns

- **Service Layer**: Separation of concerns with dedicated services
- **Component-Based UI**: Blazor components for reusability
- **Async/Await**: All database operations are asynchronous
- **Error Handling**: Try-catch with user-friendly error messages

---

## 📚 Documentation

See the `/docs/` directory for detailed feature documentation:

- **`SchemaComparison_Usage.md`** - Complete usage guide
- **`ExcelMapping_SingleSheet_Guide.md`** - Excel template guide
- **`Excel_Analysis_Feature.md`** - Mapping analysis documentation
- **`Excel_To_Table_Mappings_Feature.md`** - Load to UI feature guide

---

## 🐛 Troubleshooting

### Common Issues

**Connection Fails**
- Verify SQL Server is running and accessible
- Check firewall rules
- Ensure connection string format is correct
- Test with SQL Server Management Studio first

**Excel Upload Error: "Synchronous reads not supported"**
- ✅ Fixed in latest version (async stream reading)
- Update to latest commit if seeing this error

**Column Mappings Don't Work**
- Ensure "Load Columns" is clicked first
- Verify table names match database exactly (case-sensitive)
- Check that columns exist in both tables

**DataReader Already Open Error**
- ✅ Fixed in latest version (proper using blocks)
- Update to latest commit if seeing this error

---

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Development Setup

```bash
# Clone your fork
git clone https://github.com/yourusername/ZaDataStudio.git

# Create a branch
git checkout -b feature/my-new-feature

# Make changes and test
dotnet build
dotnet test

# Commit and push
git add .
git commit -m "Description of changes"
git push origin feature/my-new-feature
```

---

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **ClosedXML** - Excel file processing
- **Bootstrap** - UI framework
- **Bootstrap Icons** - Icon library
- **.NET Team** - For the amazing Blazor framework

---

## 📧 Contact

For questions, issues, or suggestions:

- **GitHub Issues**: [Create an issue](https://github.com/mohammedzaki/ZaDataStudio/issues)
- **Email**: mohammed.elsayed.zaki@outlook.com

---

## 🗺️ Roadmap

### Planned Features

- [ ] **Azure SQL Database support**
- [ ] **PostgreSQL support**
- [ ] **MySQL support**
- [ ] **Database synchronization automation**
- [ ] **Scheduled comparison jobs**
- [ ] **Email notifications for differences**
- [ ] **Export comparison results to Excel**
- [ ] **Multi-database comparison (compare 3+ databases)**
- [ ] **Rollback script generation**
- [ ] **Integration with Azure DevOps**
- [ ] **Docker containerization**

---

## ⭐ Star This Project

If you find this tool useful, please consider giving it a star on GitHub!

---

**Version**: 1.0.0  
**Last Updated**: 2024  
**Status**: Active Development
