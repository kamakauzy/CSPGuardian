from __future__ import annotations

import json
from collections import Counter
from datetime import datetime, timezone

from .models import REPORT_JSON, ScanOptions, ScanResult


def render_report(result: ScanResult, options: ScanOptions) -> str:
    if options.report_format == REPORT_JSON:
        return json.dumps(result.to_dict(), indent=2)
    return render_markdown(result, options)


def render_markdown(result: ScanResult, options: ScanOptions) -> str:
    lines = [
        "# CSPGuardian Python Scan Report",
        "",
        f"**Generated:** {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S')} UTC",
        f"**Framework:** {options.framework}",
        f"**Cleanup Strategy:** {options.cleanup}",
        f"**Files Scanned:** {result.total_files_scanned}",
        f"**Files Skipped:** {result.total_files_skipped}",
        f"**Violations Found:** {result.violations_found()}",
        f"**Dry Run:** {options.dry_run}",
        "",
    ]

    if result.skipped_paths:
        lines.extend(["## Skipped Paths", ""])
        lines.extend(f"- `{path}`" for path in result.skipped_paths)
        lines.append("")

    if result.violations_found() == 0:
        lines.append("No CSP violations detected.")
        return "\n".join(lines)

    lines.extend(["## Violations Summary", ""])
    counts = Counter(violation.type for violation in result.violations)
    lines.extend(f"- **{violation_type}**: {count}" for violation_type, count in counts.items())
    lines.extend(["", "## Detailed Violations", ""])

    for violation in result.violations:
        lines.extend(
            [
                f"### {violation.type} - {violation.file_path}",
                "",
                f"- **Line:** {violation.line_number}",
                f"- **Severity:** {violation.severity}",
                f"- **Content:** `{violation.content}`",
            ]
        )
        if violation.attribute_name:
            lines.append(f"- **Attribute:** `{violation.attribute_name}`")
        if violation.hash:
            lines.append(f"- **Hash:** `{violation.hash}`")
        if violation.suggested_fix:
            lines.append(f"- **Suggested Fix:** {violation.suggested_fix}")
        lines.append("")

    return "\n".join(lines)
