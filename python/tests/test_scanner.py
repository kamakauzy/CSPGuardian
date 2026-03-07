from __future__ import annotations

import tempfile
from pathlib import Path
import unittest

from cspguardian_py.models import (
    CLEANUP_HASH,
    FRAMEWORK_JS_LEGACY,
    FRAMEWORK_STATIC,
    ScanOptions,
    VIOLATION_DYNAMIC_INLINE,
    VIOLATION_EVENT_HANDLER,
    VIOLATION_INLINE_SCRIPT,
    VIOLATION_JAVASCRIPT_URL,
    VIOLATION_STYLE_ATTRIBUTE,
)
from cspguardian_py.reporting import render_markdown
from cspguardian_py.scanner import execute_scan, scan


class ScannerTests(unittest.TestCase):
    def test_scan_detects_html_and_css_findings(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            path = Path(temp_directory) / "index.html"
            path.write_text(
                "<html><head><script>alert('hash');</script></head>"
                "<body><a href=\"javascript:alert('xss')\">bad</a>"
                "<div style=\"background-image: url('javascript:alert(1)')\">x</div>"
                "<button onclick=\"alert('py')\">Click</button></body></html>"
            )

            result = scan(ScanOptions(path=str(path), framework=FRAMEWORK_STATIC))

            self.assertTrue(any(violation.type == VIOLATION_INLINE_SCRIPT for violation in result.violations))
            self.assertTrue(any(violation.type == VIOLATION_STYLE_ATTRIBUTE for violation in result.violations))
            self.assertTrue(any(violation.type == VIOLATION_EVENT_HANDLER for violation in result.violations))
            self.assertGreaterEqual(sum(violation.type == VIOLATION_JAVASCRIPT_URL for violation in result.violations), 2)

    def test_scan_skips_excluded_directories(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            temp_path = Path(temp_directory)
            (temp_path / "src").mkdir()
            (temp_path / "bin").mkdir()
            (temp_path / "src" / "included.html").write_text("<script>alert('ok')</script>")
            (temp_path / "bin" / "ignored.html").write_text("<script>alert('ignored')</script>")

            result = scan(ScanOptions(path=str(temp_path), framework=FRAMEWORK_STATIC))

            self.assertEqual(1, result.total_files_scanned)
            self.assertFalse(any("/bin/" in violation.file_path for violation in result.violations))

    def test_execute_scan_hashes_inline_blocks(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            path = Path(temp_directory) / "index.html"
            path.write_text(f"<script>{'console.log(1);' * 25}</script>")

            result = execute_scan(ScanOptions(path=str(path), framework=FRAMEWORK_STATIC, cleanup=CLEANUP_HASH))

            hashes = [violation.hash for violation in result.violations if violation.type == VIOLATION_INLINE_SCRIPT]
            self.assertEqual(1, len(hashes))
            self.assertTrue(hashes[0].startswith("sha384-"))

    def test_scan_detects_js_framework_heuristics(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            path = Path(temp_directory) / "legacy.js"
            path.write_text("$(document).ready(function () { eval(\"alert('x')\"); });")

            result = scan(ScanOptions(path=str(path), framework=FRAMEWORK_JS_LEGACY))

            self.assertGreaterEqual(sum(violation.type == VIOLATION_DYNAMIC_INLINE for violation in result.violations), 2)

    def test_render_markdown_contains_summary(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            path = Path(temp_directory) / "index.html"
            path.write_text("<script>alert('x')</script>")

            options = ScanOptions(path=str(path), framework=FRAMEWORK_STATIC)
            result = scan(options)
            report = render_markdown(result, options.normalize())

            self.assertIn("## Violations Summary", report)
            self.assertIn("InlineScript", report)


if __name__ == "__main__":
    unittest.main()
