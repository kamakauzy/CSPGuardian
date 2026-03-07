package main

import (
	"flag"
	"fmt"
	"os"
	"strings"

	"github.com/kamakauzy/CSPGuardian/go/internal/cspguard"
)

type multiValueFlag []string

func (m *multiValueFlag) String() string {
	return strings.Join(*m, ",")
}

func (m *multiValueFlag) Set(value string) error {
	*m = append(*m, value)
	return nil
}

func main() {
	if len(os.Args) < 2 || os.Args[1] != "scan" {
		printUsage()
		os.Exit(2)
	}

	fs := flag.NewFlagSet("scan", flag.ContinueOnError)
	fs.SetOutput(os.Stderr)

	var excludes multiValueFlag
	path := fs.String("path", "", "File or directory to scan")
	fs.StringVar(path, "p", "", "File or directory to scan")
	framework := fs.String("framework", cspguard.FrameworkModernDotNet, "Framework: modern-dotnet, legacy-dotnet, static, js-modern, js-legacy")
	fs.StringVar(framework, "f", cspguard.FrameworkModernDotNet, "Framework: modern-dotnet, legacy-dotnet, static, js-modern, js-legacy")
	cleanup := fs.String("cleanup", cspguard.CleanupNone, "Cleanup strategy: none, hash, nonce, externalize")
	fs.StringVar(cleanup, "c", cspguard.CleanupNone, "Cleanup strategy: none, hash, nonce, externalize")
	reportFormat := fs.String("report-format", cspguard.ReportMarkdown, "Report format: md or json")
	fs.StringVar(reportFormat, "r", cspguard.ReportMarkdown, "Report format: md or json")
	reportOutput := fs.String("report-output", "", "Report output path")
	dryRun := fs.Bool("dry-run", false, "Preview findings without writing report files")
	ciMode := fs.Bool("ci-mode", false, "Exit with code 1 when violations are found")
	fs.Var(&excludes, "exclude", "Directory name or path to exclude (repeatable)")

	if err := fs.Parse(os.Args[2:]); err != nil {
		os.Exit(2)
	}

	options := cspguard.ScanOptions{
		Path:                *path,
		Framework:           *framework,
		Cleanup:             *cleanup,
		ReportFormat:        *reportFormat,
		ReportOutput:        *reportOutput,
		ExcludedDirectories: []string(excludes),
		DryRun:              *dryRun,
		CIMode:              *ciMode,
	}

	result, err := cspguard.ExecuteScan(options)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}

	if options.CIMode && result.ViolationsFound() > 0 {
		os.Exit(1)
	}
}

func printUsage() {
	fmt.Fprintln(os.Stderr, "Usage:")
	fmt.Fprintln(os.Stderr, "  cspguard-go scan --path <path> [options]")
}
