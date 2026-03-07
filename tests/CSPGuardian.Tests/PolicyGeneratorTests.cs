using CSPGuardian.Core;
using Xunit;

namespace CSPGuardian.Tests;

public class PolicyGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_UsesHashesAndDefaults()
    {
        var result = new ScanResult
        {
            Violations =
            [
                new Violation
                {
                    Type = ViolationType.InlineScript,
                    Hash = "sha384-script-hash"
                },
                new Violation
                {
                    Type = ViolationType.InlineStyle,
                    Hash = "sha384-style-hash"
                }
            ]
        };

        var generator = new PolicyGenerator(new ScanOptions
        {
            Framework = ScanFrameworks.ModernDotNet,
            Cleanup = CleanupStrategies.Hash
        });

        var policy = await generator.GenerateAsync(result);

        Assert.Contains("default-src 'self'", policy);
        Assert.Contains("script-src 'self' 'sha384-script-hash'", policy);
        Assert.Contains("style-src 'self' 'sha384-style-hash'", policy);
        Assert.Contains("object-src 'none'", policy);
    }

    [Fact]
    public async Task GenerateAsync_IncludesNonceAndLegacyGuidanceWhenRequested()
    {
        var generator = new PolicyGenerator(new ScanOptions
        {
            Framework = ScanFrameworks.LegacyDotNet,
            Cleanup = CleanupStrategies.Nonce,
            LegacyMode = true
        });

        var policy = await generator.GenerateAsync(new ScanResult());

        Assert.Contains("'nonce-{nonce}'", policy);
        Assert.Contains("Nonce implementation guidance", policy);
        Assert.Contains("Legacy .NET mode", policy);
        Assert.Contains("Legacy .NET Migration Suggestions", policy);
    }
}
