using CSPGuardian.Core;
using Xunit;

namespace CSPGuardian.Tests;

public class ScannerTests
{
    [Fact]
    public async Task ScanAsync_DetectsInlineScript()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".html";
        var htmlContent = "<html><head><script>alert('test');</script></head></html>";
        await File.WriteAllTextAsync(tempFile, htmlContent);

        var options = new ScanOptions { Path = tempFile, Framework = "static" };
        var scanner = new Scanner(options);

        try
        {
            // Act
            var result = await scanner.ScanAsync();

            // Assert
            Assert.True(result.ViolationsFound > 0);
            Assert.Contains(result.Violations, v => v.Type == ViolationType.InlineScript);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsInlineStyle()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".html";
        var htmlContent = "<html><head><style>body { color: red; }</style></head></html>";
        await File.WriteAllTextAsync(tempFile, htmlContent);

        var options = new ScanOptions { Path = tempFile, Framework = "static" };
        var scanner = new Scanner(options);

        try
        {
            // Act
            var result = await scanner.ScanAsync();

            // Assert
            Assert.True(result.ViolationsFound > 0);
            Assert.Contains(result.Violations, v => v.Type == ViolationType.InlineStyle);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsEventHandler()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".html";
        var htmlContent = "<html><body><button onclick=\"alert('test')\">Click</button></body></html>";
        await File.WriteAllTextAsync(tempFile, htmlContent);

        var options = new ScanOptions { Path = tempFile, Framework = "static" };
        var scanner = new Scanner(options);

        try
        {
            // Act
            var result = await scanner.ScanAsync();

            // Assert
            Assert.True(result.ViolationsFound > 0);
            Assert.Contains(result.Violations, v => v.Type == ViolationType.EventHandler);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

