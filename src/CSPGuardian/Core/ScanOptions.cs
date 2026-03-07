namespace CSPGuardian.Core;

public class ScanOptions
{
    private static readonly string[] DefaultExcludedDirectories = [".git", "bin", "obj", "node_modules"];

    public string Path { get; set; } = string.Empty;
    public string Framework { get; set; } = ScanFrameworks.ModernDotNet;
    public string Cleanup { get; set; } = CleanupStrategies.None;
    public string? Output { get; set; } = "policy.csp";
    public string? ReportOutput { get; set; }
    public bool DryRun { get; set; }
    public bool LegacyMode { get; set; }
    public bool CiMode { get; set; }
    public string ReportFormat { get; set; } = ReportFormats.Markdown;
    public List<string> ExcludedDirectories { get; set; } = [];

    public void Normalize()
    {
        if (!string.IsNullOrWhiteSpace(Path))
        {
            Path = System.IO.Path.GetFullPath(Path.Trim());
        }

        Framework = NormalizeValue(Framework, ScanFrameworks.ModernDotNet);
        Cleanup = NormalizeValue(Cleanup, CleanupStrategies.None);
        ReportFormat = NormalizeValue(ReportFormat, ReportFormats.Markdown);

        if (!string.IsNullOrWhiteSpace(Output))
        {
            Output = System.IO.Path.GetFullPath(Output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(ReportOutput))
        {
            ReportOutput = System.IO.Path.GetFullPath(ReportOutput.Trim());
        }

        ExcludedDirectories = ExcludedDirectories
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string GetReportOutputPath()
    {
        var reportOutput = ReportOutput;
        if (string.IsNullOrWhiteSpace(reportOutput))
        {
            reportOutput = $"report.{ReportFormat}";
        }

        return System.IO.Path.GetFullPath(reportOutput);
    }

    public IReadOnlyCollection<string> GetExcludedDirectories()
    {
        return DefaultExcludedDirectories
            .Concat(ExcludedDirectories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }
}

public static class ScanFrameworks
{
    public const string ModernDotNet = "modern-dotnet";
    public const string LegacyDotNet = "legacy-dotnet";
    public const string Static = "static";
    public const string JsModern = "js-modern";
    public const string JsLegacy = "js-legacy";

    public static readonly string[] All =
    [
        ModernDotNet,
        LegacyDotNet,
        Static,
        JsModern,
        JsLegacy
    ];
}

public static class CleanupStrategies
{
    public const string None = "none";
    public const string Externalize = "externalize";
    public const string Hash = "hash";
    public const string Nonce = "nonce";

    public static readonly string[] All =
    [
        None,
        Externalize,
        Hash,
        Nonce
    ];
}

public static class ReportFormats
{
    public const string Markdown = "md";
    public const string Json = "json";
    public const string Csv = "csv";

    public static readonly string[] All =
    [
        Markdown,
        Json,
        Csv
    ];
}

