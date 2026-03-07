package cspguard

import (
	"crypto/rand"
	"crypto/sha256"
	"crypto/sha512"
	"encoding/base64"
	"fmt"
	"io/fs"
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strings"
)

var (
	scriptTagPattern = regexp.MustCompile(`(?is)<script\b([^>]*)>(.*?)</script>`)
	styleTagPattern  = regexp.MustCompile(`(?is)<style\b([^>]*)>(.*?)</style>`)
	htmlTagPattern   = regexp.MustCompile(`(?is)<([a-zA-Z][\w:-]*)(\s[^<>]*?)?>`)
	evalPattern      = regexp.MustCompile(`(?i)\beval\s*\(`)
	functionPattern  = regexp.MustCompile(`(?i)\b(?:new\s+)?Function\s*\(`)
	webResource      = regexp.MustCompile(`(?i)WebResource\.axd`)
	viewState        = regexp.MustCompile(`(?is)__VIEWSTATE.*script`)
)

var supportedExtensions = map[string]struct{}{
	".html":   {},
	".htm":    {},
	".cshtml": {},
	".aspx":   {},
	".ascx":   {},
	".js":     {},
	".css":    {},
}

type invalidPathError struct {
	message string
}

func (e invalidPathError) Error() string {
	return e.message
}

func ErrInvalidPath(message string) error {
	return invalidPathError{message: message}
}

func ExecuteScan(options ScanOptions) (ScanResult, error) {
	if err := options.Normalize(); err != nil {
		return ScanResult{}, err
	}

	result, err := Scan(options)
	if err != nil {
		return ScanResult{}, err
	}

	ApplyCleanup(&result, options)

	if options.DryRun {
		fmt.Print(RenderMarkdown(result, options))
		return result, nil
	}

	reportBytes, err := RenderReport(result, options)
	if err != nil {
		return ScanResult{}, err
	}

	if err := os.WriteFile(options.ReportOutput, reportBytes, 0o644); err != nil {
		return ScanResult{}, err
	}

	return result, nil
}

func Scan(options ScanOptions) (ScanResult, error) {
	if err := options.Normalize(); err != nil {
		return ScanResult{}, err
	}

	result := ScanResult{
		Metadata: map[string]any{
			"framework": options.Framework,
			"cleanup":   options.Cleanup,
			"dryRun":    options.DryRun,
		},
	}

	info, err := os.Stat(options.Path)
	if err != nil {
		if os.IsNotExist(err) {
			return ScanResult{}, ErrInvalidPath(fmt.Sprintf("the scan path '%s' does not exist", options.Path))
		}

		return ScanResult{}, err
	}

	if !info.IsDir() {
		if _, ok := supportedExtensions[strings.ToLower(filepath.Ext(options.Path))]; !ok {
			return result, nil
		}

		violations, err := scanFile(options, options.Path)
		if err != nil {
			return ScanResult{}, err
		}

		result.TotalFilesScanned = 1
		result.Violations = append(result.Violations, violations...)
		return result, nil
	}

	err = filepath.WalkDir(options.Path, func(path string, d fs.DirEntry, walkErr error) error {
		if walkErr != nil {
			result.TotalFilesSkipped++
			result.SkippedPaths = append(result.SkippedPaths, path)
			return nil
		}

		if d.IsDir() {
			if path != options.Path && isExcludedDirectory(path, options) {
				return filepath.SkipDir
			}
			return nil
		}

		if _, ok := supportedExtensions[strings.ToLower(filepath.Ext(path))]; !ok {
			return nil
		}

		violations, err := scanFile(options, path)
		if err != nil {
			result.TotalFilesSkipped++
			result.SkippedPaths = append(result.SkippedPaths, path)
			return nil
		}

		result.TotalFilesScanned++
		result.Violations = append(result.Violations, violations...)
		return nil
	})
	if err != nil {
		return ScanResult{}, err
	}

	sort.Strings(result.SkippedPaths)
	return result, nil
}

