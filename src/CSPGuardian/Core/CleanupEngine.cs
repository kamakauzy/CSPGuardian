using System.Security.Cryptography;
using System.Text;

namespace CSPGuardian.Core;

public class CleanupEngine
{
    private readonly ScanOptions _options;

    public CleanupEngine(ScanOptions options)
    {
        _options = options;
    }

    public async Task ProcessAsync(ScanResult scanResult)
    {
        switch (_options.Cleanup.ToLowerInvariant())
        {
            case "externalize":
                await ExternalizeAsync(scanResult);
                break;
            case "hash":
                await HashAsync(scanResult);
                break;
            case "nonce":
                await NonceAsync(scanResult);
                break;
        }
    }

    private async Task ExternalizeAsync(ScanResult scanResult)
    {
        var violationsByFile = scanResult.Violations
            .Where(v => v.Type == ViolationType.InlineScript || v.Type == ViolationType.InlineStyle)
            .GroupBy(v => v.FilePath);

        foreach (var group in violationsByFile)
        {
            var filePath = group.Key;
            var content = await File.ReadAllTextAsync(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(filePath);

            int scriptIndex = 0;
            int styleIndex = 0;

            foreach (var violation in group)
            {
                if (violation.Type == ViolationType.InlineScript)
                {
                    var externalFile = Path.Combine(directory, $"{baseName}.script{scriptIndex++}.js");
                    // Extract and write script content
                    // Update original file to reference external file
                    violation.SuggestedFix = $"Externalize to {externalFile}";
                }
                else if (violation.Type == ViolationType.InlineStyle)
                {
                    var externalFile = Path.Combine(directory, $"{baseName}.style{styleIndex++}.css");
                    violation.SuggestedFix = $"Externalize to {externalFile}";
                }
            }
        }
    }

    private async Task HashAsync(ScanResult scanResult)
    {
        foreach (var violation in scanResult.Violations)
        {
            if (violation.Type == ViolationType.InlineScript || violation.Type == ViolationType.InlineStyle)
            {
                var hash = ComputeHash(violation.Content, _options.Framework.Contains("legacy") ? "sha256" : "sha384");
                violation.Hash = hash;
                violation.SuggestedFix = $"Add hash to CSP: '{hash}'";
            }
        }
    }

    private async Task NonceAsync(ScanResult scanResult)
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
}

