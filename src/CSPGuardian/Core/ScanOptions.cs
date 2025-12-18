namespace CSPGuardian.Core;

public class ScanOptions
{
    public string Path { get; set; } = string.Empty;
    public string Framework { get; set; } = "modern-dotnet";
    public string Cleanup { get; set; } = "none";
    public string Output { get; set; } = "policy.csp";
    public bool DryRun { get; set; } = false;
    public bool LegacyMode { get; set; } = false;
    public bool CiMode { get; set; } = false;
    public string ReportFormat { get; set; } = "md";
}

