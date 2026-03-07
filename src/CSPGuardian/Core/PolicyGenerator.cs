using System.Text;
using CSPGuardian.Adapters;

namespace CSPGuardian.Core;

public class PolicyGenerator
{
    private readonly ScanOptions _options;

    public PolicyGenerator(ScanOptions options)
    {
        _options = options;
    }

    public Task<string> GenerateAsync(ScanResult scanResult)
    {
        var policy = new StringBuilder();

        var scriptHashes = scanResult.Violations
            .Where(v => v.Type == ViolationType.InlineScript && !string.IsNullOrEmpty(v.Hash))
            .Select(v => $"'{v.Hash}'")
            .Distinct()
            .ToList();

        var styleHashes = scanResult.Violations
            .Where(v => v.Type == ViolationType.InlineStyle && !string.IsNullOrEmpty(v.Hash))
            .Select(v => $"'{v.Hash}'")
            .Distinct()
            .ToList();

        policy.Append("Content-Security-Policy: ");

        var directives = new List<string>();
        directives.Add("default-src 'self'");

        var scriptSrc = new List<string> { "'self'" };
        if (scriptHashes.Any())
        {
            scriptSrc.AddRange(scriptHashes);
        }
        else if (_options.Cleanup == CleanupStrategies.Nonce)
        {
            scriptSrc.Add("'nonce-{nonce}'");
        }

        directives.Add($"script-src {string.Join(" ", scriptSrc)}");

        var styleSrc = new List<string> { "'self'" };
        if (styleHashes.Any())
        {
            styleSrc.AddRange(styleHashes);
        }
        else if (_options.Cleanup == CleanupStrategies.Nonce)
        {
            styleSrc.Add("'nonce-{nonce}'");
        }

        directives.Add($"style-src {string.Join(" ", styleSrc)}");

        directives.Add("object-src 'none'");
        directives.Add("base-uri 'self'");
        directives.Add("form-action 'self'");
        directives.Add("frame-ancestors 'none'");
        directives.Add("upgrade-insecure-requests");

        policy.Append(string.Join("; ", directives));

        var remainingInlineFindings = scanResult.Violations.Count(violation =>
            violation.Type is ViolationType.InlineScript or ViolationType.InlineStyle or ViolationType.StyleAttribute or ViolationType.EventHandler or ViolationType.JavaScriptUrl);

        if (remainingInlineFindings > 0 && _options.Cleanup == CleanupStrategies.None)
        {
            policy.AppendLine();
            policy.AppendLine("# Inline findings remain. Use --cleanup hash, --cleanup nonce, or --cleanup externalize before enforcing this policy.");
        }

        if (_options.Cleanup == CleanupStrategies.Nonce)
        {
            var adapter = new DotNetAdapter(_options.Framework, _options.LegacyMode);
            policy.AppendLine();
            policy.AppendLine("# Nonce implementation guidance:");
            policy.AppendLine(adapter.GenerateNonceMiddleware());
        }

        if (_options.Framework.Contains("legacy", StringComparison.OrdinalIgnoreCase))
        {
            var adapter = new DotNetAdapter(_options.Framework, _options.LegacyMode);
            policy.AppendLine();
            policy.AppendLine("# Note: Legacy .NET mode - some directives may need adjustment for Web Forms/MVC 4");
            policy.AppendLine("# " + adapter.GetMigrationSuggestion().Replace(Environment.NewLine, Environment.NewLine + "# "));
        }

        return Task.FromResult(policy.ToString());
    }
}

