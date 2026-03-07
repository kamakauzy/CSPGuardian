package cspguard

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestScanDetectsHTMLJSAndCSSFindings(t *testing.T) {
	t.Parallel()

	root := t.TempDir()
	file := filepath.Join(root, "index.html")
	content := "<html><head><script>alert('hash');</script><style>body { background-image: url(\"javascript:alert(1)\"); }</style></head><body><a href=\"javascript:alert('xss')\">bad</a><button onclick=\"alert('go')\">Click</button></body></html>"
	if err := os.WriteFile(file, []byte(content), 0o644); err != nil {
		t.Fatalf("write file: %v", err)
	}

	result, err := Scan(ScanOptions{Path: file, Framework: FrameworkStatic})
	if err != nil {
		t.Fatalf("scan: %v", err)
	}

	if result.ViolationsFound() != 5 {
		t.Fatalf("expected 5 violations, got %d", result.ViolationsFound())
	}

	assertContainsViolationType(t, result, ViolationInlineScript)
	assertContainsViolationType(t, result, ViolationInlineStyle)
	assertContainsViolationType(t, result, ViolationJavaScriptURL)
	assertContainsViolationType(t, result, ViolationEventHandler)
}

func TestScanSkipsExcludedDirectories(t *testing.T) {
	t.Parallel()

	root := t.TempDir()
	includedDir := filepath.Join(root, "src")
	excludedDir := filepath.Join(root, "bin")
	if err := os.MkdirAll(includedDir, 0o755); err != nil {
		t.Fatalf("mkdir included: %v", err)
	}
	if err := os.MkdirAll(excludedDir, 0o755); err != nil {
		t.Fatalf("mkdir excluded: %v", err)
	}

	if err := os.WriteFile(filepath.Join(includedDir, "included.html"), []byte("<script>alert('included')</script>"), 0o644); err != nil {
		t.Fatalf("write included: %v", err)
	}
	if err := os.WriteFile(filepath.Join(excludedDir, "ignored.html"), []byte("<script>alert('ignored')</script>"), 0o644); err != nil {
		t.Fatalf("write excluded: %v", err)
	}

	result, err := Scan(ScanOptions{Path: root, Framework: FrameworkStatic})
	if err != nil {
		t.Fatalf("scan: %v", err)
	}

	if result.TotalFilesScanned != 1 {
		t.Fatalf("expected 1 scanned file, got %d", result.TotalFilesScanned)
	}
	if strings.Contains(result.Violations[0].FilePath, string(filepath.Separator)+"bin"+string(filepath.Separator)) {
		t.Fatalf("expected excluded directory to be skipped")
	}
}

func TestApplyCleanupHashUsesRawContent(t *testing.T) {
	t.Parallel()

	result := ScanResult{
		Violations: []Violation{
			{
				Type:       ViolationInlineScript,
				Content:    strings.Repeat("a", 40) + "...",
				RawContent: strings.Repeat("console.log('hash');", 20),
			},
		},
	}

	ApplyCleanup(&result, ScanOptions{Cleanup: CleanupHash, Framework: FrameworkModernDotNet})
	if result.Violations[0].Hash == "" {
		t.Fatalf("expected hash to be generated")
	}
	if !strings.HasPrefix(result.Violations[0].Hash, "sha384-") {
		t.Fatalf("expected sha384 hash, got %s", result.Violations[0].Hash)
	}
}

func TestRenderMarkdownIncludesSummary(t *testing.T) {
	t.Parallel()

	report := RenderMarkdown(ScanResult{
		Violations: []Violation{
			{FilePath: "index.html", Type: ViolationInlineScript, LineNumber: 3, Severity: "high", Content: "alert('x');"},
		},
		Metadata: map[string]any{},
	}, ScanOptions{Framework: FrameworkStatic, Cleanup: CleanupNone})

	if !strings.Contains(report, "## Violations Summary") {
		t.Fatalf("expected summary section")
	}
	if !strings.Contains(report, "InlineScript") {
		t.Fatalf("expected inline script in report")
	}
}

func assertContainsViolationType(t *testing.T, result ScanResult, violationType string) {
	t.Helper()
	for _, violation := range result.Violations {
		if violation.Type == violationType {
			return
		}
	}

	t.Fatalf("expected violation type %s in %+v", violationType, result.Violations)
}
