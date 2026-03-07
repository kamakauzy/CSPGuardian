using System.Text.RegularExpressions;
using CSPGuardian.Adapters;

namespace CSPGuardian.Core;

public class Scanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".cshtml", ".aspx", ".ascx", ".js", ".css"
    };

    private static readonly Regex ScriptTagRegex = new(
        @"<script\b(?<attributes>[^>]*)>(?<body>.*?)</script>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex StyleTagRegex = new(
        @"<style\b(?<attributes>[^>]*)>(?<body>.*?)</style>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlTagRegex = new(
        @"<(?<tag>[a-zA-Z][\w:-]*)(?<attributes>\s[^<>]*?)?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[\w:-]+)\s*=\s*(?<quote>[""'])(?<value>.*?)\k<quote>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex EvalRegex = new(
        @"\beval\s*\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionRegex = new(
        @"\b(?:new\s+)?Function\s*\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WebResourceRegex = new(
        @"WebResource\.axd",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ViewStateRegex = new(
        @"__VIEWSTATE.*script",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CssJavaScriptUrlRegex = new(
        @"url\(\s*(?:(?<quote>[""'])(?<value>javascript\s*:.*?)\k<quote>|(?<value>javascript\s*:[^)]+))\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ScanOptions _options;

    public Scanner(ScanOptions options)
    {
        _options = options;
        _options.Normalize();
    }

    public async Task<ScanResult> ScanAsync()
    {
        var scanRoot = ValidateScanPath();
        var result = new ScanResult();

        foreach (var file in GetFilesToScan(scanRoot, result))
        {
            try
            {
                var violations = await ScanFileAsync(file);
                result.Violations.AddRange(violations);
                result.TotalFilesScanned++;
            }
            catch (IOException)
            {
                result.TotalFilesSkipped++;
                result.SkippedPaths.Add(file);
            }
            catch (UnauthorizedAccessException)
            {
                result.TotalFilesSkipped++;
                result.SkippedPaths.Add(file);
            }
        }

        result.Metadata["Framework"] = _options.Framework;
        result.Metadata["LegacyMode"] = _options.LegacyMode;
        result.Metadata["DryRun"] = _options.DryRun;
        result.Metadata["ScanRoot"] = scanRoot;

        return result;
    }

    private string ValidateScanPath()
    {
        if (string.IsNullOrWhiteSpace(_options.Path))
        {
            throw new ArgumentException("The scan path cannot be empty.", nameof(_options.Path));
        }

        var scanPath = Path.GetFullPath(_options.Path);
        if (!File.Exists(scanPath) && !Directory.Exists(scanPath))
        {
            throw new DirectoryNotFoundException($"The scan path '{scanPath}' does not exist.");
        }

        return scanPath;
    }

    private IEnumerable<string> GetFilesToScan(string scanRoot, ScanResult result)
    {
        if (File.Exists(scanRoot))
        {
            if (SupportedExtensions.Contains(Path.GetExtension(scanRoot)))
            {
                yield return scanRoot;
            }

            yield break;
        }

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(scanRoot);

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentDirectory);
            }
            catch (IOException)
            {
                result.TotalFilesSkipped++;
                result.SkippedPaths.Add(currentDirectory);
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                result.TotalFilesSkipped++;
                result.SkippedPaths.Add(currentDirectory);
                continue;
            }

            foreach (var file in files.Where(file => SupportedExtensions.Contains(Path.GetExtension(file))))
            {
                yield return file;
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(currentDirectory);
            }
            catch (IOException)
            {
                result.TotalFilesSkipped++;
                result.SkippedPaths.Add(currentDirectory);
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                result.TotalFilesSkipped++;
                result.SkippedPaths.Add(currentDirectory);
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (!IsExcludedDirectory(childDirectory, scanRoot))
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }
    }

    private bool IsExcludedDirectory(string directoryPath, string scanRoot)
    {
        var excludedDirectories = _options.GetExcludedDirectories();
        var directoryName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (excludedDirectories.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedDirectoryPath = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return _options.ExcludedDirectories.Any(excludedDirectory =>
        {
            var candidate = excludedDirectory;
            if (!Path.IsPathRooted(candidate))
            {
                candidate = Path.GetFullPath(Path.Combine(scanRoot, candidate));
            }

            candidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedDirectoryPath, candidate, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async Task<List<Violation>> ScanFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var extension = Path.GetExtension(filePath);

        if (IsHtmlLikeExtension(extension))
        {
            return ScanHtmlContent(filePath, content);
        }

        if (extension.Equals(".js", StringComparison.OrdinalIgnoreCase))
        {
            return ScanJavaScriptContent(filePath, content);
        }

        if (extension.Equals(".css", StringComparison.OrdinalIgnoreCase))
        {
            return ScanCssContent(filePath, content);
        }

        return [];
    }

    private List<Violation> ScanHtmlContent(string filePath, string content)
    {
        var violations = new List<Violation>();

        violations.AddRange(ScanInlineScripts(filePath, content));
        violations.AddRange(ScanInlineStyles(filePath, content));
        violations.AddRange(ScanHtmlAttributes(filePath, content));

        if (_options.LegacyMode || _options.Framework == ScanFrameworks.LegacyDotNet)
        {
            violations.AddRange(ScanLegacyDotNet(filePath, content));
        }

        return violations;
    }

    private List<Violation> ScanInlineScripts(string filePath, string content)
    {
        var violations = new List<Violation>();

        foreach (Match match in ScriptTagRegex.Matches(content))
        {
            var attributes = match.Groups["attributes"].Value;
            if (HasAttribute(attributes, "src"))
            {
                continue;
            }

            var scriptBody = match.Groups["body"].Value;
            if (string.IsNullOrWhiteSpace(scriptBody))
            {
                continue;
            }

            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = GetLineNumberFromIndex(content, match.Index),
                Type = ViolationType.InlineScript,
                Content = TruncateContent(scriptBody, 200),
                RawContent = scriptBody,
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "high"
            });
        }

        return violations;
    }

    private List<Violation> ScanInlineStyles(string filePath, string content)
    {
        var violations = new List<Violation>();

        foreach (Match match in StyleTagRegex.Matches(content))
        {
            var styleBody = match.Groups["body"].Value;
            if (string.IsNullOrWhiteSpace(styleBody))
            {
                continue;
            }

            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = GetLineNumberFromIndex(content, match.Index),
                Type = ViolationType.InlineStyle,
                Content = TruncateContent(styleBody, 200),
                RawContent = styleBody,
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "medium"
            });

            violations.AddRange(ScanCssJavaScriptUrls(filePath, content, styleBody, match.Groups["body"].Index));
        }

        return violations;
    }

    private List<Violation> ScanHtmlAttributes(string filePath, string content)
    {
        var violations = new List<Violation>();

        foreach (Match tagMatch in HtmlTagRegex.Matches(content))
        {
            var attributesGroup = tagMatch.Groups["attributes"];
            if (!attributesGroup.Success || string.IsNullOrWhiteSpace(attributesGroup.Value))
            {
                continue;
            }

            foreach (Match attributeMatch in AttributeRegex.Matches(attributesGroup.Value))
            {
                var attributeName = attributeMatch.Groups["name"].Value;
                var attributeValue = attributeMatch.Groups["value"].Value;
                var absoluteIndex = attributesGroup.Index + attributeMatch.Index;

                if (attributeName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(new Violation
                    {
                        FilePath = filePath,
                        LineNumber = GetLineNumberFromIndex(content, absoluteIndex),
                        Type = ViolationType.EventHandler,
                        Content = TruncateContent(attributeMatch.Value, 200),
                        RawContent = attributeValue,
                        AttributeName = attributeName,
                        OriginalText = attributeMatch.Value,
                        SourceIndex = absoluteIndex,
                        SourceLength = attributeMatch.Length,
                        Severity = "high"
                    });
                }

                if (attributeName.Equals("style", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(attributeValue))
                {
                    violations.Add(new Violation
                    {
                        FilePath = filePath,
                        LineNumber = GetLineNumberFromIndex(content, absoluteIndex),
                        Type = ViolationType.StyleAttribute,
                        Content = TruncateContent(attributeValue, 200),
                        RawContent = attributeValue,
                        AttributeName = attributeName,
                        OriginalText = attributeMatch.Value,
                        SourceIndex = absoluteIndex,
                        SourceLength = attributeMatch.Length,
                        Severity = "medium"
                    });

                    violations.AddRange(ScanCssJavaScriptUrls(filePath, content, attributeValue, absoluteIndex));
                }

                if (IsJavaScriptUrl(attributeValue))
                {
                    violations.Add(new Violation
                    {
                        FilePath = filePath,
                        LineNumber = GetLineNumberFromIndex(content, absoluteIndex),
                        Type = ViolationType.JavaScriptUrl,
                        Content = TruncateContent(attributeMatch.Value, 200),
                        RawContent = attributeValue,
                        AttributeName = attributeName,
                        OriginalText = attributeMatch.Value,
                        SourceIndex = absoluteIndex,
                        SourceLength = attributeMatch.Length,
                        Severity = "high"
                    });
                }
            }
        }

        return violations;
    }

    private List<Violation> ScanLegacyDotNet(string filePath, string content)
    {
        var violations = new List<Violation>();

        foreach (Match match in WebResourceRegex.Matches(content))
        {
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = GetLineNumberFromIndex(content, match.Index),
                Type = ViolationType.LegacyWebResource,
                Content = "WebResource.axd reference detected",
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "low"
            });
        }

        foreach (Match match in ViewStateRegex.Matches(content))
        {
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = GetLineNumberFromIndex(content, match.Index),
                Type = ViolationType.ViewStateEmbedded,
                Content = "ViewState with embedded script detected",
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "medium"
            });
        }

        return violations;
    }

    private List<Violation> ScanJavaScriptContent(string filePath, string content)
    {
        var violations = new List<Violation>();

        foreach (Match match in EvalRegex.Matches(content))
        {
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = GetLineNumberFromIndex(content, match.Index),
                Type = ViolationType.DynamicInline,
                Content = "eval() usage detected",
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "high"
            });
        }

        foreach (Match match in FunctionRegex.Matches(content))
        {
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = GetLineNumberFromIndex(content, match.Index),
                Type = ViolationType.DynamicInline,
                Content = "Function() constructor usage detected",
                OriginalText = match.Value,
                SourceIndex = match.Index,
                SourceLength = match.Length,
                Severity = "high"
            });
        }

        if (_options.Framework is ScanFrameworks.JsModern or ScanFrameworks.JsLegacy)
        {
            var adapter = new JsAdapter(_options.Framework);
            violations.AddRange(adapter.Process(filePath, content));
        }

        return violations;
    }

    private List<Violation> ScanCssContent(string filePath, string content)
    {
        return ScanCssJavaScriptUrls(filePath, content, content, 0);
    }

    private List<Violation> ScanCssJavaScriptUrls(string filePath, string fileContent, string cssContent, int baseIndex)
    {
        var violations = new List<Violation>();

        foreach (Match match in CssJavaScriptUrlRegex.Matches(cssContent))
        {
            var absoluteIndex = baseIndex + match.Index;
            violations.Add(new Violation
            {
                FilePath = filePath,
                LineNumber = GetLineNumberFromIndex(fileContent, absoluteIndex),
                Type = ViolationType.JavaScriptUrl,
                Content = TruncateContent(match.Value, 200),
                RawContent = match.Groups["value"].Value,
                OriginalText = match.Value,
                SourceIndex = absoluteIndex,
                SourceLength = match.Length,
                Severity = "high"
            });
        }

        return violations;
    }

    private static bool IsHtmlLikeExtension(string extension)
    {
        return extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".aspx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ascx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAttribute(string attributes, string attributeName)
    {
        return AttributeRegex.Matches(attributes)
            .Cast<Match>()
            .Any(match => match.Groups["name"].Value.Equals(attributeName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsJavaScriptUrl(string value)
    {
        return value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);
    }

    internal static int GetLineNumberFromIndex(string content, int index)
    {
        if (index < 0)
        {
            return 0;
        }

        var lineNumber = 1;
        for (var currentIndex = 0; currentIndex < index && currentIndex < content.Length; currentIndex++)
        {
            if (content[currentIndex] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    internal static string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + "...";
    }
}

