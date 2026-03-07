package cspguard

import (
	"encoding/json"
	"fmt"
	"strings"
	"time"
)

func RenderReport(result ScanResult, options ScanOptions) ([]byte, error) {
	switch options.ReportFormat {
	case ReportJSON:
		return json.MarshalIndent(result, "", "  ")
	default:
		return []byte(RenderMarkdown(result, options)), nil
	}
}

func RenderMarkdown(result ScanResult, options ScanOptions) string {
	var builder strings.Builder
	builder.WriteString("# CSPGuardian Go Scan Report\n\n")
	builder.WriteString(fmt.Sprintf("**Generated:** %s UTC\n", time.Now().UTC().Format("2006-01-02 15:04:05")))
	builder.WriteString(fmt.Sprintf("**Framework:** %s\n", options.Framework))
	builder.WriteString(fmt.Sprintf("**Cleanup Strategy:** %s\n", options.Cleanup))
	builder.WriteString(fmt.Sprintf("**Files Scanned:** %d\n", result.TotalFilesScanned))
	builder.WriteString(fmt.Sprintf("**Files Skipped:** %d\n", result.TotalFilesSkipped))
	builder.WriteString(fmt.Sprintf("**Violations Found:** %d\n", result.ViolationsFound()))
	builder.WriteString(fmt.Sprintf("**Dry Run:** %t\n\n", options.DryRun))

	if len(result.SkippedPaths) > 0 {
		builder.WriteString("## Skipped Paths\n\n")
		for _, path := range result.SkippedPaths {
			builder.WriteString(fmt.Sprintf("- `%s`\n", path))
		}
		builder.WriteString("\n")
	}

	if result.ViolationsFound() == 0 {
		builder.WriteString("No CSP violations detected.\n")
		return builder.String()
	}

	builder.WriteString("## Violations Summary\n\n")
	counts := make(map[string]int)
	for _, violation := range result.Violations {
		counts[violation.Type]++
	}
	for violationType, count := range counts {
		builder.WriteString(fmt.Sprintf("- **%s**: %d\n", violationType, count))
	}

	builder.WriteString("\n## Detailed Violations\n\n")
	for _, violation := range result.Violations {
		builder.WriteString(fmt.Sprintf("### %s - %s\n\n", violation.Type, violation.FilePath))
		builder.WriteString(fmt.Sprintf("- **Line:** %d\n", violation.LineNumber))
		builder.WriteString(fmt.Sprintf("- **Severity:** %s\n", violation.Severity))
		builder.WriteString(fmt.Sprintf("- **Content:** `%s`\n", violation.Content))
		if violation.AttributeName != "" {
			builder.WriteString(fmt.Sprintf("- **Attribute:** `%s`\n", violation.AttributeName))
		}
		if violation.Hash != "" {
			builder.WriteString(fmt.Sprintf("- **Hash:** `%s`\n", violation.Hash))
		}
		if violation.SuggestedFix != "" {
			builder.WriteString(fmt.Sprintf("- **Suggested Fix:** %s\n", violation.SuggestedFix))
		}
		builder.WriteString("\n")
	}

	return builder.String()
}
