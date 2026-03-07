using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using CSPGuardian.Commands;
using CSPGuardian.Core;

var rootCommand = new RootCommand("CSPGuardian - A tool to aid .NET/C# developers in fixing code that is out of spec with modern CSP policies");

var scanCommand = new Command("scan", "Scan codebase for CSP violations");

var pathOption = new Option<string>(
    aliases: new[] { "--path", "-p" },
    description: "Path to the file or directory to scan")
{
    IsRequired = true
};

var frameworkOption = new Option<string>(
    aliases: new[] { "--framework", "-f" },
    description: "Framework type: modern-dotnet, legacy-dotnet, static, js-modern, js-legacy",
    getDefaultValue: () => ScanFrameworks.ModernDotNet);

var cleanupOption = new Option<string>(
    aliases: new[] { "--cleanup", "-c" },
    description: "Cleanup strategy: externalize, hash, nonce, or none",
    getDefaultValue: () => CleanupStrategies.None);

var outputOption = new Option<string>(
    aliases: new[] { "--output", "-o" },
    description: "Output file path for CSP policy (default: policy.csp)",
    getDefaultValue: () => "policy.csp");

var reportOutputOption = new Option<string?>(
    aliases: new[] { "--report-output" },
    description: "Output file path for the generated report (default: report.<format>)");

var excludeOption = new Option<string[]>(
    aliases: new[] { "--exclude" },
    description: "Directory names or paths to exclude from recursive scans",
    getDefaultValue: () => []);
excludeOption.AllowMultipleArgumentsPerToken = true;

var dryRunOption = new Option<bool>(
    aliases: new[] { "--dry-run" },
    description: "Preview findings and fixes without writing files",
    getDefaultValue: () => false);

var legacyModeOption = new Option<bool>(
    aliases: new[] { "--legacy-mode" },
    description: "Enable legacy .NET mode (MVC 4/Web Forms)",
    getDefaultValue: () => false);

var ciModeOption = new Option<bool>(
    aliases: new[] { "--ci-mode" },
    description: "CI/CD mode: exit with error code if violations are found",
    getDefaultValue: () => false);

var reportFormatOption = new Option<string>(
    aliases: new[] { "--report-format", "-r" },
    description: "Report format: json, csv, md",
    getDefaultValue: () => ReportFormats.Markdown);

pathOption.AddValidator(result =>
{
    var value = result.GetValueForOption(pathOption);
    if (string.IsNullOrWhiteSpace(value))
    {
        result.ErrorMessage = "The scan path cannot be empty.";
        return;
    }

    var fullPath = Path.GetFullPath(value);
    if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
    {
        result.ErrorMessage = $"The scan path '{fullPath}' does not exist.";
    }
});

frameworkOption.AddValidator(result => ValidateChoice(result.GetValueForOption(frameworkOption), ScanFrameworks.All, "--framework", result));
cleanupOption.AddValidator(result => ValidateChoice(result.GetValueForOption(cleanupOption), CleanupStrategies.All, "--cleanup", result));
reportFormatOption.AddValidator(result => ValidateChoice(result.GetValueForOption(reportFormatOption), ReportFormats.All, "--report-format", result));

scanCommand.AddOption(pathOption);
scanCommand.AddOption(frameworkOption);
scanCommand.AddOption(cleanupOption);
scanCommand.AddOption(outputOption);
scanCommand.AddOption(reportOutputOption);
scanCommand.AddOption(excludeOption);
scanCommand.AddOption(dryRunOption);
scanCommand.AddOption(legacyModeOption);
scanCommand.AddOption(ciModeOption);
scanCommand.AddOption(reportFormatOption);

scanCommand.SetHandler(async (InvocationContext context) =>
{
    var parseResult = context.ParseResult;
    var options = new ScanOptions
    {
        Path = parseResult.GetValueForOption(pathOption) ?? string.Empty,
        Framework = parseResult.GetValueForOption(frameworkOption) ?? ScanFrameworks.ModernDotNet,
        Cleanup = parseResult.GetValueForOption(cleanupOption) ?? CleanupStrategies.None,
        Output = parseResult.GetValueForOption(outputOption),
        ReportOutput = parseResult.GetValueForOption(reportOutputOption),
        ExcludedDirectories = (parseResult.GetValueForOption(excludeOption) ?? []).ToList(),
        DryRun = parseResult.GetValueForOption(dryRunOption),
        LegacyMode = parseResult.GetValueForOption(legacyModeOption),
        CiMode = parseResult.GetValueForOption(ciModeOption),
        ReportFormat = parseResult.GetValueForOption(reportFormatOption) ?? ReportFormats.Markdown
    };

    var handler = new ScanCommandHandler();
    var result = await handler.ExecuteAsync(options);

    if (options.CiMode && result.ViolationsFound > 0)
    {
        context.ExitCode = 1;
    }
});

rootCommand.AddCommand(scanCommand);

return await rootCommand.InvokeAsync(args);

static void ValidateChoice(string? value, IEnumerable<string> validValues, string optionName, OptionResult result)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        result.ErrorMessage = $"{optionName} cannot be empty.";
        return;
    }

    if (!validValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
    {
        result.ErrorMessage = $"{optionName} must be one of: {string.Join(", ", validValues)}.";
    }
}
