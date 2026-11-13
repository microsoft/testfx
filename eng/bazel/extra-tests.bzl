"""Existing suites whose contracts require aliases, native fixtures, or test-only packages."""

load("@rules_dotnet//dotnet:defs.bzl", "publish_binary")
load(":defs.bzl", "PORTABLE_FRAMEWORKS", "testfx_binary", "testfx_test")
load(":test-assets.bzl", "testfx_fixture_app", "testfx_published_test")
load(":tests.bzl", "native_unit_test_deps")

def extra_unit_tests():
    testfx_fixture_app(
        name = "msbuild_tests_app",
        binary = ":msbuild_tests_binary",
        files = {
            ":platform_msbuild_netstandard2_0": "LegacyMSBuild/Microsoft.Testing.Platform.MSBuild.dll",
        },
    )
    directory = "test/UnitTests/Microsoft.Testing.Platform.MSBuild.UnitTests"
    testfx_binary(
        name = "msbuild_tests_binary",
        project = directory,
        exclude = [directory + "/Program.cs"],
        linked_srcs = ["//eng/bazel:NativeUnitTestMain.cs", "//eng/bazel:PortableTestFrameworkMetadata.cs"],
        defines = ["TESTFX_CRASHDUMP_IGNORE_UNSUPPORTED"],
        generate_documentation_file = False,
        appsetting_files = native.glob([directory + "/Properties/*.json"]),
        reference_aliases = {"Microsoft.Testing.Platform.MSBuild": ["global", "PlatformMSBuild"]},
        deps = native_unit_test_deps() + [
            ":platform_msbuild",
            ":msbuild_extension",
            ":test_infrastructure",
            "@paket.source//microsoft.build.framework",
            "@paket.source//microsoft.build.utilities.core",
            "@paket.source//moq",
        ],
    )

    directory = "test/UnitTests/Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests"
    testfx_binary(
        name = "server_client_tests_binary",
        project = directory,
        exclude = [directory + "/Program.cs"],
        linked_srcs = ["//eng/bazel:ServerClientTestMain.cs", "//eng/bazel:PortableTestFrameworkMetadata.cs"],
        usings = ["Polyfills"],
        generate_documentation_file = False,
        reference_aliases = {"Microsoft.Testing.Platform.ServerMode.Client.Sources": ["global", "serverclient"]},
        deps = native_unit_test_deps() + [":server_client"],
    )
    server_applications = {}
    for framework in PORTABLE_FRAMEWORKS:
        suffix = framework.replace(".", "_")
        configuration = "server_client_" + suffix + "_configuration"
        application = "server_client_" + suffix
        native.config_setting(
            name = configuration,
            flag_values = {"@rules_dotnet//dotnet:target_framework": framework},
        )
        # The alias below reaches the publish for the active TFM; the others
        # cannot be analyzed in that configuration.
        publish_binary(
            name = application,
            binary = ":server_client_tests_binary",
            target_framework = framework,
            self_contained = True,
            tags = ["manual"],
        )
        server_applications[":" + configuration] = ":" + application
    native.alias(
        name = "server_client_tests_app",
        actual = select(server_applications),
    )

    testfx_published_test(
        name = "server_client_tests",
        binary = ":server_client_tests_app",
        args = ["--minimum-expected-tests", "1"],
        size = "large",
    )
    testfx_test(
        name = "msbuild_tests",
        binary = ":msbuild_tests_app",
        args = ["--minimum-expected-tests", "1"],
        size = "large",
    )
