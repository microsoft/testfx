#!/usr/bin/env python3
"""Fail when a central PackageVersion has no PackageReference in repository projects."""

import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT_EXTENSIONS = {".csproj", ".fsproj", ".vbproj", ".props", ".targets"}
EXCLUDED_DIRECTORIES = {".git", ".dotnet", "artifacts", "bin", "obj", "node_modules"}


def unused_package_versions(root: Path) -> list[str]:
    versions = {
        element.attrib["Include"].casefold(): element.attrib["Include"]
        for element in ET.parse(root / "Directory.Packages.props").iter("PackageVersion")
    }
    references = set()
    for directory, subdirectories, files in os.walk(root):
        subdirectories[:] = [name for name in subdirectories if name not in EXCLUDED_DIRECTORIES]
        for name in files:
            if Path(name).suffix not in PROJECT_EXTENSIONS:
                continue
            for element in ET.parse(Path(directory) / name).iter():
                if element.tag in {"PackageReference", "GlobalPackageReference"}:
                    if include := element.get("Include"):
                        references.add(include.casefold())
    return sorted((name for key, name in versions.items() if key not in references), key=str.casefold)


if __name__ == "__main__":
    try:
        unused = unused_package_versions(ROOT)
    except (OSError, ET.ParseError) as error:
        print(f"Cannot check central package versions: {error}", file=sys.stderr)
        sys.exit(1)
    if unused:
        print("Unused central PackageVersion entries: " + ", ".join(unused), file=sys.stderr)
        sys.exit(1)
    print("All central PackageVersion entries have a PackageReference.")
