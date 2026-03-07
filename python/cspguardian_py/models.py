from __future__ import annotations

from dataclasses import dataclass, field, asdict
from pathlib import Path


FRAMEWORK_MODERN_DOTNET = "modern-dotnet"
FRAMEWORK_LEGACY_DOTNET = "legacy-dotnet"
FRAMEWORK_STATIC = "static"
FRAMEWORK_JS_MODERN = "js-modern"
FRAMEWORK_JS_LEGACY = "js-legacy"

CLEANUP_NONE = "none"
CLEANUP_HASH = "hash"
CLEANUP_NONCE = "nonce"
CLEANUP_EXTERNALIZE = "externalize"

REPORT_MARKDOWN = "md"
REPORT_JSON = "json"

VIOLATION_INLINE_SCRIPT = "InlineScript"
VIOLATION_INLINE_STYLE = "InlineStyle"
VIOLATION_STYLE_ATTRIBUTE = "StyleAttribute"
VIOLATION_EVENT_HANDLER = "EventHandler"
VIOLATION_JAVASCRIPT_URL = "JavaScriptUrl"
VIOLATION_DYNAMIC_INLINE = "DynamicInline"
VIOLATION_LEGACY_RESOURCE = "LegacyWebResource"
VIOLATION_VIEWSTATE_SCRIPT = "ViewStateEmbedded"

DEFAULT_EXCLUDED_DIRECTORIES = [".git", "bin", "obj", "node_modules"]


@dataclass
class ScanOptions:
    path: str
    framework: str = FRAMEWORK_MODERN_DOTNET
    cleanup: str = CLEANUP_NONE
    report_format: str = REPORT_MARKDOWN
    report_output: str | None = None
    excluded_directories: list[str] = field(default_factory=list)
    dry_run: bool = False
    ci_mode: bool = False

    def normalize(self) -> "ScanOptions":
        if not self.path or not self.path.strip():
            raise ValueError("the scan path cannot be empty")

        self.path = str(Path(self.path).expanduser().resolve())
        self.framework = (self.framework or FRAMEWORK_MODERN_DOTNET).strip().lower()
        self.cleanup = (self.cleanup or CLEANUP_NONE).strip().lower()
        self.report_format = (self.report_format or REPORT_MARKDOWN).strip().lower()

        excluded = DEFAULT_EXCLUDED_DIRECTORIES + list(self.excluded_directories)
        self.excluded_directories = list(dict.fromkeys(value.strip() for value in excluded if value and value.strip()))

        if not self.report_output:
            self.report_output = str(Path(f"report.{self.report_format}").resolve())
        else:
            self.report_output = str(Path(self.report_output).expanduser().resolve())

        return self


@dataclass
class Violation:
    file_path: str
    line_number: int
    type: str
    content: str
    severity: str
    raw_content: str | None = None
    attribute_name: str | None = None
    original_text: str | None = None
    source_index: int = -1
    source_length: int = 0
    suggested_fix: str | None = None
    hash: str | None = None
    generated_asset_path: str | None = None

    def to_dict(self) -> dict:
        return asdict(self)


@dataclass
class ScanResult:
    violations: list[Violation] = field(default_factory=list)
    total_files_scanned: int = 0
    total_files_skipped: int = 0
    skipped_paths: list[str] = field(default_factory=list)
    metadata: dict = field(default_factory=dict)

    def violations_found(self) -> int:
        return len(self.violations)

    def to_dict(self) -> dict:
        return {
            "violations": [violation.to_dict() for violation in self.violations],
            "totalFilesScanned": self.total_files_scanned,
            "totalFilesSkipped": self.total_files_skipped,
            "skippedPaths": self.skipped_paths,
            "metadata": self.metadata,
        }
