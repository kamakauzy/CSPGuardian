using CSPGuardian.Core;
using CSPGuardian.Reporting;
using Xunit;

namespace CSPGuardian.Tests;

public class ReporterTests
{
    [Fact]
    public void RenderMarkdownReport_IncludesSummaryAndGuidance()
    {
        var reporter = new Reporter(new ScanOptions
        {
            Framework = ScanFrameworks.LegacyDotNet,
            Cleanup = CleanupStrategies.Nonce,
            LegacyMode = true
        });

        var result = new ScanResult
        {
            TotalFilesScanned = 2,
            TotalFilesSkipped = 1,
            SkippedPaths = ["/tmp/skipped"],
            Violations =
            [
                new Violation
                {
                    FilePath = "/tmp/index.html",
                    LineNumber = 4,
                    Type = ViolationType.StyleAttribute,
                    Content = "color:red",
                    Severity = "medium",
                    AttributeName = "style",
                    SuggestedFix = "Move inline styles into a stylesheet."
                }
            ]
        };

        var markdown = reporter.RenderMarkdownReport(result);

        Assert.Contains("## Skipped Paths", markdown);
        Assert.Contains("## Violations Summary", markdown);
        Assert.Contains("StyleAttribute", markdown);
        Assert.Contains("Nonce Implementation Guidance", markdown);
        Assert.Contains("Legacy .NET Guidance", markdown);
    }

    [Fact]
    public void RenderJsonAndCsvReport_IncludeViolationFields()
    {
        var reporter = new Reporter(new ScanOptions { ReportFormat = ReportFormats.Json });
        var result = new ScanResult
        {
            Violations =
            [
                new Violation
                {
                    FilePath = "/tmp/index.html",
                    LineNumber = 1,
                    Type = ViolationType.InlineScript,
                    Content = "alert('x');",
                    Severity = "high",
                    Hash = "sha384-value",
                    SuggestedFix = "Add hash to CSP.",
                    GeneratedAssetPath = "/tmp/index.script0.js"
                }
            ]
        };

        var json = reporter.RenderJsonReport(result);
        var csv = reporter.RenderCsvReport(result);

        Assert.Contains("\"Violations\"", json);
        Assert.Contains("sha384-value", json);
        Assert.Contains("GeneratedAssetPath", csv);
        Assert.Contains("/tmp/index.script0.js", csv);
    }
}