func isExcludedDirectory(path string, options ScanOptions) bool {
	base := strings.ToLower(filepath.Base(path))
	for _, excluded := range options.ExcludedDirectories {
		if strings.ToLower(filepath.Base(excluded)) == base || strings.EqualFold(excluded, base) {
			return true
		}

		if filepath.IsAbs(excluded) {
			if normalized, err := filepath.Abs(path); err == nil && strings.EqualFold(normalized, excluded) {
				return true
			}
		}
	}

	return false
}

func scanFile(options ScanOptions, path string) ([]Violation, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}

	content := string(data)
	switch strings.ToLower(filepath.Ext(path)) {
	case ".html", ".htm", ".cshtml", ".aspx", ".ascx":
		return scanHTML(options, path, content), nil
	case ".js":
		return scanJavaScript(options, path, content), nil
	case ".css":
		return scanCSS(path, content, content, 0), nil
	default:
		return nil, nil
	}
}

func scanHTML(options ScanOptions, path, content string) []Violation {
	violations := make([]Violation, 0)
	violations = append(violations, scanInlineScripts(path, content)...)
	violations = append(violations, scanInlineStyles(path, content)...)
	violations = append(violations, scanHTMLAttributes(path, content)...)

	if options.Framework == FrameworkLegacyDotNet {
		violations = append(violations, scanLegacy(path, content)...)
	}

	return violations
}

func scanInlineScripts(path, content string) []Violation {
	violations := make([]Violation, 0)
	matches := scriptTagPattern.FindAllStringSubmatchIndex(content, -1)
	for _, match := range matches {
		fullStart, fullEnd := match[0], match[1]
		attrStart, attrEnd := match[2], match[3]
		bodyStart, bodyEnd := match[4], match[5]
		attributes := ""
		if attrStart >= 0 && attrEnd >= 0 {
			attributes = content[attrStart:attrEnd]
		}

		if hasAttribute(attributes, "src") {
			continue
		}

		body := content[bodyStart:bodyEnd]
		if strings.TrimSpace(body) == "" {
			continue
		}

		violations = append(violations, Violation{
			FilePath:     path,
			LineNumber:   lineNumberFromIndex(content, fullStart),
			Type:         ViolationInlineScript,
			Content:      truncateContent(body, 200),
			RawContent:   body,
			Severity:     "high",
			OriginalText: content[fullStart:fullEnd],
			SourceIndex:  fullStart,
			SourceLength: fullEnd - fullStart,
		})
	}

	return violations
}

func scanInlineStyles(path, content string) []Violation {
	violations := make([]Violation, 0)
	matches := styleTagPattern.FindAllStringSubmatchIndex(content, -1)
	for _, match := range matches {
		fullStart, fullEnd := match[0], match[1]
		bodyStart, bodyEnd := match[4], match[5]
		body := content[bodyStart:bodyEnd]
		if strings.TrimSpace(body) == "" {
			continue
		}

		violations = append(violations, Violation{
			FilePath:     path,
			LineNumber:   lineNumberFromIndex(content, fullStart),
			Type:         ViolationInlineStyle,
			Content:      truncateContent(body, 200),
			RawContent:   body,
			Severity:     "medium",
			OriginalText: content[fullStart:fullEnd],
			SourceIndex:  fullStart,
			SourceLength: fullEnd - fullStart,
		})

		violations = append(violations, scanCSS(path, content, body, bodyStart)...)
	}

	return violations
}

