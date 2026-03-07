from __future__ import annotations

import base64
import hashlib
import os
import re
import secrets
from pathlib import Path

from .models import (
    CLEANUP_EXTERNALIZE,
    CLEANUP_HASH,
    CLEANUP_NONCE,
    FRAMEWORK_JS_LEGACY,
    FRAMEWORK_JS_MODERN,
    FRAMEWORK_LEGACY_DOTNET,
    ScanOptions,
    ScanResult,
    Violation,
    VIOLATION_DYNAMIC_INLINE,
    VIOLATION_EVENT_HANDLER,
    VIOLATION_INLINE_SCRIPT,
    VIOLATION_INLINE_STYLE,
    VIOLATION_JAVASCRIPT_URL,
    VIOLATION_LEGACY_RESOURCE,
    VIOLATION_STYLE_ATTRIBUTE,
    VIOLATION_VIEWSTATE_SCRIPT,
)

SUPPORTED_EXTENSIONS = {".html", ".htm", ".cshtml", ".aspx", ".ascx", ".js", ".css"}

SCRIPT_TAG_RE = re.compile(r"<script\b(?P<attributes>[^>]*)>(?P<body>.*?)</script>", re.IGNORECASE | re.DOTALL)
STYLE_TAG_RE = re.compile(r"<style\b(?P<attributes>[^>]*)>(?P<body>.*?)</style>", re.IGNORECASE | re.DOTALL)
HTML_TAG_RE = re.compile(r"<(?P<tag>[a-zA-Z][\w:-]*)(?P<attributes>\s[^<>]*?)?>", re.IGNORECASE | re.DOTALL)
ATTRIBUTE_RE = re.compile(r'(?P<name>[\w:-]+)\s*=\s*(?P<quote>["\'])(?P<value>.*?)\2', re.IGNORECASE | re.DOTALL)
EVAL_RE = re.compile(r"\beval\s*\(", re.IGNORECASE)
FUNCTION_RE = re.compile(r"\b(?:new\s+)?Function\s*\(", re.IGNORECASE)
WEBPACK_RE = re.compile(r"__webpack_require__", re.IGNORECASE)
JQUERY_READY_RE = re.compile(r"\$\(document\)\.ready\s*\([^)]*function", re.IGNORECASE)
WEBRESOURCE_RE = re.compile(r"WebResource\.axd", re.IGNORECASE)
VIEWSTATE_RE = re.compile(r"__VIEWSTATE.*script", re.IGNORECASE | re.DOTALL)
CSS_JS_URL_RE = re.compile(r'url\(\s*(?:(["\'])(javascript\s*:.*?)\1|(javascript\s*:[^)]+))\s*\)', re.IGNORECASE | re.DOTALL)


def execute_scan(options: ScanOptions) -> ScanResult:
    options.normalize()
    result = scan(options)
    apply_cleanup(result, options)
    return result


def scan(options: ScanOptions) -> ScanResult:
    options.normalize()
    scan_root = Path(options.path)
    if not scan_root.exists():
        raise FileNotFoundError(f"the scan path '{scan_root}' does not exist")

    result = ScanResult(metadata={"framework": options.framework, "cleanup": options.cleanup, "dryRun": options.dry_run})

    if scan_root.is_file():
        if scan_root.suffix.lower() in SUPPORTED_EXTENSIONS:
            result.violations.extend(_scan_file(scan_root, options))
            result.total_files_scanned = 1
        return result

    for root, dir_names, file_names in os.walk(scan_root):
        dir_names[:] = [name for name in dir_names if not _is_excluded(Path(root) / name, options)]
        for file_name in file_names:
            path = Path(root) / file_name
            if path.suffix.lower() not in SUPPORTED_EXTENSIONS:
                continue
            try:
                result.violations.extend(_scan_file(path, options))
                result.total_files_scanned += 1
            except OSError:
                result.total_files_skipped += 1
                result.skipped_paths.append(str(path))

    return result


def _scan_file(path: Path, options: ScanOptions) -> list[Violation]:
    content = path.read_text()
    suffix = path.suffix.lower()
    if suffix in {".html", ".htm", ".cshtml", ".aspx", ".ascx"}:
        return _scan_html(path, content, options)
    if suffix == ".js":
        return _scan_js(path, content, options)
    if suffix == ".css":
        return _scan_css(str(path), content, content, 0)
    return []


def _scan_html(path: Path, content: str, options: ScanOptions) -> list[Violation]:
    violations = []
    violations.extend(_scan_inline_scripts(str(path), content))
    violations.extend(_scan_inline_styles(str(path), content))
    violations.extend(_scan_html_attributes(str(path), content))
    if options.framework == FRAMEWORK_LEGACY_DOTNET:
        violations.extend(_scan_legacy(str(path), content))
    return violations


