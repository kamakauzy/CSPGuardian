using CSPGuardian.Core;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CSPGuardian.Tests;

public class CleanupEngineTests
{
    [Fact]
    public async Task ProcessAsync_HashUsesFullRawContent()
    {
        var fullContent = string.Join(Environment.NewLine, Enumerable.Repeat("console.log('hash-me');", 20));
        var expectedHash = $"sha384-{Convert.ToBase64String(SHA384.HashData(Encoding.UTF8.GetBytes(fullContent)))}";
        var scanResult = new ScanResult
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Type = ViolationType.InlineScript,
                    Content = fullContent[..40] + "...",
                    RawContent = fullContent
                }
            }
        };

        var options = new ScanOptions { Cleanup = CleanupStrategies.Hash, Framework = ScanFrameworks.ModernDotNet };
        var cleanupEngine = new CleanupEngine(options);

        await cleanupEngine.ProcessAsync(scanResult);

        Assert.Equal(expectedHash, scanResult.Violations.Single().Hash);
    }

    [Fact]
    public async Task ProcessAsync_NonceGeneratesSuggestionsForInlineViolations()
    {
        var scanResult = new ScanResult
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Type = ViolationType.InlineScript,
                    RawContent = "alert('test');",
                    Content = "alert('test');"
                }
            }
        };

        var options = new ScanOptions { Cleanup = CleanupStrategies.Nonce };
        var cleanupEngine = new CleanupEngine(options);

        await cleanupEngine.ProcessAsync(scanResult);

        Assert.All(scanResult.Violations, v => Assert.NotNull(v.SuggestedFix));
        Assert.All(scanResult.Violations, v => Assert.Contains("nonce", v.SuggestedFix ?? ""));
    }

    [Fact]
    public async Task ProcessAsync_ExternalizeDryRunPlansChangesWithoutWritingFiles()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "page.html");
        const string htmlContent = "<html><head><script>alert('dry-run');</script><style>body { color: red; }</style></head></html>";
        await File.WriteAllTextAsync(tempFile, htmlContent);

        try
        {
            var options = new ScanOptions
            {
                Path = tempFile,
                Cleanup = CleanupStrategies.Externalize,
                DryRun = true
            };

            var scanResult = await new Scanner(options).ScanAsync();
            var cleanupEngine = new CleanupEngine(options);

            await cleanupEngine.ProcessAsync(scanResult);

            Assert.Equal(htmlContent, await File.ReadAllTextAsync(tempFile));
            Assert.Contains(scanResult.Violations, violation => violation.GeneratedAssetPath?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true);
            Assert.Contains(scanResult.Violations, violation => violation.GeneratedAssetPath?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true);
            Assert.Empty(Directory.GetFiles(tempDirectory, "*.js"));
            Assert.Empty(Directory.GetFiles(tempDirectory, "*.css"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_ExternalizeWritesAssetsAndRewritesSource()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "page.html");
        const string script = "console.log('externalized');";
        const string style = "body { color: red; }";
        await File.WriteAllTextAsync(tempFile, $"<html><head><script>{script}</script><style>{style}</style></head></html>");

        try
        {
            var options = new ScanOptions
            {
                Path = tempFile,
                Cleanup = CleanupStrategies.Externalize
            };

            var scanResult = await new Scanner(options).ScanAsync();
            var cleanupEngine = new CleanupEngine(options);

            await cleanupEngine.ProcessAsync(scanResult);

            var rewrittenHtml = await File.ReadAllTextAsync(tempFile);
            var scriptAsset = Path.Combine(tempDirectory, "page.script0.js");
            var styleAsset = Path.Combine(tempDirectory, "page.style0.css");

            Assert.Contains("src=\"page.script0.js\"", rewrittenHtml);
            Assert.Contains("href=\"page.style0.css\"", rewrittenHtml);
            Assert.Equal(script, await File.ReadAllTextAsync(scriptAsset));
            Assert.Equal(style, await File.ReadAllTextAsync(styleAsset));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_ExternalizeUsesNextAvailableSuffixWhenFilesExist()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "page.html");
        await File.WriteAllTextAsync(tempFile, "<script>alert('collision');</script>");
        await File.WriteAllTextAsync(Path.Combine(tempDirectory, "page.script0.js"), "// existing");

        try
        {
            var options = new ScanOptions
            {
                Path = tempFile,
                Cleanup = CleanupStrategies.Externalize
            };

            var scanResult = await new Scanner(options).ScanAsync();
            await new CleanupEngine(options).ProcessAsync(scanResult);

            Assert.True(File.Exists(Path.Combine(tempDirectory, "page.script1.js")));
            Assert.Contains(scanResult.Violations, violation => violation.GeneratedAssetPath?.EndsWith("page.script1.js", StringComparison.OrdinalIgnoreCase) == true);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_AssignsManualSuggestionsForNonExternalizableFindings()
    {
        var scanResult = new ScanResult
        {
            Violations =
            [
                new Violation { Type = ViolationType.StyleAttribute, Content = "color:red" },
                new Violation { Type = ViolationType.JavaScriptUrl, Content = "javascript:alert(1)" }
            ]
        };

        var cleanupEngine = new CleanupEngine(new ScanOptions { Cleanup = CleanupStrategies.Externalize });
        await cleanupEngine.ProcessAsync(scanResult);

        Assert.Contains("stylesheet", scanResult.Violations[0].SuggestedFix ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("javascript:", scanResult.Violations[1].SuggestedFix ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "cspguardian-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}

