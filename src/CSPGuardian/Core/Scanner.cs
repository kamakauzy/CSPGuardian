using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CSPGuardian.Core;

public class Scanner
{
    private readonly ScanOptions _options;
    private static readonly string[] SupportedExtensions = { ".html", ".htm", ".cshtml", ".aspx", ".ascx", ".js", ".css" };
    private static readonly Regex EventHandlerRegex = new(
        @"\b(on\w+)\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public Scanner(ScanOptions options)
    {
        _options = options;
    }

    public async Task<ScanResult> ScanAsync()
    {
        var result = new ScanResult();
        var files = GetFilesToScan(_options.Path);

        foreach (var file in files)
        {
            var violations = await ScanFileAsync(file);
            result.Violations.AddRange(violations);
            result.TotalFilesScanned++;
        }

        result.Metadata["Framework"] = _options.Framework;
        result.Metadata["LegacyMode"] = _options.LegacyMode;

        return result;
    }

    private List<string> GetFilesToScan(string path)
    {
        var files = new List<string>();

        if (File.Exists(path))
        {
            files.Add(path);
            return files;
        }

        if (Directory.Exists(path))
        {
            files.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())));
        }

        return files;
    }

    private async Task<List<Violation>> ScanFileAsync(string filePath)
    {
        var violations = new List<Violation>();
        var content = await File.ReadAllTextAsync(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".html" || extension == ".htm" || extension == ".cshtml" || 
            extension == ".aspx" || extension == ".ascx")
        {
            violations.AddRange(ScanHtmlContent(filePath, content));
        }
        else if (extension == ".js")
        {
            violations.AddRange(ScanJavaScriptContent(filePath, content));
        }

        return violations;
    }

    private List<Violation> ScanHtmlContent(string filePath, string content)
    {
        var violations = new List<Violation>();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // Scan for inline <script> tags
        var scriptTags = doc.DocumentNode.SelectNodes("//script[not(@src)]");
        if (scriptTags != null)
        {
            foreach (var script in scriptTags)
            {
                var scriptContent = script.InnerText;
                if (!string.IsNullOrWhiteSpace(scriptContent))
                {
                    violations.Add(new Violation
                    {
                        FilePath = filePath,
                        LineNumber = GetLineNumber(content, script.OuterHtml),
                        Type = ViolationType.InlineScript,
                        Content = TruncateContent(scriptContent, 200),
                        Severity = "high"
                    });
                }
            }
        }

        // Scan for inline <style> tags
        var styleTags = doc.DocumentNode.SelectNodes("//style");
        if (styleTags != null)
        {
            foreach (var style in styleTags)
            {
                var styleContent = style.InnerText;
                if (!string.IsNullOrWhiteSpace(styleContent))
                {
                    violations.Add(new Violation
                    {
                        FilePath = filePath,
                        LineNumber = GetLineNumber(content, style.OuterHtml),
                        Type = ViolationType.InlineStyle,
                        Content = TruncateContent(styleContent, 200),
                        Severity = "medium"
                    });
                }
            }
        }

        // Scan for event handlers (onclick, onload, etc.)
        var allNodes = doc.DocumentNode.Descendants();
        foreach (var node in allNodes)
        {
            foreach (var attribute in node.Attributes)
            {
                if (attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(new Violation
                    {
                        FilePath = filePath,
                        LineNumber = GetLineNumber(content, node.OuterHtml),
                        Type = ViolationType.EventHandler,
                        Content = $"{attribute.Name}=\"{attribute.Value}\"",
                        Severity = "high"
                    });
                }
            }
        }

        // Legacy .NET specific scans
        if (_options.LegacyMode || _options.Framework == "legacy-dotnet")
        {
            violations.AddRange(ScanLegacyDotNet(content, filePath));
        }

        return violations;
    }

    private List<Violation> ScanLegacyDotNet(string content, string filePath)
    {
        var violations = new List<Violation>();

        // Scan for WebResource.axd references
        if (Regex.IsMatch(content, @"WebResource\.axd", RegexOptions.IgnoreCase))
        {
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = 0,
                Type = ViolationType.LegacyWebResource,
                Content = "WebResource.axd reference detected",
                Severity = "low"
            });
        }

        // Scan for ViewState embedded JS
        if (Regex.IsMatch(content, @"__VIEWSTATE.*script", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = 0,
                Type = ViolationType.ViewStateEmbedded,
                Content = "ViewState with embedded script detected",
                Severity = "medium"
            });
        }

        return violations;
    }

    private List<Violation> ScanJavaScriptContent(string filePath, string content)
    {
        var violations = new List<Violation>();

        // Scan for eval() usage
        if (Regex.IsMatch(content, @"\beval\s*\(", RegexOptions.IgnoreCase))
        {
            var matches = Regex.Matches(content, @"\beval\s*\(", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                violations.Add(new Violation
                {
                    FilePath = filePath,
                    LineNumber = GetLineNumber(content, match.Value),
                    Type = ViolationType.DynamicInline,
                    Content = "eval() usage detected",
                    Severity = "high"
                });
            }
        }

        // Scan for Function() constructor
        if (Regex.IsMatch(content, @"new\s+Function\s*\(", RegexOptions.IgnoreCase))
        {
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = 0,
                Type = ViolationType.DynamicInline,
                Content = "Function() constructor usage detected",
                Severity = "high"
            });
        }

        return violations;
    }

    private int GetLineNumber(string content, string searchText)
    {
        var index = content.IndexOf(searchText, StringComparison.Ordinal);
        if (index == -1) return 0;

        return content.Substring(0, index).Split('\n').Length;
    }

    private string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength) return content;
        return content.Substring(0, maxLength) + "...";
    }
}

