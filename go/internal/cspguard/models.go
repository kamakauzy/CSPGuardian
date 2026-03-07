package cspguard

import (
	"encoding/base64"
	"path/filepath"
	"strings"
)

const (
	FrameworkModernDotNet = "modern-dotnet"
	FrameworkLegacyDotNet = "legacy-dotnet"
	FrameworkStatic       = "static"
	FrameworkJSModern     = "js-modern"
	FrameworkJSLegacy     = "js-legacy"

	CleanupNone        = "none"
	CleanupHash        = "hash"
	CleanupNonce       = "nonce"
	CleanupExternalize = "externalize"

	ReportMarkdown = "md"
	ReportJSON     = "json"

	ViolationInlineScript    = "InlineScript"
	ViolationInlineStyle     = "InlineStyle"
	ViolationStyleAttribute  = "StyleAttribute"
	ViolationEventHandler    = "EventHandler"
	ViolationJavaScriptURL   = "JavaScriptUrl"
	ViolationDynamicInline   = "DynamicInline"
	ViolationLegacyResource  = "LegacyWebResource"
	ViolationViewStateScript = "ViewStateEmbedded"
)

var defaultExcludedDirectories = []string{".git", "bin", "obj", "node_modules"}

type ScanOptions struct {
	Path                string
	Framework           string
	Cleanup             string
	ReportFormat        string
	ReportOutput        string
	ExcludedDirectories []string
	DryRun              bool
	CIMode              bool
}

type ScanResult struct {
	Violations        []Violation    `json:"violations"`
	TotalFilesScanned int            `json:"totalFilesScanned"`
	TotalFilesSkipped int            `json:"totalFilesSkipped"`
	SkippedPaths      []string       `json:"skippedPaths"`
	Metadata          map[string]any `json:"metadata"`
}

type Violation struct {
	FilePath       string `json:"filePath"`
	LineNumber     int    `json:"lineNumber"`
	Type           string `json:"type"`
	Content        string `json:"content"`
	RawContent     string `json:"rawContent,omitempty"`
	Severity       string `json:"severity"`
	AttributeName  string `json:"attributeName,omitempty"`
	OriginalText   string `json:"originalText,omitempty"`
	SourceIndex    int    `json:"sourceIndex,omitempty"`
	SourceLength   int    `json:"sourceLength,omitempty"`
	SuggestedFix   string `json:"suggestedFix,omitempty"`
	Hash           string `json:"hash,omitempty"`
	GeneratedAsset string `json:"generatedAssetPath,omitempty"`
}

func (r ScanResult) ViolationsFound() int {
	return len(r.Violations)
}

func (o *ScanOptions) Normalize() error {
	if strings.TrimSpace(o.Path) == "" {
		return ErrInvalidPath("the scan path cannot be empty")
	}

	absolutePath, err := filepath.Abs(strings.TrimSpace(o.Path))
	if err != nil {
		return err
	}

	o.Path = absolutePath
	o.Framework = normalizeChoice(o.Framework, FrameworkModernDotNet)
	o.Cleanup = normalizeChoice(o.Cleanup, CleanupNone)
	o.ReportFormat = normalizeChoice(o.ReportFormat, ReportMarkdown)
	o.ExcludedDirectories = dedupeStrings(append(defaultExcludedDirectories, o.ExcludedDirectories...))

	if strings.TrimSpace(o.ReportOutput) == "" {
		o.ReportOutput = filepath.Join(".", "report."+o.ReportFormat)
	}

	reportOutput, err := filepath.Abs(strings.TrimSpace(o.ReportOutput))
	if err != nil {
		return err
	}

	o.ReportOutput = reportOutput
	return nil
}

func normalizeChoice(value, fallback string) string {
	if strings.TrimSpace(value) == "" {
		return fallback
	}

	return strings.ToLower(strings.TrimSpace(value))
}

func dedupeStrings(values []string) []string {
	seen := make(map[string]struct{})
	result := make([]string, 0, len(values))
	for _, value := range values {
		cleaned := strings.TrimSpace(value)
		if cleaned == "" {
			continue
		}

		key := strings.ToLower(cleaned)
		if _, ok := seen[key]; ok {
			continue
		}

		seen[key] = struct{}{}
		result = append(result, cleaned)
	}

	return result
}

func encodeNonce(data []byte) string {
	return base64.StdEncoding.EncodeToString(data)
}