def _scan_inline_scripts(file_path: str, content: str) -> list[Violation]:
    violations = []
    for match in SCRIPT_TAG_RE.finditer(content):
        if _has_attribute(match.group("attributes") or "", "src"):
            continue
        body = match.group("body")
        if not body or not body.strip():
            continue
        violations.append(
            Violation(
                file_path=file_path,
                line_number=_line_number(content, match.start()),
                type=VIOLATION_INLINE_SCRIPT,
                content=_truncate(body, 200),
                raw_content=body,
                severity="high",
                original_text=match.group(0),
                source_index=match.start(),
                source_length=match.end() - match.start(),
            )
        )
    return violations


def _scan_inline_styles(file_path: str, content: str) -> list[Violation]:
    violations = []
    for match in STYLE_TAG_RE.finditer(content):
        body = match.group("body")
        if not body or not body.strip():
            continue
        violations.append(
            Violation(
                file_path=file_path,
                line_number=_line_number(content, match.start()),
                type=VIOLATION_INLINE_STYLE,
                content=_truncate(body, 200),
                raw_content=body,
                severity="medium",
                original_text=match.group(0),
                source_index=match.start(),
                source_length=match.end() - match.start(),
            )
        )
        violations.extend(_scan_css(file_path, content, body, match.start("body")))
    return violations


def _scan_html_attributes(file_path: str, content: str) -> list[Violation]:
    violations = []
    for tag_match in HTML_TAG_RE.finditer(content):
        attributes = tag_match.group("attributes") or ""
        for attribute_match in ATTRIBUTE_RE.finditer(attributes):
            absolute_index = tag_match.start("attributes") + attribute_match.start()
            attribute_name = attribute_match.group("name")
            attribute_value = attribute_match.group("value")
            raw = attribute_match.group(0)

            if attribute_name.lower().startswith("on"):
                violations.append(
                    Violation(
                        file_path=file_path,
                        line_number=_line_number(content, absolute_index),
                        type=VIOLATION_EVENT_HANDLER,
                        content=_truncate(raw, 200),
                        raw_content=attribute_value,
                        severity="high",
                        attribute_name=attribute_name,
                        original_text=raw,
                        source_index=absolute_index,
                        source_length=len(raw),
                    )
                )

            if attribute_name.lower() == "style" and attribute_value.strip():
                violations.append(
                    Violation(
                        file_path=file_path,
                        line_number=_line_number(content, absolute_index),
                        type=VIOLATION_STYLE_ATTRIBUTE,
                        content=_truncate(attribute_value, 200),
                        raw_content=attribute_value,
                        severity="medium",
                        attribute_name=attribute_name,
                        original_text=raw,
                        source_index=absolute_index,
                        source_length=len(raw),
                    )
                )
                violations.extend(_scan_css(file_path, content, attribute_value, absolute_index))

            if attribute_value.lstrip().lower().startswith("javascript:"):
                violations.append(
                    Violation(
                        file_path=file_path,
                        line_number=_line_number(content, absolute_index),
                        type=VIOLATION_JAVASCRIPT_URL,
                        content=_truncate(raw, 200),
                        raw_content=attribute_value,
                        severity="high",
                        attribute_name=attribute_name,
                        original_text=raw,
                        source_index=absolute_index,
                        source_length=len(raw),
                    )
                )
    return violations


def _scan_legacy(file_path: str, content: str) -> list[Violation]:
    violations = []
    for match in WEBRESOURCE_RE.finditer(content):
        violations.append(
            Violation(
                file_path=file_path,
                line_number=_line_number(content, match.start()),
                type=VIOLATION_LEGACY_RESOURCE,
                content="WebResource.axd reference detected",
                severity="low",
                original_text=match.group(0),
                source_index=match.start(),
                source_length=match.end() - match.start(),
            )
        )
    for match in VIEWSTATE_RE.finditer(content):
        violations.append(
            Violation(
                file_path=file_path,
                line_number=_line_number(content, match.start()),
                type=VIOLATION_VIEWSTATE_SCRIPT,
                content="ViewState with embedded script detected",
                severity="medium",
                original_text=match.group(0),
                source_index=match.start(),
                source_length=match.end() - match.start(),
            )
        )
    return violations


def _scan_js(path: Path, content: str, options: ScanOptions) -> list[Violation]:
    violations = []
    for match in EVAL_RE.finditer(content):
        violations.append(
            Violation(
                file_path=str(path),
                line_number=_line_number(content, match.start()),
                type=VIOLATION_DYNAMIC_INLINE,
                content="eval() usage detected",
                severity="high",
                original_text=match.group(0),
                source_index=match.start(),
                source_length=match.end() - match.start(),
            )
        )
    for match in FUNCTION_RE.finditer(content):
        violations.append(
            Violation(
                file_path=str(path),
                line_number=_line_number(content, match.start()),
                type=VIOLATION_DYNAMIC_INLINE,
                content="Function() constructor usage detected",
                severity="high",
                original_text=match.group(0),
                source_index=match.start(),
                source_length=match.end() - match.start(),
            )
        )

    if options.framework == FRAMEWORK_JS_MODERN:
        for match in WEBPACK_RE.finditer(content):
            violations.append(
                Violation(
                    file_path=str(path),
                    line_number=_line_number(content, match.start()),
                    type=VIOLATION_DYNAMIC_INLINE,
                    content="Webpack inline pattern detected",
                    severity="low",
                    original_text=match.group(0),
                    source_index=match.start(),
                    source_length=match.end() - match.start(),
                )
            )

    if options.framework == FRAMEWORK_JS_LEGACY:
        for match in JQUERY_READY_RE.finditer(content):
            violations.append(
                Violation(
                    file_path=str(path),
                    line_number=_line_number(content, match.start()),
                    type=VIOLATION_DYNAMIC_INLINE,
                    content="jQuery.ready with inline function detected",
                    severity="medium",
                    original_text=match.group(0),
                    source_index=match.start(),
                    source_length=match.end() - match.start(),
                )
            )

    return violations


