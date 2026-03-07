namespace CSPGuardian.Adapters;

/// <summary>
/// Adapter for JavaScript framework handling (React, Angular, Vue, etc.)
/// Supports both modern (Webpack/Vite) and legacy (jQuery) scenarios
/// </summary>
public class JsAdapter
{
    private readonly string _framework;

    public JsAdapter(string framework)
    {
        _framework = framework;
    }

    public List<Core.Violation> Process(string filePath, string content)
    {
        var violations = new List<Core.Violation>();

        // Modern frameworks: Check for Webpack/Vite inline patterns
        if (_framework == Core.ScanFrameworks.JsModern)
        {
            violations.AddRange(ScanModernJs(content, filePath));
        }
        // Legacy: Check for jQuery.ready, inline event handlers
        else if (_framework == Core.ScanFrameworks.JsLegacy)
        {
            violations.AddRange(ScanLegacyJs(content, filePath));
        }

        return violations;
    }

    private List<Core.Violation> ScanModernJs(string content, string filePath)
    {
        var violations = new List<Core.Violation>();

        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     content,
                     @"__webpack_require__",
                     System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            violations.Add(new Core.Violation
            {
                FilePath = filePath,
                LineNumber = Core.Scanner.GetLineNumberFromIndex(content, match.Index),
                Type = Core.ViolationType.DynamicInline,
                Content = "Webpack inline pattern detected",
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "low"
            });
        }

        return violations;
    }

    private List<Core.Violation> ScanLegacyJs(string content, string filePath)
    {
        var violations = new List<Core.Violation>();

        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     content,
                     @"\$\(document\)\.ready\s*\([^)]*function",
                     System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            violations.Add(new Core.Violation
            {
                FilePath = filePath,
                LineNumber = Core.Scanner.GetLineNumberFromIndex(content, match.Index),
                Type = Core.ViolationType.DynamicInline,
                Content = "jQuery.ready with inline function detected",
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "medium"
            });
        }

        return violations;
    }
}

