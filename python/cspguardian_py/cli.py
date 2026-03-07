from __future__ import annotations

import argparse
from pathlib import Path
import sys

from .models import (
    CLEANUP_NONE,
    FRAMEWORK_MODERN_DOTNET,
    REPORT_MARKDOWN,
    ScanOptions,
)
from .reporting import render_report
from .scanner import execute_scan


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="cspguard-py")
    subparsers = parser.add_subparsers(dest="command", required=True)

    scan_parser = subparsers.add_parser("scan", help="Scan codebase for CSP violations")
    scan_parser.add_argument("-p", "--path", required=True, help="File or directory to scan")
    scan_parser.add_argument(
        "-f",
        "--framework",
        default=FRAMEWORK_MODERN_DOTNET,
        help="Framework: modern-dotnet, legacy-dotnet, static, js-modern, js-legacy",
    )
    scan_parser.add_argument(
        "-c",
        "--cleanup",
        default=CLEANUP_NONE,
        help="Cleanup strategy: none, hash, nonce, externalize",
    )
    scan_parser.add_argument("-r", "--report-format", default=REPORT_MARKDOWN, help="Report format: md or json")
    scan_parser.add_argument("--report-output", help="Report output path")
    scan_parser.add_argument("--exclude", action="append", default=[], help="Directory name or path to exclude")
    scan_parser.add_argument("--dry-run", action="store_true", help="Preview findings without writing report files")
    scan_parser.add_argument("--ci-mode", action="store_true", help="Exit with code 1 when violations are found")

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    options = ScanOptions(
        path=args.path,
        framework=args.framework,
        cleanup=args.cleanup,
        report_format=args.report_format,
        report_output=args.report_output,
        excluded_directories=args.exclude,
        dry_run=args.dry_run,
        ci_mode=args.ci_mode,
    )

    try:
        result = execute_scan(options)
    except (FileNotFoundError, ValueError) as error:
        print(error, file=sys.stderr)
        return 1

    rendered = render_report(result, options.normalize())
    if options.dry_run:
        print(rendered)
    else:
        Path(options.report_output).write_text(rendered)

    if options.ci_mode and result.violations_found() > 0:
        return 1

    return 0