def _scan_css(file_path: str, file_content: str, css_content: str, base_index: int) -> list[Violation]:
    violations = []
    for match in CSS_JS_URL_RE.finditer(css_content):
        value = match.group(2) or match.group(3) or ""
        absolute_index = base_index + match.start()
        violations.append(
            Violation(
                file_path=file_path,
                line_number=_line_number(file_content, absolute_index),
                type=VIOLATION_JAVASCRIPT_URL,
                content=_truncate(match.group(0), 200),
                raw_content=value,
                severity="high",
                original_text=match.group(0),
                source_index=absolute_index,
                source_length=match.end() - match.start(),
            )
        )
    return violations


def apply_cleanup(result: ScanResult, options: ScanOptions) -> None:
    if options.cleanup == CLEANUP_HASH:
        _apply_hashes(result, options)
    elif options.cleanup == CLEANUP_NONCE:
        _apply_nonces(result)
    elif options.cleanup == CLEANUP_EXTERNALIZE:
        for violation in result.violations:
            if violation.type == VIOLATION_INLINE_SCRIPT:
                violation.suggested_fix = "Externalize this inline script into a .js file in the target runtime."
            elif violation.type == VIOLATION_INLINE_STYLE:
                violation.suggested_fix = "Externalize this inline style block into a .css file in the target runtime."

    for violation in result.violations:
        if violation.suggested_fix:
            continue
        violation.suggested_fix = {
            VIOLATION_EVENT_HANDLER: "Move inline event-handler code into a script file and bind it with addEventListener.",
            VIOLATION_STYLE_ATTRIBUTE: "Move inline styles into a stylesheet or use a temporary CSP nonce/hash during migration.",
            VIOLATION_JAVASCRIPT_URL: "Replace javascript: URLs with safe links, buttons, or scripted event handlers.",
            VIOLATION_DYNAMIC_INLINE: "Refactor dynamic code execution to avoid eval() and Function() where possible.",
            VIOLATION_LEGACY_RESOURCE: "Review legacy WebResource.axd usage and prefer bundled static assets when possible.",
            VIOLATION_VIEWSTATE_SCRIPT: "Review ViewState-driven script generation and move executable code into static assets.",
        }.get(violation.type, "Review and remediate this finding before enforcing a strict CSP.")


def _apply_hashes(result: ScanResult, options: ScanOptions) -> None:
    algorithm = hashlib.sha256 if options.framework == FRAMEWORK_LEGACY_DOTNET else hashlib.sha384
    algorithm_name = "sha256" if options.framework == FRAMEWORK_LEGACY_DOTNET else "sha384"
    for violation in result.violations:
        if violation.type not in {VIOLATION_INLINE_SCRIPT, VIOLATION_INLINE_STYLE}:
            continue
        source = violation.raw_content or violation.content
        digest = base64.b64encode(algorithm(source.encode("utf-8")).digest()).decode("ascii")
        violation.hash = f"{algorithm_name}-{digest}"
        violation.suggested_fix = f"Add hash to CSP: '{violation.hash}'"


def _apply_nonces(result: ScanResult) -> None:
    nonce = base64.b64encode(secrets.token_bytes(16)).decode("ascii")
    for violation in result.violations:
        if violation.type in {VIOLATION_INLINE_SCRIPT, VIOLATION_INLINE_STYLE}:
            violation.suggested_fix = f"Add nonce='{nonce}' and include 'nonce-{nonce}' in CSP"


def _is_excluded(path: Path, options: ScanOptions) -> bool:
    base = path.name.lower()
    normalized = str(path.resolve()).lower()
    for excluded in options.excluded_directories:
        cleaned = excluded.lower()
        if cleaned == base or cleaned == normalized or Path(cleaned).name == base:
            return True
    return False


def _has_attribute(attributes: str, name: str) -> bool:
    return any(match.group("name").lower() == name.lower() for match in ATTRIBUTE_RE.finditer(attributes))


def _line_number(content: str, index: int) -> int:
    return content.count("\n", 0, max(index, 0)) + 1


def _truncate(content: str, max_length: int) -> str:
    return content if len(content) <= max_length else content[:max_length] + "..."
