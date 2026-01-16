using System.CommandLine;
using CSPGuardian.Commands;
using CSPGuardian.Core;

var rootCommand = new RootCommand("CSPGuardian - A tool to aid .NET/C# developers in fixing code that is out of spec with modern CSP policies");

var scanCommand = new Command("scan", "Scan codebase for CSP violations");

var pathOption = new Option<string>(new[] { "--path", "-p" })
{
    Description = "Path to the directory to scan",
    IsRequired = true
};

var frameworkOption = new Option<string>(
    new[] { "--framework", "-f" },
    getDefaultValue: () => "modern-dotnet")
{
    Description = "Framework type: modern-dotnet, legacy-dotnet, static, js-modern, js-legacy"
};

var cleanupOption = new Option<string>(
    new[] { "--cleanup", "-c" },
    getDefaultValue: () => "none")
{
    Description = "Cleanup strategy: externalize, hash, nonce, or none"
};

var outputOption = new Option<string>(
    new[] { "--output", "-o" },
    getDefaultValue: () => "policy.csp")
{
    Description = "Output file path for CSP policy (default: policy.csp)"
};

var dryRunOption = new Option<bool>(new[] { "--dry-run" })
{
    Description = "Perform a dry run without making changes"
};

var legacyModeOption = new Option<bool>(new[] { "--legacy-mode" })
{
    Description = "Enable legacy .NET mode (MVC 4/Web Forms)"
};

var ciModeOption = new Option<bool>(new[] { "--ci-mode" })
{
    Description = "CI/CD mode: exit with error code if violations found"
};

var reportFormatOption = new Option<string>(
    new[] { "--report-format", "-r" },
    getDefaultValue: () => "md")
{
    Description = "Report format: json, csv, md"
};

scanCommand.AddOption(pathOption);
scanCommand.AddOption(frameworkOption);
scanCommand.AddOption(cleanupOption);
scanCommand.AddOption(outputOption);
scanCommand.AddOption(dryRunOption);
scanCommand.AddOption(legacyModeOption);
scanCommand.AddOption(ciModeOption);
scanCommand.AddOption(reportFormatOption);

scanCommand.SetHandler(async (string path, string framework, string cleanup, string output, bool dryRun, bool legacyMode, bool ciMode, string reportFormat) =>
{
    var options = new ScanOptions
    {
        Path = path,
        Framework = framework,
        Cleanup = cleanup,
        Output = output,
        DryRun = dryRun,
        LegacyMode = legacyMode,
        CiMode = ciMode,
        ReportFormat = reportFormat
    };

    var handler = new ScanCommandHandler();
    var result = await handler.ExecuteAsync(options);

    if (ciMode && result.ViolationsFound > 0)
    {
        Environment.Exit(1);
    }
}, pathOption, frameworkOption, cleanupOption, outputOption, dryRunOption, legacyModeOption, ciModeOption, reportFormatOption);

rootCommand.AddCommand(scanCommand);

await rootCommand.InvokeAsync(args);
