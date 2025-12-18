using System.Text;

namespace CSPGuardian.Core;

public class PolicyGenerator
{
    private readonly ScanOptions _options;

    public PolicyGenerator(ScanOptions options)
    {
        _options = options;
    }

    public async Task<string> GenerateAsync(ScanResult scanResult)
    {
        var policy = new StringBuilder();
        
        // Collect hashes and nonces
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

        // Build CSP directives
        policy.Append("Content-Security-Policy: ");
        
        var directives = new List<string>();

        // script-src
        var scriptSrc = new List<string> { "'self'" };
        if (scriptHashes.Any())
        {
            scriptSrc.AddRange(scriptHashes);
        }
        else if (_options.Cleanup == "nonce")
        {
            scriptSrc.Add("'nonce-{nonce}'");
        }
        else
        {
            scriptSrc.Add("'strict-dynamic'");
        }
        directives.Add($"script-src {string.Join(" ", scriptSrc)}");

        // style-src
        var styleSrc = new List<string> { "'self'" };
        if (styleHashes.Any())
        {
            styleSrc.AddRange(styleHashes);
        }
        else if (_options.Cleanup == "nonce")
        {
            styleSrc.Add("'nonce-{nonce}'");
        }
        directives.Add($"style-src {string.Join(" ", styleSrc)}");

        // object-src
        directives.Add("object-src 'none'");

        // base-uri
        directives.Add("base-uri 'self'");

        // form-action
        directives.Add("form-action 'self'");

        // frame-ancestors
        directives.Add("frame-ancestors 'none'");

        // upgrade-insecure-requests
        directives.Add("upgrade-insecure-requests");

        policy.Append(string.Join("; ", directives));

        if (_options.Framework.Contains("legacy"))
        {
            policy.AppendLine();
            policy.AppendLine("# Note: Legacy .NET mode - some directives may need adjustment for Web Forms/MVC 4");
        }

        return policy.ToString();
    }
}

