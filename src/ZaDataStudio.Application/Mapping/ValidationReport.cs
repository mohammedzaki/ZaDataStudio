using System.Text;

namespace ZaDataStudio.Application.Mapping;

/// <summary>
/// Validation report containing errors and warnings from mapping validation
/// </summary>
public class ValidationReport
{
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => !Errors.Any();

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Mapping Validation Report ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (IsValid)
        {
            sb.AppendLine("✓ No errors found");
        }
        else
        {
            sb.AppendLine($"✗ {Errors.Count} error(s) found:");
            foreach (var error in Errors)
            {
                sb.AppendLine($"  - {error}");
            }
        }

        sb.AppendLine();

        if (Warnings.Any())
        {
            sb.AppendLine($"⚠ {Warnings.Count} warning(s):");
            foreach (var warning in Warnings)
            {
                sb.AppendLine($"  - {warning}");
            }
        }

        return sb.ToString();
    }
}
