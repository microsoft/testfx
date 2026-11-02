#!/usr/bin/env python3

import importlib.util
import pathlib
import tempfile
import unittest


SCRIPT_PATH = pathlib.Path(__file__).with_name("check_unused_package_versions.py")
SPEC = importlib.util.spec_from_file_location("check_unused_package_versions", SCRIPT_PATH)
assert SPEC is not None
assert SPEC.loader is not None
CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK)
MSBUILD_NAMESPACE = "http://schemas.microsoft.com/developer/msbuild/2003"


class CheckUnusedPackageVersionsTests(unittest.TestCase):
    def test_matches_references_in_all_project_formats_and_global_references_case_insensitively(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "Directory.Packages.props").write_text(
                '<Project><ItemGroup>'
                '<PackageVersion Include="CS" /><PackageVersion Include="FS" />'
                '<PackageVersion Include="VB" /><PackageVersion Include="Props" />'
                '<PackageVersion Include="Targets" /><PackageVersion Include="Global" />'
                '</ItemGroup></Project>'
            )
            for extension, package in (
                (".csproj", "cs"),
                (".fsproj", "FS"),
                (".vbproj", "VB"),
                (".props", "Props"),
                (".targets", "Targets"),
            ):
                (root / f"project{extension}").write_text(
                    f"<Project><ItemGroup><PackageReference Include='{package}' /></ItemGroup></Project>"
                )
            (root / "global.props").write_text(
                '<Project><ItemGroup><GlobalPackageReference Include="Global" /></ItemGroup></Project>'
            )

            self.assertEqual([], CHECK.unused_package_versions(root))

    def test_matches_namespaced_package_references(self) -> None:
        for prefix, namespace_attribute in (("", "xmlns"), ("msbuild:", "xmlns:msbuild")):
            with self.subTest(namespace=namespace_attribute), tempfile.TemporaryDirectory() as directory:
                root = pathlib.Path(directory)
                (root / "Directory.Packages.props").write_text(
                    '<Project><ItemGroup><PackageVersion Include="Used" />'
                    '<PackageVersion Include="Global" /><PackageVersion Include="Unused" />'
                    '<PackageVersion Include="Updated" /></ItemGroup></Project>'
                )
                (root / "legacy.targets").write_text(
                    f'<{prefix}Project {namespace_attribute}="{MSBUILD_NAMESPACE}">'
                    f'<{prefix}ItemGroup><{prefix}PackageReference Include="used" />'
                    f'<{prefix}GlobalPackageReference Include="GLOBAL" />'
                    f'<{prefix}PackageReference Update="Updated" />'
                    f'<!-- <{prefix}PackageReference Include="Unused" /> -->'
                    f'</{prefix}ItemGroup></{prefix}Project>'
                )

                self.assertEqual(["Unused", "Updated"], CHECK.unused_package_versions(root))

    def test_detects_unused_namespaced_package_versions(self) -> None:
        for prefix, namespace_attribute in (("", "xmlns"), ("msbuild:", "xmlns:msbuild")):
            with self.subTest(namespace=namespace_attribute), tempfile.TemporaryDirectory() as directory:
                root = pathlib.Path(directory)
                (root / "Directory.Packages.props").write_text(
                    f'<{prefix}Project {namespace_attribute}="{MSBUILD_NAMESPACE}">'
                    f'<{prefix}ItemGroup><{prefix}PackageVersion Include="Used" />'
                    f'<{prefix}PackageVersion Include="Unused" />'
                    f'</{prefix}ItemGroup></{prefix}Project>'
                )
                (root / "project.csproj").write_text(
                    '<Project><ItemGroup><PackageReference Include="Used" /></ItemGroup></Project>'
                )

                self.assertEqual(["Unused"], CHECK.unused_package_versions(root))

    def test_reports_unused_entries_ignoring_comments_update_only_items_and_build_outputs(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "Directory.Packages.props").write_text(
                '<Project><ItemGroup><PackageVersion Include="Used" />'
                '<PackageVersion Include="Unused" /><PackageVersion Include="Updated" />'
                '<PackageVersion Include="OutputOnly" /></ItemGroup></Project>'
            )
            (root / "project.csproj").write_text(
                '<Project><ItemGroup><PackageReference Include="Used" />'
                '<PackageReference Update="Updated" />'
                '<!-- <PackageReference Include="Unused" /> -->'
                '</ItemGroup></Project>'
            )
            (root / "obj").mkdir()
            (root / "obj" / "generated.props").write_text(
                '<Project><PackageReference Include="OutputOnly" /></Project>'
            )

            self.assertEqual(
                ["OutputOnly", "Unused", "Updated"],
                CHECK.unused_package_versions(root),
            )


if __name__ == "__main__":
    unittest.main()
