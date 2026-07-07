#!/usr/bin/env python3
"""Bump the MSTest.Sdk-bundled Microsoft.Playwright.MSTest.v4 version.

Dependabot cannot reliably open this bump on its own: it discovers the package
via the anchor in Directory.Packages.props and even computes the new version,
but drops the change at PR-assembly time for this specific package (see
https://github.com/microsoft/testfx/issues/9362). This script replaces that
one Dependabot flow.

It looks up the latest STABLE Microsoft.Playwright.MSTest.v4 on nuget.org and,
when newer than what the repo bundles, rewrites BOTH coupled values in
Directory.Packages.props so they stay in sync (the _ValidateBundledSdkFeatureVersions
target fails the build otherwise):

  * the <MicrosoftPlaywrightVersion> property (consumed by the build / baked into
    the shipped SDK template), and
  * the literal <PackageVersion Include="Microsoft.Playwright.MSTest.v4" ...>.

Modes:
  update   Query nuget.org and rewrite Directory.Packages.props if a newer
           stable version exists. Emits changed/old/new to $GITHUB_OUTPUT.
  check    Validate the two coupled values are present and in sync (used by the
           workflow's pull_request self-test; no network, no writes).
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.request
from pathlib import Path

PACKAGE_ID = "Microsoft.Playwright.MSTest.v4"
FLAT_CONTAINER = (
    "https://api.nuget.org/v3-flatcontainer/"
    "microsoft.playwright.mstest.v4/index.json"
)

REPO_ROOT = Path(__file__).resolve().parents[2]
PROPS_PATH = REPO_ROOT / "Directory.Packages.props"

PROPERTY_RE = re.compile(
    r"(<MicrosoftPlaywrightVersion>)(?P<version>[^<]+)(</MicrosoftPlaywrightVersion>)"
)
PACKAGE_VERSION_RE = re.compile(
    r'(<PackageVersion\s+Include="Microsoft\.Playwright\.MSTest\.v4"\s+Version=")'
    r"(?P<version>[^\"]+)(\"\s*/>)"
)


def _stable_tuple(version: str) -> tuple[int, ...] | None:
    """Return a comparable tuple for a stable version, or None for prereleases."""
    if "-" in version or "+" in version:
        return None
    parts = version.split(".")
    if not all(p.isdigit() for p in parts):
        return None
    return tuple(int(p) for p in parts)


def latest_stable_version() -> str:
    with urllib.request.urlopen(FLAT_CONTAINER, timeout=60) as response:
        payload = json.load(response)
    candidates = []
    for version in payload.get("versions", []):
        key = _stable_tuple(version)
        if key is not None:
            candidates.append((key, version))
    if not candidates:
        raise RuntimeError(f"No stable versions found for {PACKAGE_ID}.")
    candidates.sort(key=lambda item: item[0])
    return candidates[-1][1]


def read_current_versions(text: str) -> tuple[str, str]:
    prop_match = PROPERTY_RE.search(text)
    pkg_match = PACKAGE_VERSION_RE.search(text)
    if prop_match is None:
        raise RuntimeError("Could not find <MicrosoftPlaywrightVersion> in Directory.Packages.props.")
    if pkg_match is None:
        raise RuntimeError(
            f'Could not find <PackageVersion Include="{PACKAGE_ID}" .../> in Directory.Packages.props.'
        )
    return prop_match.group("version"), pkg_match.group("version")


def set_github_output(**pairs: str) -> None:
    output_path = os.environ.get("GITHUB_OUTPUT")
    if not output_path:
        return
    with open(output_path, "a", encoding="utf-8") as handle:
        for key, value in pairs.items():
            handle.write(f"{key}={value}\n")


def cmd_check() -> int:
    text = PROPS_PATH.read_text(encoding="utf-8")
    prop_version, pkg_version = read_current_versions(text)
    if prop_version != pkg_version:
        print(
            "::error::MicrosoftPlaywrightVersion "
            f"('{prop_version}') is out of sync with the {PACKAGE_ID} "
            f"PackageVersion ('{pkg_version}')."
        )
        return 1
    print(f"OK: bundled Playwright is {prop_version} (property and PackageVersion in sync).")
    return 0


def cmd_update() -> int:
    text = PROPS_PATH.read_text(encoding="utf-8")
    current_property, current_package = read_current_versions(text)
    if current_property != current_package:
        print(
            "::error::Refusing to update: MicrosoftPlaywrightVersion "
            f"('{current_property}') and the {PACKAGE_ID} PackageVersion "
            f"('{current_package}') already disagree. Reconcile them first."
        )
        return 1

    current = current_property
    latest = latest_stable_version()
    print(f"Current bundled Playwright: {current}")
    print(f"Latest stable on nuget.org: {latest}")

    if _stable_tuple(latest) <= _stable_tuple(current):
        print("Already up to date; nothing to do.")
        set_github_output(changed="false", old_version=current, new_version=current)
        return 0

    updated = PROPERTY_RE.sub(rf"\g<1>{latest}\g<3>", text, count=1)
    updated = PACKAGE_VERSION_RE.sub(rf"\g<1>{latest}\g<3>", updated, count=1)
    PROPS_PATH.write_text(updated, encoding="utf-8")

    print(f"Updated bundled Playwright {current} -> {latest}.")
    set_github_output(changed="true", old_version=current, new_version=latest)
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["update", "check"])
    args = parser.parse_args()
    if args.mode == "check":
        return cmd_check()
    return cmd_update()


if __name__ == "__main__":
    sys.exit(main())
