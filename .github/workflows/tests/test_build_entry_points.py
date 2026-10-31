#!/usr/bin/env python3

import os
import pathlib
import shutil
import subprocess
import tempfile
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]


@unittest.skipUnless(os.name == "nt", "Windows build entry points require Windows")
class BuildEntryPointTests(unittest.TestCase):
    def run_entry_point(self, *, batch: bool, exit_code: int) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory) / "build entry points"
            common = root / "eng" / "common"
            common.mkdir(parents=True)
            shutil.copyfile(REPO_ROOT / "build.cmd", root / "build.cmd")
            shutil.copyfile(REPO_ROOT / "eng" / "build.ps1", root / "eng" / "build.ps1")
            (root / "eng" / "install-windows-sdk.ps1").write_text(
                'Write-Output "WINDOWS_SDK_STUB"\n', encoding="utf-8"
            )
            (common / "Build.ps1").write_text(
                "param([switch]$restore, [switch]$build, [switch]$pack)\n"
                'Write-Output "ARCADE_BUILD_STUB"\n'
                f"exit {exit_code}\n",
                encoding="utf-8",
            )

            system32 = pathlib.Path(os.environ["SystemRoot"]) / "System32"
            powershell = system32 / "WindowsPowerShell" / "v1.0" / "powershell.exe"
            environment = os.environ.copy()
            environment["PATH"] = str(powershell.parent) + os.pathsep + environment["PATH"]
            if batch:
                command = (
                    f'"{system32 / "cmd.exe"}" /d /s /c '
                    f'"set "PATH={powershell.parent};%PATH%" && call "{root / "build.cmd"}" -pack"'
                )
            else:
                command = [
                    str(powershell),
                    "-ExecutionPolicy",
                    "Bypass",
                    "-NoProfile",
                    "-File",
                    str(root / "eng" / "build.ps1"),
                    "-installWindowsSdk",
                    "-restore",
                    "-build",
                    "-pack",
                ]

            result = subprocess.run(
                command,
                cwd=root,
                env=environment,
                capture_output=True,
                text=True,
                timeout=30,
            )
            self.assertIn("WINDOWS_SDK_STUB", result.stdout, result.stderr)
            self.assertIn("ARCADE_BUILD_STUB", result.stdout, result.stderr)
            return result

    def test_powershell_entry_point_preserves_arcade_exit_code(self) -> None:
        for exit_code in (0, 23):
            with self.subTest(exit_code=exit_code):
                result = self.run_entry_point(batch=False, exit_code=exit_code)
                self.assertEqual(exit_code, result.returncode, result.stdout + result.stderr)

    def test_batch_entry_point_preserves_arcade_exit_code(self) -> None:
        for exit_code in (0, 23):
            with self.subTest(exit_code=exit_code):
                result = self.run_entry_point(batch=True, exit_code=exit_code)
                self.assertEqual(exit_code, result.returncode, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
