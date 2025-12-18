using CSPGuardian.Core;
using Xunit;

namespace CSPGuardian.Tests;

public class CleanupEngineTests
{
    [Fact]
    public async Task HashAsync_GeneratesHashForViolations()
    {
        // Arrange
        var scanResult = new ScanResult
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Type = ViolationType.InlineScript,
                    Content = "alert('test');"
                }
            }
        };

        var options = new ScanOptions { Cleanup = "hash", Framework = "modern-dotnet" };
        var cleanupEngine = new CleanupEngine(options);

        // Act
        await cleanupEngine.ProcessAsync(scanResult);

        // Assert
        Assert.All(scanResult.Violations, v => Assert.NotNull(v.Hash));
        Assert.All(scanResult.Violations, v => Assert.NotEmpty(v.Hash));
    }

    [Fact]
    public async Task NonceAsync_GeneratesNonceForViolations()
    {
        // Arrange
        var scanResult = new ScanResult
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Type = ViolationType.InlineScript,
                    Content = "alert('test');"
                }
            }
        };

        var options = new ScanOptions { Cleanup = "nonce" };
        var cleanupEngine = new CleanupEngine(options);

        // Act
        await cleanupEngine.ProcessAsync(scanResult);

        // Assert
        Assert.All(scanResult.Violations, v => Assert.NotNull(v.SuggestedFix));
        Assert.All(scanResult.Violations, v => Assert.Contains("nonce", v.SuggestedFix ?? ""));
    }
}

