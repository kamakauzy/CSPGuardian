using System.Text;
using System.Text.Json;
using CSPGuardian.Adapters;
using CSPGuardian.Core;

namespace CSPGuardian.Reporting;

public class Reporter
{
    private readonly ScanOptions _options;

    public Reporter(ScanOptions options)
    {
        _options = options;
    }

    public async Task GenerateReportAsync(ScanResult scanResult)
    {
        var reportPath = _options.GetReportOutputPath();

        switch (_options.ReportFormat)
        {
            case ReportFormats.Json:
                await File.WriteAllTextAsync(reportPath, RenderJsonReport(scanResult));
                break;
            case ReportFormats.Csv:
                await File.WriteAllTextAsync(reportPath, RenderCsvReport(scanResult));
                break;
            case ReportFormats.Markdown:
            default:
                await File.WriteAllTextAsync(reportPath, RenderMarkdownReport(scanResult));
                break;
        }
    }

    public string RenderMarkdownReport(ScanResult scanResult)
    {
        var report = new StringBuilder();
        
        report.AppendLine("# CSPGuardian Scan Report");
        report.AppendLine();
        report.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"**Framework:** {_options.Framework}");
        report.AppendLine($"**Cleanup Strategy:** {_options.Cleanup}");
        report.AppendLine($"**Files Scanned:** {scanResult.TotalFilesScanned}");
        report.AppendLine($"**Files Skipped:** {scanResult.TotalFilesSkipped}");
        report.AppendLine($"**Violations Found:** {scanResult.ViolationsFound}");
        report.AppendLine($"**Dry Run:** {_options.DryRun}");
        report.AppendLine();

        if (scanResult.SkippedPaths.Count > 0)
        {
            report.AppendLine("## Skipped Paths");
            report.AppendLine();

            foreach (var skippedPath in scanResult.SkippedPaths)
            {
                report.AppendLine($"- `{skippedPath}`");
            }

            report.AppendLine();
        }

        if (scanResult.ViolationsFound == 0)
        {
            report.AppendLine("✅ **No CSP violations detected!**");
            return report.ToString();
        }

        report.AppendLine("## Violations Summary");
        report.AppendLine();

        var byType = scanResult.Violations.GroupBy(v => v.Type);
        foreach (var group in byType)
        {
            report.AppendLine($"- **{group.Key}**: {group.Count()}");
        }

        report.AppendLine();
        report.AppendLine("## Detailed Violations");
        report.AppendLine();

        foreach (var violation in scanResult.Violations)
        {
            report.AppendLine($"### {violation.Type} - {Path.GetFileName(violation.FilePath)}");
            report.AppendLine();
            report.AppendLine($"- **File:** `{violation.FilePath}`");
            report.AppendLine($"- **Line:** {violation.LineNumber}");
            report.AppendLine($"- **Severity:** {violation.Severity}");
            report.AppendLine($"- **Content:** `{violation.Content}`");

            if (!string.IsNullOrEmpty(violation.AttributeName))
            {
                report.AppendLine($"- **Attribute:** `{violation.AttributeName}`");
            }
            
            if (!string.IsNullOrEmpty(violation.Hash))
            {
                report.AppendLine($"- **Hash:** `{violation.Hash}`");
            }

            if (!string.IsNullOrEmpty(violation.SuggestedFix))
            {
                report.AppendLine($"- **Suggested Fix:** {violation.SuggestedFix}");
            }

            report.AppendLine();
        }

        if (_options.Cleanup == CleanupStrategies.Nonce)
        {
            var adapter = new DotNetAdapter(_options.Framework, _options.LegacyMode);
            report.AppendLine("## Nonce Implementation Guidance");
            report.AppendLine();
            report.AppendLine("```csharp");
            report.AppendLine(adapter.GenerateNonceMiddleware());
            report.AppendLine("```");
            report.AppendLine();
        }

        if (_options.LegacyMode || _options.Framework == ScanFrameworks.LegacyDotNet)
        {
            var adapter = new DotNetAdapter(_options.Framework, _options.LegacyMode);
            report.AppendLine("## Legacy .NET Guidance");
            report.AppendLine();
            report.AppendLine("```text");
            report.AppendLine(adapter.GetMigrationSuggestion());
            report.AppendLine("```");
            report.AppendLine();
        }

        return report.ToString();
    }

    public string RenderJsonReport(ScanResult scanResult)
    {
        return JsonSerializer.Serialize(scanResult, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public string RenderCsvReport(ScanResult scanResult)
    {
        var csv = new StringBuilder();
        csv.AppendLine("FilePath,LineNumber,Type,Severity,AttributeName,Content,Hash,SuggestedFix,GeneratedAssetPath");

        foreach (var violation in scanResult.Violations)
        {
            csv.AppendLine(
                $"{EscapeCsv(violation.FilePath)},{violation.LineNumber},{violation.Type},{violation.Severity},{EscapeCsv(violation.AttributeName ?? "")},{EscapeCsv(violation.Content)},{EscapeCsv(violation.Hash ?? "")},{EscapeCsv(violation.SuggestedFix ?? "")},{EscapeCsv(violation.GeneratedAssetPath ?? "")}");
        }

        return csv.ToString();
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

