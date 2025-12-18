namespace CSPGuardian.Core;

public class ScanResult
{
    public List<Violation> Violations { get; set; } = new();
    public int TotalFilesScanned { get; set; }
    public int ViolationsFound => Violations.Count;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class Violation
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public ViolationType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string? SuggestedFix { get; set; }
    public string? Hash { get; set; }
}

public enum ViolationType
{
    InlineScript,
    InlineStyle,
    EventHandler,
    DynamicInline,
    LegacyWebResource,
    ViewStateEmbedded
}

