#!/usr/bin/env python3

import contextlib
import importlib.util
import io
import pathlib
import subprocess
import sys
import tempfile
import unittest
from xml.sax.saxutils import escape


SCRIPT_PATH = pathlib.Path(__file__).with_name("check_localization.py")
SPEC = importlib.util.spec_from_file_location("check_localization", SCRIPT_PATH)
assert SPEC is not None
assert SPEC.loader is not None
CHECK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECK)


class LocalizationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = pathlib.Path(self.directory.name).resolve()
        self.neutral = self.root / "Resources.resx"
        self.neutral.write_text(
            '<root><data name="Message"><value>Hello {0}, MSTest.</value>'
            '<comment>{Locked="MSTest"}</comment></data></root>',
            encoding="utf-8",
        )
        self.catalog = self.root / "xlf" / "Resources.fr.xlf"
        self.catalog.parent.mkdir()
        self.write_catalog(self.unit())

    def unit(
        self, name: str = "Message", source: str = "Hello {0}, MSTest.",
        target: str = "MSTest, bonjour {0}.", note: str = '{Locked="MSTest"}',
        state: str = "translated",
    ) -> str:
        return (
            f'<trans-unit id="{name}"><source>{escape(source)}</source>'
            f'<target state="{state}">{escape(target)}</target>'
            f'<note>{escape(note)}</note></trans-unit>'
        )

    def write_catalog(self, body: str, original: str = "../Resources.resx") -> None:
        self.catalog.write_text(
            f'<xliff xmlns="urn:oasis:names:tc:xliff:document:1.2" version="1.2">'
            f'<file original="{original}" source-language="en" target-language="fr" datatype="xml">'
            f'<body>{body}</body></file></xliff>',
            encoding="utf-8-sig",
        )

    def findings(self) -> list:
        return CHECK.validate(self.root, [self.catalog])

    def codes(self) -> set[str]:
        return {finding.code for finding in self.findings()}

    def test_valid_translation_and_reordered_placeholders(self) -> None:
        self.assertEqual([], self.findings())

    def test_optional_target_and_nested_groups_are_legal(self) -> None:
        body = '<group id="g"><trans-unit id="Message"><source>Hello {0}, MSTest.</source>' \
               '<note>{Locked="MSTest"}</note></trans-unit></group>'
        self.write_catalog(body)
        self.assertEqual([], self.findings())

    def test_alternative_targets_are_not_duplicate_direct_targets(self) -> None:
        body = self.unit().replace(
            "</trans-unit>",
            '<alt-trans><source>Old</source><target>Ancien</target><target>Autre</target></alt-trans></trans-unit>',
        )
        self.write_catalog(body)
        self.assertNotIn("duplicate-target", self.codes())

    def test_duplicate_direct_targets(self) -> None:
        self.write_catalog(self.unit().replace("<note>", "<target>Other</target><note>"))
        self.assertIn("duplicate-target", self.codes())

    def test_duplicate_ids_across_nested_groups(self) -> None:
        self.write_catalog(self.unit() + '<group id="g">' + self.unit() + "</group>")
        self.assertIn("duplicate-unit", self.codes())

    def test_binary_unit_shares_id_namespace(self) -> None:
        self.write_catalog(self.unit() + '<bin-unit id="Message"/>')
        self.assertIn("duplicate-unit", self.codes())

    def test_ids_are_scoped_per_file(self) -> None:
        content = self.catalog.read_text(encoding="utf-8-sig")
        file = content[content.index("<file "):content.index("</xliff>")]
        self.catalog.write_text(content.replace("</xliff>", file + "</xliff>"), encoding="utf-8")
        self.assertEqual([], self.findings())

    def test_missing_and_orphaned_ids(self) -> None:
        self.write_catalog(self.unit(name="Orphan"))
        self.assertEqual({"missing-unit", "orphan-unit"}, self.codes())

    def test_source_and_comment_drift(self) -> None:
        self.write_catalog(self.unit(source="Old {0}", note="Old comment"))
        self.assertEqual({"stale-source", "stale-note"}, self.codes())

    def test_additional_notes_are_legal(self) -> None:
        self.write_catalog(self.unit().replace("</trans-unit>", "<note>Extra context</note></trans-unit>"))
        self.assertEqual([], self.findings())

    def test_missing_and_duplicate_sources(self) -> None:
        for body in (
            self.unit().replace("<source>Hello {0}, MSTest.</source>", ""),
            self.unit().replace("<source>", "<source>Other</source><source>"),
        ):
            with self.subTest(body=body):
                self.write_catalog(body)
                self.assertIn("source-count", self.codes())

    def test_format_placeholder_loss_and_addition(self) -> None:
        for target in ("MSTest, bonjour.", "MSTest {1}", "MSTest {0} {1}"):
            with self.subTest(target=target):
                self.write_catalog(self.unit(target=target))
                self.assertIn("placeholder-indices", self.codes())

    def test_invalid_target_format(self) -> None:
        self.write_catalog(self.unit(target="MSTest {0"))
        self.assertIn("target-format", self.codes())

    def test_format_recognition(self) -> None:
        for value, expected in (
            ("{1} {0}", {0, 1}),
            ("{{0}} {0,-10:N2} {1:X8}", {0, 1}),
            ("{{{0}}}", {0}),
            ("{0} {0}", {0}),
            ("plain", set()),
            ("{tfm} {0}", None),
            ('{"count": {0}}', None),
            ("{0", None),
            ("{0:bad{format}}", None),
        ):
            with self.subTest(value=value):
                self.assertEqual(expected, CHECK.format_indices(value))

    def test_named_templates_and_json_are_not_assumed_to_be_composite_formats(self) -> None:
        for value in ("MyReport_{tfm}.trx {0}", '{"count": {0}}'):
            with self.subTest(value=value):
                self.neutral.write_text(
                    f'<root><data name="Message"><value>{escape(value)}</value></data></root>',
                    encoding="utf-8",
                )
                self.write_catalog(self.unit(source=value, target=value, note=""))
                self.assertEqual([], self.findings())

    def test_locked_token_loss(self) -> None:
        self.write_catalog(self.unit(target="bonjour {0}."))
        self.assertEqual({"locked-target"}, self.codes())

    def test_invalid_neutral_lock_is_reported_once(self) -> None:
        self.neutral.write_text(
            '<root><data name="Message"><value>Hello {0}, MSTest.</value>'
            '<comment>{Locked="Other"}</comment></data></root>', encoding="utf-8",
        )
        self.write_catalog(self.unit(note='{Locked="Other"}'))
        self.assertEqual({"locked-source"}, self.codes())

    def test_new_empty_target_is_legal(self) -> None:
        self.write_catalog(self.unit(target="", state="new"))
        self.assertEqual([], self.findings())

    def test_completed_empty_target_cannot_drop_placeholders_and_locks(self) -> None:
        self.write_catalog(self.unit(target=""))
        self.assertEqual({"locked-target", "placeholder-indices"}, self.codes())

    def test_inline_codes_are_explicitly_reported_not_misrendered(self) -> None:
        self.write_catalog(self.unit().replace("Hello {0}", 'Hello <x id="1" equiv-text="{0}"/>'))
        self.assertEqual({"inline-content"}, self.codes())

    def test_xml_namespace_and_version_errors(self) -> None:
        for value, code in (
            ("<xliff>", "xml"),
            ('<xliff version="1.2"/>', "xliff-root"),
            ('<xliff xmlns="urn:oasis:names:tc:xliff:document:1.2" version="2.0"/>', "xliff-root"),
            ('<xliff xmlns="urn:oasis:names:tc:xliff:document:1.2" version="1.2"/>', "xliff-file"),
        ):
            with self.subTest(value=value):
                self.catalog.write_text(value, encoding="utf-8")
                self.assertEqual({code}, self.codes())

    def test_original_must_be_an_in_repository_resx(self) -> None:
        for original in ("", "../../Outside.resx", "../Other.txt"):
            with self.subTest(original=original):
                self.write_catalog(self.unit(), original=original)
                self.assertEqual({"original"}, self.codes())

    def test_windows_style_original_paths_work_on_all_platforms(self) -> None:
        self.write_catalog(self.unit(), original="..\\Resources.resx")
        self.assertEqual([], self.findings())

    def test_missing_or_malformed_neutral_is_an_error(self) -> None:
        self.neutral.unlink()
        self.assertEqual({"xml"}, self.codes())
        self.neutral.write_text("<root>", encoding="utf-8")
        self.assertEqual({"xml"}, self.codes())

    def test_invalid_neutral_root_has_no_cascading_orphan_errors(self) -> None:
        self.neutral.write_text("<other/>", encoding="utf-8")
        self.assertEqual({"resx-root"}, self.codes())

    def test_missing_body_and_unit_ids(self) -> None:
        self.write_catalog(self.unit().replace('id="Message"', 'id=""'))
        self.assertIn("unit-id", self.codes())
        content = self.catalog.read_text(encoding="utf-8-sig")
        self.catalog.write_text(
            content.replace("<body>", "<other>").replace("</body>", "</other>"),
            encoding="utf-8",
        )
        self.assertEqual({"xliff-body"}, self.codes())

    def test_duplicate_neutral_ids_and_missing_values(self) -> None:
        self.neutral.write_text(
            '<root><data name="Message"><value>Hello {0}, MSTest.</value>'
            '<comment>{Locked="MSTest"}</comment></data><data name="Message"/></root>',
            encoding="utf-8",
        )
        self.assertEqual({"resource-id", "resource-value"}, self.codes())

    def test_non_string_resources_are_not_required_units(self) -> None:
        content = self.neutral.read_text(encoding="utf-8")
        self.neutral.write_text(
            content.replace("</root>", '<data name="Count" type="System.Int32"><value>1</value></data></root>'),
            encoding="utf-8",
        )
        self.assertEqual([], self.findings())

    def test_xlifftasks_resource_exclusions(self) -> None:
        content = self.neutral.read_text(encoding="utf-8")
        excluded = (
            '<data name="TypedString" type="System.String"><value>Text</value></data>'
            '<data name="Binary" mimetype=""><value>Bytes</value></data>'
            '<data name=">>Designer"><value>Designer</value></data>'
            '<data name="Form.LayoutSettings"><value>Layout</value></data>'
            '<data name="FullyLocked"><value>Constant</value><comment>{Locked}</comment></data>'
            '<data name="Empty"><value> </value></data>'
        )
        self.neutral.write_text(content.replace("</root>", excluded + "</root>"), encoding="utf-8")
        self.assertEqual([], self.findings())

    def test_escaped_formatting_reordering_and_repetition_in_targets(self) -> None:
        source = "MSTest {{literal}} {0,-10:N2} {1:X8}"
        self.neutral.write_text(
            f'<root><data name="Message"><value>{source}</value>'
            '<comment>{Locked="MSTest"}</comment></data></root>',
            encoding="utf-8",
        )
        self.write_catalog(self.unit(source=source, target="MSTest {1:X8} {0} {0} {{literal}}"))
        self.assertEqual([], self.findings())

    def test_typed_and_locked_resources_are_orphans_if_translated(self) -> None:
        self.neutral.write_text(
            '<root><data name="Message"><value>Hello {0}, MSTest.</value>'
            '<comment>{Locked}</comment></data></root>', encoding="utf-8",
        )
        self.assertEqual({"orphan-unit"}, self.codes())

    def test_shared_neutral_error_is_reported_once_across_catalogs(self) -> None:
        self.neutral.write_text("<root>", encoding="utf-8")
        other = self.catalog.with_name("Resources.de.xlf")
        other.write_bytes(self.catalog.read_bytes())
        findings = CHECK.validate(self.root, [other, self.catalog])
        self.assertEqual(1, len(findings))
        self.assertEqual("xml", findings[0].code)

    def test_validation_does_not_modify_resources_or_catalogs(self) -> None:
        before = {path: path.read_bytes() for path in (self.neutral, self.catalog)}
        self.assertEqual([], self.findings())
        self.assertEqual(before, {path: path.read_bytes() for path in before})

    def test_diagnostics_group_shared_resource_cause_across_locales(self) -> None:
        self.write_catalog(self.unit(name="Orphan"))
        other = self.catalog.with_name("Resources.de.xlf")
        other.write_bytes(self.catalog.read_bytes())
        findings = CHECK.validate(self.root, [other, self.catalog])
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            CHECK.print_findings(findings)
        self.assertEqual(
            "[missing-unit] Resources.resx :: Message: neutral string resource has no unit; regenerate with UpdateXlf\n"
            "  catalogs (2):\n"
            "    xlf/Resources.de.xlf\n"
            "    xlf/Resources.fr.xlf\n"
            "[orphan-unit] Resources.resx :: Orphan: unit id is absent from the neutral string resources\n"
            "  catalogs (2):\n"
            "    xlf/Resources.de.xlf\n"
            "    xlf/Resources.fr.xlf\n"
            "4 finding(s) in 2 root-cause group(s).\n",
            output.getvalue(),
        )

    def test_cli_exit_codes_and_discovery(self) -> None:
        subprocess.run(["git", "init", "--quiet"], cwd=self.root, check=True)
        command = [sys.executable, str(SCRIPT_PATH), "--repository", str(self.root)]
        valid = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(0, valid.returncode, valid.stderr)
        self.assertIn("Checked 1 localization catalog(s).", valid.stdout)
        self.write_catalog(self.unit(name="Orphan"))
        invalid = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(1, invalid.returncode, invalid.stderr)
        self.assertIn("[orphan-unit]", invalid.stdout)
        self.catalog.unlink()
        empty = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(2, empty.returncode)
        self.assertIn("No localization catalogs found", empty.stderr)

    def test_one_orphan_across_thirteen_locales_is_one_root_cause(self) -> None:
        self.write_catalog(self.unit() + self.unit(name="Orphan"))
        paths = [self.catalog]
        for locale in ("cs", "de", "es", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant"):
            other = self.catalog.with_name(f"Resources.{locale}.xlf")
            other.write_bytes(self.catalog.read_bytes())
            paths.append(other)
        findings = CHECK.validate(self.root, paths)
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            CHECK.print_findings(findings)
        self.assertEqual(13, len(findings))
        self.assertIn("  catalogs (13):", output.getvalue())
        self.assertIn("13 finding(s) in 1 root-cause group(s).", output.getvalue())

    def test_cli_reports_invalid_repository(self) -> None:
        command = [sys.executable, str(SCRIPT_PATH), "--repository", str(self.root / "Missing")]
        result = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(2, result.returncode)
        self.assertIn("Cannot enumerate localization catalogs", result.stderr)

    def test_discovery_includes_tracked_and_new_but_not_ignored_catalogs(self) -> None:
        subprocess.run(["git", "init", "--quiet"], cwd=self.root, check=True)
        subprocess.run(
            ["git", "add", "--", self.catalog.relative_to(self.root).as_posix()],
            cwd=self.root, check=True,
        )
        new = self.catalog.with_name("Resources.de.xlf")
        new.write_bytes(self.catalog.read_bytes())
        ignored = self.root / "ignored" / "Resources.xlf"
        ignored.parent.mkdir()
        ignored.write_bytes(self.catalog.read_bytes())
        (self.root / ".gitignore").write_text("ignored/\n", encoding="utf-8")
        self.assertEqual([new, self.catalog], CHECK.catalog_paths(self.root))

    def test_cli_rejects_duplicate_targets(self) -> None:
        subprocess.run(["git", "init", "--quiet"], cwd=self.root, check=True)
        self.write_catalog(self.unit().replace("<note>", "<target>Other</target><note>"))
        result = subprocess.run(
            [sys.executable, str(SCRIPT_PATH), "--repository", str(self.root)],
            capture_output=True, text=True,
        )
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn("[duplicate-target]", result.stdout)


if __name__ == "__main__":
    unittest.main()
