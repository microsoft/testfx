"""Existing analyzer tests with source-built MSTest and generated Recommended policy."""

load("@rules_dotnet//dotnet:defs.bzl", "publish_binary")
load(":defs.bzl", "PORTABLE_FRAMEWORKS", "testfx_binary")
load(":test-assets.bzl", "testfx_published_test")
load(":tests.bzl", "native_unit_test_deps")

def _recommended_config_impl(ctx):
    group = ctx.attr.package[OutputGroupInfo]
    configs = [
        file
        for file in group.globalconfigs.to_list()
        if file.basename == "mstest-recommended.globalconfig"
    ]
    if len(configs) != 1:
        fail("Expected exactly one generated mstest-recommended.globalconfig, found %s" % len(configs))
    return [DefaultInfo(files = depset(configs))]

_recommended_config = rule(
    implementation = _recommended_config_impl,
    attrs = {"package": attr.label(mandatory = True, providers = [OutputGroupInfo])},
)

def analyzer_unit_tests():
    _recommended_config(name = "analyzer_test_policy", package = ":analyzers_package")
    analyzer_directory = "src/Analyzers/MSTest.Analyzers"
    directory = "test/UnitTests/MSTest.Analyzers.UnitTests"
    testfx_binary(
        name = "analyzer_tests_binary",
        project = directory,
        exclude = [directory + "/Program.cs"],
        linked_srcs = [
            "//eng/bazel:NativeUnitTestMain.cs",
            "//eng/bazel:PortableTestFrameworkMetadata.cs",
            "//eng/bazel:AnalyzerReferencePackMetadata.cs",
            "test/Utilities/Microsoft.Testing.TestInfrastructure/RootFinder.cs",
            "test/Utilities/DiagnosticAssertionObserver.cs",
        ],
        defines = ["TESTFX_ISOLATED_NUGET", "TESTFX_ANALYZER_CHILD_PROBES"],
        usings = ["Microsoft.Testing.TestInfrastructure", "Polyfills"],
        analyzer_configs = [":analyzer_test_policy"],
        generate_documentation_file = False,
        appsetting_files = [directory + "/MSTest.Analyzers.UnitTests.testconfig.json"],
        # The original project references the analyzers as test subjects, not compiler plugins.
        deps = [dependency for dependency in native_unit_test_deps() if dependency != ":mstest_analyzer_plugin"] + [
            ":analyzers",
            ":codefixes",
            ":mstest_hosting",
            "@testsupport.analyzers//microsoft.codeanalysis",
            "@testsupport.analyzers//microsoft.codeanalysis.common",
            "@testsupport.analyzers//microsoft.codeanalysis.csharp.workspaces",
            "@testsupport.analyzers//microsoft.codeanalysis.csharp.analyzer.testing",
            "@testsupport.analyzers//microsoft.codeanalysis.csharp.codefix.testing",
            "@testsupport.analyzers//microsoft.codeanalysis.csharp.coderefactoring.testing",
        ],
    )
    applications = {}
    for framework in PORTABLE_FRAMEWORKS:
        suffix = framework.replace(".", "_")
        configuration = "analyzer_" + suffix + "_configuration"
        application = "analyzer_" + suffix
        native.config_setting(
            name = configuration,
            flag_values = {"@rules_dotnet//dotnet:target_framework": framework},
        )
        # The alias below reaches the publish for the active TFM; the others
        # cannot be analyzed in that configuration.
        publish_binary(
            name = application,
            binary = ":analyzer_tests_binary",
            target_framework = framework,
            self_contained = True,
            tags = ["manual"],
        )
        applications[":" + configuration] = ":" + application
    native.alias(name = "analyzer_tests_app", actual = select(applications))
    # Framework-dependent, so the test host runs it with the toolchain DOTNET_ROOT like the MSBuild artifact.
    publish_binary(
        name = "globalconfigs_generator_tool",
        binary = ":globalconfigs_generator",
        target_framework = "net10.0",
        self_contained = False,
        roll_forward_behavior = "Major",
    )
    testfx_published_test(
        name = "analyzer_tests",
        binary = ":analyzer_tests_app",
        args = ["--minimum-expected-tests", "1"],
        env_inherit = ["PROGRAMFILES", "PROGRAMFILES(X86)"],
        repository_marker = True,
        tools = {":globalconfigs_generator_tool": "TESTFX_BAZEL_GLOBAL_CONFIGS_GENERATOR"},
        files = {
            "NuGet.config": "NuGet.config",
            analyzer_directory + "/AnalyzerReleases.Shipped.md": analyzer_directory + "/AnalyzerReleases.Shipped.md",
            analyzer_directory + "/AnalyzerReleases.Unshipped.md": analyzer_directory + "/AnalyzerReleases.Unshipped.md",
            analyzer_directory + "/SuppressionReleases.md": analyzer_directory + "/SuppressionReleases.md",
        },
        size = "large",
    )
