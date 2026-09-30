#!/usr/bin/env python3

import importlib.util
import pathlib
import tempfile
import unittest


SCRIPT_PATH = pathlib.Path(__file__).with_name("check_source_bom.py")
SPEC = importlib.util.spec_from_file_location("check_source_bom", SCRIPT_PATH)
assert SPEC is not None
assert SPEC.loader is not None
CHECK_SOURCE_BOM = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK_SOURCE_BOM)


class CheckSourceBomTests(unittest.TestCase):
    def test_missing_bom_files_reports_code_files_without_bom(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            source = pathlib.Path(directory) / "Missing.cs"
            source.write_bytes(b"class Example {}")

            self.assertEqual([source], CHECK_SOURCE_BOM.missing_bom_files([source]))

    def test_missing_bom_files_accepts_bom_and_ignores_non_code_files(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            source = root / "WithBom.vb"
            source.write_bytes(CHECK_SOURCE_BOM.UTF8_BOM + b"Class Example")
            data = root / "Data.txt"
            data.write_bytes(b"no BOM required")

            self.assertEqual([], CHECK_SOURCE_BOM.missing_bom_files([source, data]))


if __name__ == "__main__":
    unittest.main()
