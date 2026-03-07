namespace CSPGuardian.Core;

public class ScanResult
{
    public List<Violation> Violations { get; set; } = new();
    public int TotalFilesScanned { get; set; }
    public int TotalFilesSkipped { get; set; }
    public int ViolationsFound => Violations.Count;
    public List<string> SkippedPaths { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class Violation
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public ViolationType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? RawContent { get; set; }
    public string Severity { get; set; } = "medium";
    public string? AttributeName { get; set; }
    public string? OriginalText { get; set; }
    public int SourceIndex { get; set; } = -1;
    public int SourceLength { get; set; }
    public string? SuggestedFix { get; set; }
    public string? Hash { get; set; }
    public string? GeneratedAssetPath { get; set; }
}

public enum ViolationType
{
    InlineScript,
    InlineStyle,
    StyleAttribute,
    EventHandler,
    JavaScriptUrl,
    DynamicInline,
    LegacyWebResource,
    ViewStateEmbedded
}

