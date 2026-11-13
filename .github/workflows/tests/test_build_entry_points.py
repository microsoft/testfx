#!/usr/bin/env python3

import os
import pathlib
import shutil
import subprocess
import tempfile
import unittest


REPO_ROOT = pathlib.Path(__file__).resolve().parents[3]
BASH = shutil.which("bash")


def copy_toolset(root: pathlib.Path, suffix: str) -> pathlib.Path:
    common = root / "eng" / "common"
    common.mkdir(parents=True)
    shutil.copyfile(REPO_ROOT / "global.json", root / "global.json")
    for name in ("tools", "pipeline-logging-functions"):
        shutil.copyfile(
            REPO_ROOT / "eng" / "common" / f"{name}.{suffix}",
            common / f"{name}.{suffix}",
        )
    configuration = REPO_ROOT / "eng" / f"configure-toolset.{suffix}"
    if configuration.exists():
        shutil.copyfile(configuration, root / "eng" / configuration.name)
    return common


def child_environment(root: pathlib.Path, inherited_default: str | None) -> dict[str, str]:
    environment = os.environ.copy()
    environment.pop("MSBUILDFORCEMULTITHREADED", None)
    environment.pop("MSBUILDENABLEMULTITHREADED", None)
    environment.pop("TF_BUILD", None)
    environment["NUGET_PACKAGES"] = str(root / ".packages")
    if inherited_default is not None:
        environment["MSBUILDENABLEMULTITHREADED"] = inherited_default
    return environment


@unittest.skipUnless(os.name == "nt", "Windows build entry points require Windows")
class BuildEntryPointTests(unittest.TestCase):
    def run_entry_point(
        self,
        *,
        batch: bool,
        exit_code: int = 0,
        arguments: str = "",
        inherited_default: str | None = None,
        common_entry_point: bool = False,
    ) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory) / "build entry points"
            common = copy_toolset(root, "ps1")
            shutil.copyfile(REPO_ROOT / "build.cmd", root / "build.cmd")
            shutil.copyfile(REPO_ROOT / "eng" / "build.ps1", root / "eng" / "build.ps1")
            (root / "eng" / "install-windows-sdk.ps1").write_text(
                'Write-Output "WINDOWS_SDK_STUB"\n', encoding="utf-8"
            )
            (common / "Build.ps1").write_text(
                "param([switch]$restore, [switch]$build, [switch]$pack,\n"
                "  [bool][Alias('mt')]$msbuildMultiThreaded = $true,\n"
                "  [Parameter(ValueFromRemainingArguments=$true)][string[]]$properties)\n"
                '. "$PSScriptRoot\\tools.ps1"\n'
                'Write-Output "ARCADE_BUILD_STUB"\n'
                'Write-Output "MSBUILD_MT=$env:MSBUILDENABLEMULTITHREADED"\n'
                f"exit {exit_code}\n",
                encoding="utf-8",
            )

            system32 = pathlib.Path(os.environ["SystemRoot"]) / "System32"
            powershell = system32 / "WindowsPowerShell" / "v1.0" / "powershell.exe"
            environment = child_environment(root, inherited_default)
            environment["PATH"] = str(powershell.parent) + os.pathsep + environment["PATH"]
            if batch:
                command = (
                    f'"{system32 / "cmd.exe"}" /d /s /c '
                    f'"set "PATH={powershell.parent};%PATH%" && call "{root / "build.cmd"}" -pack {arguments}"'
                )
            else:
                script = common / "Build.ps1" if common_entry_point else root / "eng" / "build.ps1"
                script_path = str(script).replace("'", "''")
                install_sdk = "" if common_entry_point else "-installWindowsSdk"
                command = [
                    str(powershell),
                    "-ExecutionPolicy",
                    "Bypass",
                    "-NoProfile",
                    "-Command",
                    f"& '{script_path}' "
                    f"{install_sdk} -restore -build -pack {arguments}; exit $LASTEXITCODE",
                ]

            result = subprocess.run(
                command,
                cwd=root,
                env=environment,
                capture_output=True,
                text=True,
                timeout=30,
            )
            if not common_entry_point:
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

    def test_entry_points_enable_multithreading_by_default(self) -> None:
        for batch, common in ((True, False), (False, False), (False, True)):
            with self.subTest(batch=batch, common=common):
                result = self.run_entry_point(batch=batch, common_entry_point=common)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn("MSBUILD_MT=1", result.stdout.splitlines())

    def test_entry_points_disable_multithreading_despite_inherited_ci_default(self) -> None:
        for batch, common in ((True, False), (False, False), (False, True)):
            with self.subTest(batch=batch, common=common):
                result = self.run_entry_point(
                    batch=batch,
                    common_entry_point=common,
                    arguments="-mt:0",
                    inherited_default="1",
                )
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn("MSBUILD_MT=0", result.stdout.splitlines())


@unittest.skipUnless(BASH, "Shell toolset tests require Bash")
class ShellToolsetTests(unittest.TestCase):
    def test_toolset_propagates_multithreading_default_and_opt_out(self) -> None:
        for enabled, inherited in (("true", None), ("false", "1")):
            with self.subTest(enabled=enabled, inherited=inherited):
                with tempfile.TemporaryDirectory() as directory:
                    root = pathlib.Path(directory) / "build entry points"
                    copy_toolset(root, "sh")
                    result = subprocess.run(
                        [
                            BASH,
                            "-c",
                            f'msbuild_multi_threaded={enabled}; '
                            'scriptroot="$PWD/eng/common"; '
                            'source "$scriptroot/tools.sh"; '
                            'printf "MSBUILD_MT=%s\\n" "$MSBUILDENABLEMULTITHREADED"',
                        ],
                        cwd=root,
                        env=child_environment(root, inherited),
                        capture_output=True,
                        text=True,
                        timeout=30,
                    )
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    expected = "1" if enabled == "true" else "0"
                    self.assertIn(f"MSBUILD_MT={expected}", result.stdout.splitlines())


if __name__ == "__main__":
    unittest.main()
