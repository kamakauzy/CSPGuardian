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

    /// <summary>
    /// Process JavaScript files for framework-specific inline patterns
    /// </summary>
    public async Task<List<Core.Violation>> ProcessAsync(string filePath, string content)
    {
        var violations = new List<Core.Violation>();

        // Modern frameworks: Check for Webpack/Vite inline patterns
        if (_framework == "js-modern")
        {
            violations.AddRange(ScanModernJs(content, filePath));
        }
        // Legacy: Check for jQuery.ready, inline event handlers
        else if (_framework == "js-legacy")
        {
            violations.AddRange(ScanLegacyJs(content, filePath));
        }

        return violations;
    }

    private List<Core.Violation> ScanModernJs(string content, string filePath)
    {
        var violations = new List<Core.Violation>();

        // Check for Webpack inline patterns
        if (System.Text.RegularExpressions.Regex.IsMatch(content, @"__webpack_require__", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            violations.Add(new Core.Violation
            {
                FilePath = filePath,
                Type = Core.ViolationType.DynamicInline,
                Content = "Webpack inline pattern detected",
                Severity = "low"
            });
        }

        return violations;
    }

    private List<Core.Violation> ScanLegacyJs(string content, string filePath)
    {
        var violations = new List<Core.Violation>();

        // Check for jQuery.ready with inline code
        if (System.Text.RegularExpressions.Regex.IsMatch(content, @"\$\(document\)\.ready\s*\([^)]*function", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            violations.Add(new Core.Violation
            {
                FilePath = filePath,
                Type = Core.ViolationType.DynamicInline,
                Content = "jQuery.ready with inline function detected",
                Severity = "medium"
            });
        }

        return violations;
    }
}