func scanHTMLAttributes(path, content string) []Violation {
	violations := make([]Violation, 0)
	matches := htmlTagPattern.FindAllStringSubmatchIndex(content, -1)
	for _, match := range matches {
		if len(match) < 6 || match[4] < 0 || match[5] < 0 {
			continue
		}

		attrStart, attrEnd := match[4], match[5]
		attributes := content[attrStart:attrEnd]
		for _, attribute := range parseAttributes(attributes) {
			absoluteIndex := attrStart + attribute.Start
			raw := attributes[attribute.Start:attribute.End]

			if strings.HasPrefix(strings.ToLower(attribute.Name), "on") {
				violations = append(violations, Violation{
					FilePath:      path,
					LineNumber:    lineNumberFromIndex(content, absoluteIndex),
					Type:          ViolationEventHandler,
					Content:       truncateContent(raw, 200),
					RawContent:    attribute.Value,
					Severity:      "high",
					AttributeName: attribute.Name,
					OriginalText:  raw,
					SourceIndex:   absoluteIndex,
					SourceLength:  attribute.End - attribute.Start,
				})
			}

			if strings.EqualFold(attribute.Name, "style") && strings.TrimSpace(attribute.Value) != "" {
				violations = append(violations, Violation{
					FilePath:      path,
					LineNumber:    lineNumberFromIndex(content, absoluteIndex),
					Type:          ViolationStyleAttribute,
					Content:       truncateContent(attribute.Value, 200),
					RawContent:    attribute.Value,
					Severity:      "medium",
					AttributeName: attribute.Name,
					OriginalText:  raw,
					SourceIndex:   absoluteIndex,
					SourceLength:  attribute.End - attribute.Start,
				})
				violations = append(violations, scanCSS(path, content, attribute.Value, absoluteIndex)...)
			}

			if strings.HasPrefix(strings.ToLower(strings.TrimSpace(attribute.Value)), "javascript:") {
				violations = append(violations, Violation{
					FilePath:      path,
					LineNumber:    lineNumberFromIndex(content, absoluteIndex),
					Type:          ViolationJavaScriptURL,
					Content:       truncateContent(raw, 200),
					RawContent:    attribute.Value,
					Severity:      "high",
					AttributeName: attribute.Name,
					OriginalText:  raw,
					SourceIndex:   absoluteIndex,
					SourceLength:  attribute.End - attribute.Start,
				})
			}
		}
	}

	return violations
}

func scanLegacy(path, content string) []Violation {
	violations := make([]Violation, 0)
	for _, match := range webResource.FindAllStringIndex(content, -1) {
		violations = append(violations, Violation{
			FilePath:     path,
			LineNumber:   lineNumberFromIndex(content, match[0]),
			Type:         ViolationLegacyResource,
			Content:      "WebResource.axd reference detected",
			Severity:     "low",
			OriginalText: content[match[0]:match[1]],
			SourceIndex:  match[0],
			SourceLength: match[1] - match[0],
		})
	}

	for _, match := range viewState.FindAllStringIndex(content, -1) {
		violations = append(violations, Violation{
			FilePath:     path,
			LineNumber:   lineNumberFromIndex(content, match[0]),
			Type:         ViolationViewStateScript,
			Content:      "ViewState with embedded script detected",
			Severity:     "medium",
			OriginalText: content[match[0]:match[1]],
			SourceIndex:  match[0],
			SourceLength: match[1] - match[0],
		})
	}

	return violations
}

func scanJavaScript(options ScanOptions, path, content string) []Violation {
	violations := make([]Violation, 0)
	for _, match := range evalPattern.FindAllStringIndex(content, -1) {
		violations = append(violations, Violation{
			FilePath:     path,
			LineNumber:   lineNumberFromIndex(content, match[0]),
			Type:         ViolationDynamicInline,
			Content:      "eval() usage detected",
			Severity:     "high",
			OriginalText: content[match[0]:match[1]],
			SourceIndex:  match[0],
			SourceLength: match[1] - match[0],
		})
	}

	for _, match := range functionPattern.FindAllStringIndex(content, -1) {
		violations = append(violations, Violation{
			FilePath:     path,
			LineNumber:   lineNumberFromIndex(content, match[0]),
			Type:         ViolationDynamicInline,
			Content:      "Function() constructor usage detected",
			Severity:     "high",
			OriginalText: content[match[0]:match[1]],
			SourceIndex:  match[0],
			SourceLength: match[1] - match[0],
		})
	}

	if options.Framework == FrameworkJSLegacy {
		for _, match := range regexp.MustCompile(`(?i)\$\(document\)\.ready\s*\([^)]*function`).FindAllStringIndex(content, -1) {
			violations = append(violations, Violation{
				FilePath:     path,
				LineNumber:   lineNumberFromIndex(content, match[0]),
				Type:         ViolationDynamicInline,
				Content:      "jQuery.ready with inline function detected",
				Severity:     "medium",
				OriginalText: content[match[0]:match[1]],
				SourceIndex:  match[0],
				SourceLength: match[1] - match[0],
			})
		}
	}

	if options.Framework == FrameworkJSModern {
		for _, match := range regexp.MustCompile(`(?i)__webpack_require__`).FindAllStringIndex(content, -1) {
			violations = append(violations, Violation{
				FilePath:     path,
				LineNumber:   lineNumberFromIndex(content, match[0]),
				Type:         ViolationDynamicInline,
				Content:      "Webpack inline pattern detected",
				Severity:     "low",
				OriginalText: content[match[0]:match[1]],
				SourceIndex:  match[0],
				SourceLength: match[1] - match[0],
			})
		}
	}

	return violations
}

