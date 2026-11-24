#!/usr/bin/env python3

import os
import pathlib
import subprocess
import sys


UTF8_BOM = b"\xef\xbb\xbf"
SOURCE_EXTENSIONS = {".cs", ".csx", ".vb", ".vbx"}


def tracked_source_files(repository: pathlib.Path) -> list[pathlib.Path]:
    result = subprocess.run(
        [
            "git",
            "ls-files",
            "-z",
            "--cached",
            "--others",
            "--exclude-standard",
            "--",
            "*.cs",
            "*.csx",
            "*.vb",
            "*.vbx",
        ],
        cwd=repository,
        check=True,
        stdout=subprocess.PIPE,
    )
    return [
        repository / os.fsdecode(path)
        for path in result.stdout.split(b"\0")
        if path
    ]


def missing_bom_files(files: list[pathlib.Path]) -> list[pathlib.Path]:
    missing = []
    for path in files:
        if not path.is_file() or path.suffix.lower() not in SOURCE_EXTENSIONS:
            continue

        with path.open("rb") as source_file:
            if source_file.read(3) != UTF8_BOM:
                missing.append(path)

    return missing


def main() -> int:
    repository = pathlib.Path(__file__).resolve().parents[2]
    missing = missing_bom_files(tracked_source_files(repository))
    if missing:
        print("The following C#/VB source files are missing the UTF-8 BOM required by .editorconfig:")
        for path in missing:
            print(f"  {path.relative_to(repository)}")
        return 1

    print("All C#/VB source files have a UTF-8 BOM.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
