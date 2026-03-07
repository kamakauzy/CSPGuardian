using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CSPGuardian.Core;

public class CleanupEngine
{
    private static readonly Regex AttributeRegex = new(
        @"(?<name>[\w:-]+)\s*=\s*(?<quote>[""'])(?<value>.*?)\k<quote>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ScriptOpenTagRegex = new(
        @"^<script\b(?<attributes>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex StyleOpenTagRegex = new(
        @"^<style\b(?<attributes>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly HashSet<string> ExternalizableScriptTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/javascript",
        "application/javascript",
        "module"
    };

    private readonly ScanOptions _options;

    public CleanupEngine(ScanOptions options)
    {
        _options = options;
    }

    public async Task ProcessAsync(ScanResult scanResult)
    {
        switch (_options.Cleanup.ToLowerInvariant())
        {
            case CleanupStrategies.Externalize:
                await ExternalizeAsync(scanResult);
                break;
            case CleanupStrategies.Hash:
                ApplyHashes(scanResult);
                break;
            case CleanupStrategies.Nonce:
                ApplyNonces(scanResult);
                break;
        }

        ApplyManualFixes(scanResult);
    }

    private async Task ExternalizeAsync(ScanResult scanResult)
    {
        var violationsByFile = scanResult.Violations
            .Where(v => v.Type == ViolationType.InlineScript || v.Type == ViolationType.InlineStyle)
            .GroupBy(v => v.FilePath);

        foreach (var group in violationsByFile)
        {
            var filePath = group.Key;
            var directory = Path.GetDirectoryName(filePath) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var originalContent = await File.ReadAllTextAsync(filePath);
            var replacements = new List<(Violation Violation, string AssetPath, string ReplacementText, string AssetContent)>();
            var reservedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var scriptIndex = 0;
            var styleIndex = 0;

            foreach (var violation in group.OrderBy(violation => violation.SourceIndex))
            {
                if (violation.Type == ViolationType.InlineScript)
                {
                    if (!CanAutoExternalizeScript(violation))
                    {
                        violation.SuggestedFix = "Inline script uses a non-executable or specialized type and must be externalized manually.";
                        continue;
                    }

                    var externalFile = GetNextAvailableAssetPath(directory, baseName, "script", "js", ref scriptIndex, reservedAssetPaths);
                    var browserPath = ToBrowserPath(Path.GetRelativePath(directory, externalFile));
                    var replacement = BuildExternalizedScriptTag(violation, browserPath);

                    violation.GeneratedAssetPath = externalFile;
                    violation.SuggestedFix = _options.DryRun
                        ? $"Would externalize to {externalFile}"
                        : $"Externalized to {externalFile}";

                    replacements.Add((violation, externalFile, replacement, violation.RawContent ?? string.Empty));
                }
                else if (violation.Type == ViolationType.InlineStyle)
                {
                    var externalFile = GetNextAvailableAssetPath(directory, baseName, "style", "css", ref styleIndex, reservedAssetPaths);
                    var browserPath = ToBrowserPath(Path.GetRelativePath(directory, externalFile));
                    var replacement = BuildExternalizedStylesheetLink(violation, browserPath);

                    violation.GeneratedAssetPath = externalFile;
                    violation.SuggestedFix = _options.DryRun
                        ? $"Would externalize to {externalFile}"
                        : $"Externalized to {externalFile}";

                    replacements.Add((violation, externalFile, replacement, violation.RawContent ?? string.Empty));
                }
            }

            if (_options.DryRun || replacements.Count == 0)
            {
                continue;
            }

            foreach (var replacement in replacements)
            {
                await File.WriteAllTextAsync(replacement.AssetPath, replacement.AssetContent);
            }

            var updatedContent = originalContent;
            foreach (var replacement in replacements.OrderByDescending(item => item.Violation.SourceIndex))
            {
                updatedContent = updatedContent.Remove(replacement.Violation.SourceIndex, replacement.Violation.SourceLength);
                updatedContent = updatedContent.Insert(replacement.Violation.SourceIndex, replacement.ReplacementText);
            }

            await File.WriteAllTextAsync(filePath, updatedContent);
        }
    }

    private void ApplyHashes(ScanResult scanResult)
    {
        foreach (var violation in scanResult.Violations)
        {
            if (violation.Type == ViolationType.InlineScript || violation.Type == ViolationType.InlineStyle)
            {
                var hash = ComputeHash(
                    violation.RawContent ?? violation.Content,
                    _options.Framework.Contains("legacy", StringComparison.OrdinalIgnoreCase) ? "sha256" : "sha384");

                violation.Hash = hash;
                violation.SuggestedFix = $"Add hash to CSP: '{hash}'";
            }
        }
    }

    private void ApplyNonces(ScanResult scanResult)
    {
        var nonce = GenerateNonce();
        
        foreach (var violation in scanResult.Violations)
        {
            if (violation.Type == ViolationType.InlineScript || violation.Type == ViolationType.InlineStyle)
            {
                violation.SuggestedFix = $"Add nonce='{nonce}' attribute and include 'nonce-{nonce}' in CSP";
            }
        }
    }

