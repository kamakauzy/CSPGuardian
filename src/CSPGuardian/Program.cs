using System.CommandLine;
using CSPGuardian.Commands;
using CSPGuardian.Core;

var rootCommand = new RootCommand("CSPGuardian - A tool to aid .NET/C# developers in fixing code that is out of spec with modern CSP policies");

var scanCommand = new Command("scan", "Scan codebase for CSP violations");

var pathOption = new Option<string>(
    aliases: new[] { "--path", "-p" },
    description: "Path to the directory to scan")
{
    IsRequired = true
};

var frameworkOption = new Option<string>(
    aliases: new[] { "--framework", "-f" },
    description: "Framework type: modern-dotnet, legacy-dotnet, static, js-modern, js-legacy",
    getDefaultValue: () => "modern-dotnet");

var cleanupOption = new Option<string>(
    aliases: new[] { "--cleanup", "-c" },
    description: "Cleanup strategy: externalize, hash, nonce, or none",
    getDefaultValue: () => "none");

var outputOption = new Option<string>(
    aliases: new[] { "--output", "-o" },
    description: "Output file path for CSP policy (default: policy.csp)",
    getDefaultValue: () => "policy.csp");

var dryRunOption = new Option<bool>(
    aliases: new[] { "--dry-run" },
    description: "Perform a dry run without making changes",
    getDefaultValue: () => false);

var legacyModeOption = new Option<bool>(
    aliases: new[] { "--legacy-mode" },
    description: "Enable legacy .NET mode (MVC 4/Web Forms)",
    getDefaultValue: () => false);

var ciModeOption = new Option<bool>(
    aliases: new[] { "--ci-mode" },
    description: "CI/CD mode: exit with error code if violations found",
    getDefaultValue: () => false);

var reportFormatOption = new Option<string>(
    aliases: new[] { "--report-format", "-r" },
    description: "Report format: json, csv, md",
    getDefaultValue: () => "md");

scanCommand.AddOption(pathOption);
scanCommand.AddOption(frameworkOption);
scanCommand.AddOption(cleanupOption);
scanCommand.AddOption(outputOption);
scanCommand.AddOption(dryRunOption);
scanCommand.AddOption(legacyModeOption);
scanCommand.AddOption(ciModeOption);
scanCommand.AddOption(reportFormatOption);

scanCommand.SetHandler(async (path, framework, cleanup, output, dryRun, legacyMode, ciMode, reportFormat) =>
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

