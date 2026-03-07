using CSPGuardian.Core;
using CSPGuardian.Reporting;

namespace CSPGuardian.Commands;

public class ScanCommandHandler
{
    public async Task<ScanResult> ExecuteAsync(ScanOptions options)
    {
        options.Normalize();

        var scanner = new Scanner(options);
        var scanResult = await scanner.ScanAsync();

        if (options.Cleanup != CleanupStrategies.None)
        {
            var cleanupEngine = new CleanupEngine(options);
            await cleanupEngine.ProcessAsync(scanResult);
        }

        if (!options.DryRun && !string.IsNullOrWhiteSpace(options.Output))
        {
            var policyGenerator = new PolicyGenerator(options);
            var policy = await policyGenerator.GenerateAsync(scanResult);
            await File.WriteAllTextAsync(options.Output, policy);
        }

        if (!options.DryRun)
        {
            var reporter = new Reporter(options);
            await reporter.GenerateReportAsync(scanResult);
        }

        return scanResult;
    }
}

