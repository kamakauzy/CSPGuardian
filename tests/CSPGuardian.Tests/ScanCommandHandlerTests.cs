using CSPGuardian.Commands;
using CSPGuardian.Core;
using Xunit;

namespace CSPGuardian.Tests;

public class ScanCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_DryRunSkipsPolicyAndReportWritesButStillPlansCleanup()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "page.html");
        var policyOutput = Path.Combine(tempDirectory, "policy.csp");
        var reportOutput = Path.Combine(tempDirectory, "report.md");
        await File.WriteAllTextAsync(tempFile, "<script>alert('dry-run');</script>");

        try
        {
            var handler = new ScanCommandHandler();
            var result = await handler.ExecuteAsync(new ScanOptions
            {
                Path = tempFile,
                Cleanup = CleanupStrategies.Hash,
                Output = policyOutput,
                ReportOutput = reportOutput,
                DryRun = true
            });

            Assert.NotNull(result.Violations.Single().Hash);
            Assert.False(File.Exists(policyOutput));
            Assert.False(File.Exists(reportOutput));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WritesPolicyAndReportWhenNotDryRun()
    {
        var tempDirectory = CreateTempDirectory();
        var tempFile = Path.Combine(tempDirectory, "page.html");
        var policyOutput = Path.Combine(tempDirectory, "policy.csp");
        var reportOutput = Path.Combine(tempDirectory, "report.md");
        await File.WriteAllTextAsync(tempFile, "<script>alert('persist');</script>");

        try
        {
            var handler = new ScanCommandHandler();
            var result = await handler.ExecuteAsync(new ScanOptions
            {
                Path = tempFile,
                Cleanup = CleanupStrategies.Hash,
                Output = policyOutput,
                ReportOutput = reportOutput
            });

            Assert.True(File.Exists(policyOutput));
            Assert.True(File.Exists(reportOutput));
            Assert.Contains("script-src 'self' 'sha384-", await File.ReadAllTextAsync(policyOutput));
            Assert.Contains("Suggested Fix", await File.ReadAllTextAsync(reportOutput));
            Assert.NotEmpty(result.Violations);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RespectsExcludedDirectories()
    {
        var tempDirectory = CreateTempDirectory();
        var sourceDirectory = Path.Combine(tempDirectory, "src");
        var excludedDirectory = Path.Combine(tempDirectory, "node_modules");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(excludedDirectory);

        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "included.html"), "<script>alert('included')</script>");
        await File.WriteAllTextAsync(Path.Combine(excludedDirectory, "ignored.html"), "<script>alert('ignored')</script>");

        try
        {
            var handler = new ScanCommandHandler();
            var result = await handler.ExecuteAsync(new ScanOptions
            {
                Path = tempDirectory,
                Cleanup = CleanupStrategies.None,
                Output = Path.Combine(tempDirectory, "policy.csp"),
                ReportOutput = Path.Combine(tempDirectory, "report.md"),
                ExcludedDirectories = ["node_modules"]
            });

            Assert.Equal(1, result.TotalFilesScanned);
            Assert.DoesNotContain(result.Violations, violation => violation.FilePath.Contains("node_modules", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForInvalidPath()
    {
        var handler = new ScanCommandHandler();
        var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => handler.ExecuteAsync(new ScanOptions
        {
            Path = invalidPath,
            Cleanup = CleanupStrategies.None
        }));
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "cspguardian-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}
