"""Existing extension tests, including their real source and runtime-data fixtures."""

load(":defs.bzl", "testfx_binary", "testfx_test")
load(":test-assets.bzl", "testfx_fixture_app")
load(":tests.bzl", "native_unit_test_deps")

def extension_unit_tests():
    extension_files = {
        path: path
        for path in native.glob([
            "test/UnitTests/Microsoft.Testing.Extensions.UnitTests/**/*.cs",
            "src/TestFramework/TestFramework/Assertions/*.cs",
        ])
    }
    extension_files["test/Shared/WindowsAppModel/appx-manifest-conformance-vectors.json"] = "AppxManifestConformanceVectors.json"
    testfx_fixture_app(
        name = "extension_tests_app",
        binary = ":extension_tests_binary",
        repository_marker = True,
        files = extension_files,
    )
    directory = "test/UnitTests/Microsoft.Testing.Extensions.UnitTests"
    testfx_binary(
        name = "extension_tests_binary",
        project = directory,
        exclude = [directory + "/Program.cs"],
        linked_srcs = [
            "//eng/bazel:NativeUnitTestMain.cs",
            "//eng/bazel:PortableTestFrameworkMetadata.cs",
            "test/Utilities/ValidationResultExtensions.cs",
        ],
        defines = ["TESTFX_TELEMETRY", "TESTFX_CRASHDUMP_IGNORE_UNSUPPORTED"],
        usings = ["Microsoft.Testing.TestInfrastructure", "Polyfills"],
        generate_documentation_file = False,
        reference_aliases = {"Microsoft.Testing.Extensions.GitHubActionsReport": ["ghactions"]},
        deps = native_unit_test_deps() + [
            ":telemetry",
            ":hotreload",
            ":html",
            ":retry",
            ":github_report",
            ":trx_abstractions",
            ":video",
            ":logging",
            ":configuration",
            ":hosting",
            ":azure_foundry",
            ":packaged_app",
            ":test_infrastructure",
            "@paket.source//moq",
            "@testsupport.main//microsoft.extensions.configuration",
            "@testsupport.main//microsoft.extensions.hosting",
        ],
    )

    testfx_test(
        name = "extension_tests",
        binary = ":extension_tests_app",
        args = ["--minimum-expected-tests", "1"],
        size = "large",
    )