    private void ApplyManualFixes(ScanResult scanResult)
    {
        foreach (var violation in scanResult.Violations.Where(violation => string.IsNullOrWhiteSpace(violation.SuggestedFix)))
        {
            violation.SuggestedFix = violation.Type switch
            {
                ViolationType.EventHandler => "Move inline event-handler code into a script file and bind it with addEventListener.",
                ViolationType.StyleAttribute => "Move inline styles into a stylesheet or use a temporary CSP nonce/hash during migration.",
                ViolationType.JavaScriptUrl => "Replace javascript: URLs with safe links, buttons, or scripted event handlers.",
                ViolationType.DynamicInline => "Refactor dynamic code execution to avoid eval() and Function() where possible.",
                ViolationType.LegacyWebResource => "Review legacy WebResource.axd usage and prefer bundled static assets when possible.",
                ViolationType.ViewStateEmbedded => "Review ViewState-driven script generation and move executable code into static assets.",
                _ => "Review and remediate this violation before enforcing a strict CSP."
            };
        }
    }

    private string ComputeHash(string content, string algorithm = "sha384")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        byte[] hashBytes;
        string algorithmName;

        switch (algorithm.ToLowerInvariant())
        {
            case "sha256":
                hashBytes = SHA256.HashData(bytes);
                algorithmName = "sha256";
                break;
            case "sha384":
                hashBytes = SHA384.HashData(bytes);
                algorithmName = "sha384";
                break;
            case "sha512":
                hashBytes = SHA512.HashData(bytes);
                algorithmName = "sha512";
                break;
            default:
                hashBytes = SHA384.HashData(bytes);
                algorithmName = "sha384";
                break;
        }

        // CSP hash format: 'sha256-<base64>'
        return $"{algorithmName}-{Convert.ToBase64String(hashBytes)}";
    }

    private string GenerateNonce()
    {
        var bytes = new byte[16];
        Random.Shared.NextBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static bool CanAutoExternalizeScript(Violation violation)
    {
        if (string.IsNullOrWhiteSpace(violation.OriginalText))
        {
            return false;
        }

        var match = ScriptOpenTagRegex.Match(violation.OriginalText);
        if (!match.Success)
        {
            return false;
        }

        var scriptType = GetAttributeValue(match.Groups["attributes"].Value, "type");
        return string.IsNullOrWhiteSpace(scriptType) || ExternalizableScriptTypes.Contains(scriptType.Trim());
    }

    private static string BuildExternalizedScriptTag(Violation violation, string browserPath)
    {
        var match = ScriptOpenTagRegex.Match(violation.OriginalText ?? string.Empty);
        var attributes = match.Success ? match.Groups["attributes"].Value : string.Empty;
        var preservedAttributes = BuildAttributeString(
            attributes,
            attribute =>
                !attribute.Name.Equals("src", StringComparison.OrdinalIgnoreCase)
                && !attribute.Name.Equals("nonce", StringComparison.OrdinalIgnoreCase));

        return $"<script{preservedAttributes} src=\"{browserPath}\"></script>";
    }

    private static string BuildExternalizedStylesheetLink(Violation violation, string browserPath)
    {
        var match = StyleOpenTagRegex.Match(violation.OriginalText ?? string.Empty);
        var attributes = match.Success ? match.Groups["attributes"].Value : string.Empty;
        var preservedAttributes = BuildAttributeString(
            attributes,
            attribute =>
                attribute.Name.Equals("media", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.Equals("id", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.Equals("class", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.Equals("title", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.StartsWith("data-", StringComparison.OrdinalIgnoreCase));

        return $"<link rel=\"stylesheet\" href=\"{browserPath}\"{preservedAttributes}>";
    }

    private static string BuildAttributeString(string attributes, Func<(string Name, string Value), bool> predicate)
    {
        var selectedAttributes = AttributeRegex.Matches(attributes)
            .Cast<Match>()
            .Select(match => (Name: match.Groups["name"].Value, Value: match.Groups["value"].Value))
            .Where(predicate)
            .Select(attribute => $"{attribute.Name}=\"{attribute.Value}\"")
            .ToList();

        return selectedAttributes.Count == 0
            ? string.Empty
            : " " + string.Join(" ", selectedAttributes);
    }

    private static string? GetAttributeValue(string attributes, string attributeName)
    {
        return AttributeRegex.Matches(attributes)
            .Cast<Match>()
            .FirstOrDefault(match => match.Groups["name"].Value.Equals(attributeName, StringComparison.OrdinalIgnoreCase))
            ?.Groups["value"].Value;
    }

    private static string GetNextAvailableAssetPath(
        string directory,
        string baseName,
        string suffix,
        string extension,
        ref int startIndex,
        ISet<string> reservedPaths)
    {
        while (true)
        {
            var candidate = Path.Combine(directory, $"{baseName}.{suffix}{startIndex}.{extension}");
            startIndex++;

            if (reservedPaths.Contains(candidate) || File.Exists(candidate))
            {
                continue;
            }

            reservedPaths.Add(candidate);
            return candidate;
        }
    }

    private static string ToBrowserPath(string relativePath)
    {
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}

