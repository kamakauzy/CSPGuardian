using System.Text;
using System.Text.Json;
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
        var reportPath = $"report.{_options.ReportFormat}";

        switch (_options.ReportFormat.ToLowerInvariant())
        {
            case "json":
                await GenerateJsonReportAsync(scanResult, reportPath);
                break;
            case "csv":
                await GenerateCsvReportAsync(scanResult, reportPath);
                break;
            case "md":
            default:
                await GenerateMarkdownReportAsync(scanResult, reportPath);
                break;
        }
    }

    private async Task GenerateMarkdownReportAsync(ScanResult scanResult, string reportPath)
    {
        var report = new StringBuilder();
        
        report.AppendLine("# CSPGuardian Scan Report");
        report.AppendLine();
        report.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"**Framework:** {_options.Framework}");
        report.AppendLine($"**Files Scanned:** {scanResult.TotalFilesScanned}");
        report.AppendLine($"**Violations Found:** {scanResult.ViolationsFound}");
        report.AppendLine();

        if (scanResult.ViolationsFound == 0)
        {
            report.AppendLine("✅ **No CSP violations detected!**");
            await File.WriteAllTextAsync(reportPath, report.ToString());
            return;
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

        await File.WriteAllTextAsync(reportPath, report.ToString());
    }

    private async Task GenerateJsonReportAsync(ScanResult scanResult, string reportPath)
    {
        var json = JsonSerializer.Serialize(scanResult, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(reportPath, json);
    }

    private async Task GenerateCsvReportAsync(ScanResult scanResult, string reportPath)
    {
        var csv = new StringBuilder();
        csv.AppendLine("FilePath,LineNumber,Type,Severity,Content,Hash,SuggestedFix");

        foreach (var violation in scanResult.Violations)
        {
            csv.AppendLine($"{EscapeCsv(violation.FilePath)},{violation.LineNumber},{violation.Type},{violation.Severity},{EscapeCsv(violation.Content)},{EscapeCsv(violation.Hash ?? "")},{EscapeCsv(violation.SuggestedFix ?? "")}");
        }

        await File.WriteAllTextAsync(reportPath, csv.ToString());
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

