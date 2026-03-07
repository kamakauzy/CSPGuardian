from __future__ import annotations

import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


class CLITests(unittest.TestCase):
    def test_cli_writes_report(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            temp_path = Path(temp_directory)
            source = temp_path / "index.html"
            report = temp_path / "report.md"
            source.write_text("<script>alert('cli')</script>")

            completed = self._run(
                "scan",
                "--path",
                str(source),
                "--framework",
                "static",
                "--report-output",
                str(report),
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            self.assertTrue(report.exists())
            self.assertIn("InlineScript", report.read_text())

    def test_cli_invalid_path_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            missing = Path(temp_directory) / "missing"
            completed = self._run("scan", "--path", str(missing))

            self.assertEqual(1, completed.returncode)
            self.assertIn("does not exist", completed.stderr)

    def _run(self, *args: str) -> subprocess.CompletedProcess[str]:
        env = dict(os.environ)
        env["PYTHONPATH"] = str(Path(__file__).resolve().parents[1])
        return subprocess.run(
            [sys.executable, "-m", "cspguardian_py", *args],
            capture_output=True,
            text=True,
            env=env,
            check=False,
        )


if __name__ == "__main__":
    unittest.main()
