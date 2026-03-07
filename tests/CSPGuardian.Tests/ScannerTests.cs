using CSPGuardian.Core;
using Xunit;

namespace CSPGuardian.Tests;

public class ScannerTests
{
    [Fact]
    public async Task ScanAsync_DetectsInlineScriptAndPreservesFullContent()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "index.html");
        var longScript = string.Join(Environment.NewLine, Enumerable.Repeat("console.log('test');", 20));
        var htmlContent = $"<html><head><script>{longScript}</script></head></html>";
        await File.WriteAllTextAsync(tempFile, htmlContent);

        try
        {
            var scanner = new Scanner(new ScanOptions { Path = tempFile, Framework = ScanFrameworks.Static });
            var result = await scanner.ScanAsync();
            var violation = Assert.Single(result.Violations.Where(v => v.Type == ViolationType.InlineScript));

            Assert.True(result.ViolationsFound > 0);
            Assert.Equal(longScript, violation.RawContent);
            Assert.EndsWith("...", violation.Content);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsStyleAttributesAndJavaScriptUrls()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "index.html");
        var htmlContent = """
            <html>
            <body>
                <a href=" javascript:alert('xss')">Click</a>
                <div style="background-image: url('javascript:alert(1)'); color: red;">Styled</div>
            </body>
            </html>
            """;
        await File.WriteAllTextAsync(tempFile, htmlContent);

        try
        {
            var scanner = new Scanner(new ScanOptions { Path = tempFile, Framework = ScanFrameworks.Static });
            var result = await scanner.ScanAsync();

            Assert.Contains(result.Violations, v => v.Type == ViolationType.StyleAttribute);
            Assert.Equal(2, result.Violations.Count(v => v.Type == ViolationType.JavaScriptUrl));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsCssJavaScriptUrls()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "site.css");
        await File.WriteAllTextAsync(tempFile, ".hero { background-image: url(\"javascript:alert(1)\"); }");

        try
        {
            var scanner = new Scanner(new ScanOptions { Path = tempFile, Framework = ScanFrameworks.Static });
            var result = await scanner.ScanAsync();

            Assert.Contains(result.Violations, v => v.Type == ViolationType.JavaScriptUrl);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsEventHandlerAndReportsAccurateLineNumbersForDuplicates()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "index.html");
        var htmlContent = """
            <html>
            <body>
            <script>alert('dup');</script>
            <div>between</div>
            <script>alert('dup');</script>
            <button onclick="alert('click')">Click</button>
            </body>
            </html>
            """;
        await File.WriteAllTextAsync(tempFile, htmlContent);

        try
        {
            var scanner = new Scanner(new ScanOptions { Path = tempFile, Framework = ScanFrameworks.Static });
            var result = await scanner.ScanAsync();

            Assert.Contains(result.Violations, v => v.Type == ViolationType.EventHandler);
            Assert.Equal(new[] { 3, 5 }, result.Violations
                .Where(v => v.Type == ViolationType.InlineScript)
                .Select(v => v.LineNumber)
                .OrderBy(lineNumber => lineNumber)
                .ToArray());
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsLegacyDotNetPatternsAndJavaScriptFrameworkPatterns()
    {
        var tempDirectory = CreateTempDirectory();
        var legacyFile = Path.Combine(tempDirectory, "legacy.aspx");
        var jsFile = Path.Combine(tempDirectory, "legacy.js");

        await File.WriteAllTextAsync(legacyFile, """
            <html>
            <body>
                WebResource.axd
                __VIEWSTATE<script>alert('legacy')</script>
            </body>
            </html>
            """);

        await File.WriteAllTextAsync(jsFile, """
            eval("alert('x')");
            new Function("a", "return a;");
            $(document).ready(function () { console.log('ready'); });
            """);

        try
        {
            var legacyScanner = new Scanner(new ScanOptions
            {
                Path = legacyFile,
                Framework = ScanFrameworks.LegacyDotNet,
                LegacyMode = true
            });

            var jsScanner = new Scanner(new ScanOptions
            {
                Path = jsFile,
                Framework = ScanFrameworks.JsLegacy
            });

            var legacyResult = await legacyScanner.ScanAsync();
            var jsResult = await jsScanner.ScanAsync();

            Assert.Contains(legacyResult.Violations, v => v.Type == ViolationType.LegacyWebResource);
            Assert.Contains(legacyResult.Violations, v => v.Type == ViolationType.ViewStateEmbedded);
            Assert.True(jsResult.Violations.Count(v => v.Type == ViolationType.DynamicInline) >= 3);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_SkipsExcludedDirectories()
    {
        var tempDirectory = CreateTempDirectory();
        var includedDirectory = Path.Combine(tempDirectory, "src");
        var excludedDirectory = Path.Combine(tempDirectory, "bin");
        Directory.CreateDirectory(includedDirectory);
        Directory.CreateDirectory(excludedDirectory);

        await File.WriteAllTextAsync(Path.Combine(includedDirectory, "included.html"), "<script>alert('included')</script>");
        await File.WriteAllTextAsync(Path.Combine(excludedDirectory, "ignored.html"), "<script>alert('ignored')</script>");

        try
        {
            var scanner = new Scanner(new ScanOptions
            {
                Path = tempDirectory,
                Framework = ScanFrameworks.Static,
                ExcludedDirectories = ["bin"]
            });

            var result = await scanner.ScanAsync();

            Assert.Equal(1, result.TotalFilesScanned);
            Assert.DoesNotContain(result.Violations, violation => violation.FilePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_ThrowsForInvalidPath()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing");
        var scanner = new Scanner(new ScanOptions { Path = invalidPath, Framework = ScanFrameworks.Static });

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => scanner.ScanAsync());
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "cspguardian-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}