func scanCSS(path, fileContent, cssContent string, baseIndex int) []Violation {
	violations := make([]Violation, 0)
	lowerContent := strings.ToLower(cssContent)
	searchIndex := 0

	for {
		offset := strings.Index(lowerContent[searchIndex:], "url(")
		if offset == -1 {
			break
		}

		start := searchIndex + offset
		cursor := start + 4
		for cursor < len(cssContent) && isWhitespace(cssContent[cursor]) {
			cursor++
		}

		quote := byte(0)
		if cursor < len(cssContent) && (cssContent[cursor] == '"' || cssContent[cursor] == '\'') {
			quote = cssContent[cursor]
			cursor++
		}

		valueStart := cursor
		for cursor < len(cssContent) {
			if quote != 0 {
				if cssContent[cursor] == quote {
					break
				}
			} else if cssContent[cursor] == ')' {
				break
			}
			cursor++
		}

		value := strings.TrimSpace(cssContent[valueStart:cursor])
		rawEnd := cursor
		if quote != 0 && cursor < len(cssContent) && cssContent[cursor] == quote {
			rawEnd++
			cursor++
		}
		for cursor < len(cssContent) && cssContent[cursor] != ')' {
			cursor++
		}
		if cursor < len(cssContent) && cssContent[cursor] == ')' {
			rawEnd = cursor + 1
		}

		if strings.HasPrefix(strings.ToLower(value), "javascript:") {
			absoluteStart := baseIndex + start
			violations = append(violations, Violation{
				FilePath:     path,
				LineNumber:   lineNumberFromIndex(fileContent, absoluteStart),
				Type:         ViolationJavaScriptURL,
				Content:      truncateContent(cssContent[start:rawEnd], 200),
				RawContent:   value,
				Severity:     "high",
				OriginalText: cssContent[start:rawEnd],
				SourceIndex:  absoluteStart,
				SourceLength: rawEnd - start,
			})
		}

		searchIndex = start + 4
	}

	return violations
}

func ApplyCleanup(result *ScanResult, options ScanOptions) {
	switch options.Cleanup {
	case CleanupHash:
		applyHashes(result, options)
	case CleanupNonce:
		applyNonces(result)
	case CleanupExternalize:
		applyExternalizeSuggestions(result)
	}

	applyManualSuggestions(result)
}

func applyHashes(result *ScanResult, options ScanOptions) {
	for index := range result.Violations {
		violation := &result.Violations[index]
		if violation.Type != ViolationInlineScript && violation.Type != ViolationInlineStyle {
			continue
		}

		source := violation.RawContent
		if source == "" {
			source = violation.Content
		}

		if options.Framework == FrameworkLegacyDotNet {
			hash := sha256.Sum256([]byte(source))
			violation.Hash = "sha256-" + base64.StdEncoding.EncodeToString(hash[:])
		} else {
			hash := sha512.Sum384([]byte(source))
			violation.Hash = "sha384-" + base64.StdEncoding.EncodeToString(hash[:])
		}

		violation.SuggestedFix = fmt.Sprintf("Add hash to CSP: '%s'", violation.Hash)
	}
}

func applyNonces(result *ScanResult) {
	bytes := make([]byte, 16)
	_, _ = rand.Read(bytes)
	nonce := encodeNonce(bytes)

	for index := range result.Violations {
		violation := &result.Violations[index]
		if violation.Type != ViolationInlineScript && violation.Type != ViolationInlineStyle {
			continue
		}

		violation.SuggestedFix = fmt.Sprintf("Add nonce='%s' and include 'nonce-%s' in CSP", nonce, nonce)
	}
}

func applyExternalizeSuggestions(result *ScanResult) {
	for index := range result.Violations {
		violation := &result.Violations[index]
		switch violation.Type {
		case ViolationInlineScript:
			violation.SuggestedFix = "Externalize this inline script into a .js file in the target runtime."
		case ViolationInlineStyle:
			violation.SuggestedFix = "Externalize this inline style block into a .css file in the target runtime."
		}
	}
}

func applyManualSuggestions(result *ScanResult) {
	for index := range result.Violations {
		violation := &result.Violations[index]
		if violation.SuggestedFix != "" {
			continue
		}

		switch violation.Type {
		case ViolationEventHandler:
			violation.SuggestedFix = "Move inline event-handler code into a script file and bind it with addEventListener."
		case ViolationStyleAttribute:
			violation.SuggestedFix = "Move inline styles into a stylesheet or use a temporary CSP nonce/hash during migration."
		case ViolationJavaScriptURL:
			violation.SuggestedFix = "Replace javascript: URLs with safe links, buttons, or scripted event handlers."
		case ViolationDynamicInline:
			violation.SuggestedFix = "Refactor dynamic code execution to avoid eval() and Function() where possible."
		case ViolationLegacyResource:
			violation.SuggestedFix = "Review legacy WebResource.axd usage and prefer bundled static assets when possible."
		case ViolationViewStateScript:
			violation.SuggestedFix = "Review ViewState-driven script generation and move executable code into static assets."
		default:
			violation.SuggestedFix = "Review and remediate this finding before enforcing a strict CSP."
		}
	}
}

type parsedAttribute struct {
	Name  string
	Value string
	Start int
	End   int
}

func parseAttributes(attributes string) []parsedAttribute {
	result := make([]parsedAttribute, 0)
	index := 0
	for index < len(attributes) {
		for index < len(attributes) && isWhitespace(attributes[index]) {
			index++
		}
		if index >= len(attributes) {
			break
		}

		start := index
		for index < len(attributes) && isAttributeNameChar(attributes[index]) {
			index++
		}
		if start == index {
			index++
			continue
		}

		name := attributes[start:index]
		for index < len(attributes) && isWhitespace(attributes[index]) {
			index++
		}
		if index >= len(attributes) || attributes[index] != '=' {
			continue
		}
		index++
		for index < len(attributes) && isWhitespace(attributes[index]) {
			index++
		}
		if index >= len(attributes) {
			break
		}

		valueStart := index
		if attributes[index] == '"' || attributes[index] == '\'' {
			quote := attributes[index]
			index++
			valueStart = index
			for index < len(attributes) && attributes[index] != quote {
				index++
			}
			value := attributes[valueStart:index]
			if index < len(attributes) {
				index++
			}
			result = append(result, parsedAttribute{Name: name, Value: value, Start: start, End: index})
			continue
		}

		for index < len(attributes) && !isWhitespace(attributes[index]) && attributes[index] != '>' {
			index++
		}
		value := attributes[valueStart:index]
		result = append(result, parsedAttribute{Name: name, Value: value, Start: start, End: index})
	}

	return result
}

func hasAttribute(attributes, name string) bool {
	for _, attribute := range parseAttributes(attributes) {
		if strings.EqualFold(attribute.Name, name) {
			return true
		}
	}
	return false
}

func isWhitespace(char byte) bool {
	return char == ' ' || char == '\t' || char == '\n' || char == '\r'
}

func isAttributeNameChar(char byte) bool {
	return char == ':' || char == '-' || char == '_' ||
		(char >= 'a' && char <= 'z') ||
		(char >= 'A' && char <= 'Z') ||
		(char >= '0' && char <= '9')
}

func lineNumberFromIndex(content string, index int) int {
	if index < 0 {
		return 0
	}

	line := 1
	for cursor := 0; cursor < len(content) && cursor < index; cursor++ {
		if content[cursor] == '\n' {
			line++
		}
	}

	return line
}

func truncateContent(content string, maxLength int) string {
	if len(content) <= maxLength {
		return content
	}

	return content[:maxLength] + "..."
}
