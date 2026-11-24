"""Existing portable samples, with native source dependencies and unchanged test bodies."""

load("@rules_dotnet//dotnet:defs.bzl", "import_library", "publish_binary")
load(":compatibility.bzl", "testfx_framework_variant")
load(":defs.bzl", "testfx_analyzer", "testfx_binary", "testfx_library", "testfx_package_files", "testfx_test")
load(":test-assets.bzl", "testfx_published_test")

def portable_samples():
    for name, project, defines, minimum in [
        ("reliable_suite", "samples/public/ReliableTestSuite", ["TESTFX_METHOD_PARALLEL", "TESTFX_TOOLCHAIN_ON_PATH"], "1"),
        ("sdk_runner_sample", "samples/public/DemoMSTestSdk/ProjectUsingMSTestRunner", [], "1"),
        ("scale_10c", "samples/public/mstest-runner/runner_vs_vstest/10C_100M", [], "1000"),
        ("scale_100c", "samples/public/mstest-runner/runner_vs_vstest/100C_100M", [], "10000"),
        ("scale_1000c", "samples/public/mstest-runner/runner_vs_vstest/1000C_100M", [], "100000"),
    ]:
        testfx_binary(
            name = name,
            project = project,
            target_frameworks = ["net10.0"],
            polyfills = False,
            linked_srcs = ["//eng/bazel:TestMain.cs", "//eng/bazel:PortableTestFrameworkMetadata.cs"],
            defines = defines,
            generate_documentation_file = False,
            deps = [":adapter", ":framework", ":trx"],
        )
        testfx_test(
            name = name + "_test",
            binary = ":" + name,
            args = ["--minimum-expected-tests", minimum],
            size = "large" if name == "scale_1000c" else "medium",
        )

    testfx_binary(
        name = "custom_report",
        project = "samples/public/mstest-runner/CustomReportExtension/CustomReportExtension",
        linked_srcs = ["//eng/bazel:PortableTestFrameworkMetadata.cs"],
        target_frameworks = ["net10.0"],
        polyfills = False,
        usings = ["Microsoft.VisualStudio.TestTools.UnitTesting"],
        generate_documentation_file = False,
        deps = [":adapter", ":framework", ":platform"],
    )
    testfx_test(
        name = "custom_report_test",
        binary = ":custom_report",
        args = ["--filter", "FullyQualifiedName~PassedTestMethod", "--minimum-expected-tests", "1"],
        size = "small",
    )
    testfx_test(
        name = "custom_report_failure_probe",
        binary = ":custom_report",
        args = ["--filter", "FullyQualifiedName~FailedTestMethod", "--minimum-expected-tests", "1"],
        size = "small",
        tags = ["manual"],
    )

    testfx_library(
        name = "ensure_framework",
        project = "samples/public/mstest-runner/EnsureTestFramework/EnsureTestFramework",
        target_frameworks = ["net10.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [":platform"],
    )
    testfx_binary(
        name = "ensure_sample",
        project = "samples/public/mstest-runner/EnsureTestFramework/MyTestProject",
        linked_srcs = ["//eng/bazel:PortableTestFrameworkMetadata.cs"],
        target_frameworks = ["net10.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [":ensure_framework", ":platform"],
    )
    testfx_test(
        name = "ensure_failure_probe",
        binary = ":ensure_sample",
        args = ["--minimum-expected-tests", "1"],
        size = "small",
        tags = ["manual"],
    )

    testfx_binary(
        name = "otel_sample",
        project = "samples/public/MTPOTel",
        linked_srcs = ["//eng/bazel:PortableTestFrameworkMetadata.cs"],
        target_frameworks = ["net10.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [
            ":platform",
            ":hosting",
            ":opentelemetry",
            "@testsupport.main//microsoft.extensions.hosting",
            "@testsupport.main//opentelemetry.exporter.console",
            "@testsupport.main//opentelemetry.extensions.hosting",
        ],
    )
    testfx_test(
        name = "otel_sample_test",
        binary = ":otel_sample",
        args = ["--minimum-expected-tests", "1"],
        size = "small",
    )

    testfx_binary(
        name = "benchmarks",
        project = "test/Performance/MSTest.Performance.Benchmarks",
        linked_srcs = ["//eng/bazel:PortableTestFrameworkMetadata.cs"],
        compiler_options = ["/optimize+"],
        target_frameworks = ["net10.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [
            ":adapter",
            ":platform_services",
            ":vstest_bridge",
            ":framework",
            "@testsupport.benchmarks//benchmarkdotnet",
        ],
    )
    publish_binary(
        name = "benchmark_runner",
        binary = ":benchmarks",
        target_framework = "net10.0",
        self_contained = True,
    )

    testfx_binary(
        name = "browser_sample",
        project = "samples/BrowserPlayground",
        linked_srcs = ["//eng/bazel:PortableTestFrameworkMetadata.cs"],
        target_frameworks = ["net10.0"],
        project_sdk = "blazorwasm",
        static_web_assets = native.glob(["samples/BrowserPlayground/wwwroot/*"]),
        polyfills = False,
        generate_documentation_file = False,
        deps = [":platform", ":telemetry"],
    )
    publish_binary(
        name = "browser_publish",
        binary = ":browser_sample",
        target_framework = "net10.0",
        wasm = True,
        trim_mode = "copy",
    )

    testfx_analyzer(name = "sample_source_generator", assembly = ":mstest_generator")
    testfx_binary(
        name = "aot_sample",
        project = "samples/public/DemoMSTestSdk/ProjectWithNativeAOT",
        target_frameworks = ["net10.0"],
        polyfills = False,
        linked_srcs = ["//eng/bazel:TestMain.cs", "//eng/bazel:PortableTestFrameworkMetadata.cs"],
        analyzer_configs = ["//eng/bazel:reflection-free.globalconfig"],
        generate_documentation_file = False,
        deps = [":adapter", ":framework", ":platform_services", ":trx", ":sample_source_generator"],
    )
    # The pinned rules_dotnet links NativeAOT only for Linux and macOS; it fails analysis for Windows.
    native_aot_hosts = select({
        "@platforms//os:linux": [],
        "@platforms//os:macos": [],
        "//conditions:default": ["@platforms//:incompatible"],
    })
    publish_binary(
        name = "aot_publish",
        binary = ":aot_sample",
        target_framework = "net10.0",
        native_aot = True,
        target_compatible_with = native_aot_hosts,
    )
    testfx_published_test(
        name = "aot_publish_test",
        binary = ":aot_publish",
        args = ["--minimum-expected-tests", "1"],
        size = "small",
        target_compatible_with = native_aot_hosts,
    )

    native.filegroup(
        name = "extra_samples",
        srcs = [
            ":aot_sample",
            ":benchmarks",
            ":ctrf_playground",
            ":custom_report",
            ":ensure_sample",
            ":fx_extensibility_net462",
            ":nunit_playground",
            ":otel_sample",
            ":reliable_suite",
            ":sdk_runner_sample",
            ":scale_10c",
            ":scale_100c",
            ":scale_1000c",
            ":xunit_mtp_playground",
        ],
    )

def slnx_samples():
    """Build-only TestFx.slnx samples; their intentional failures are not run as tests."""

    # xunit.v3.core.mtp-v2's targets generate the entry point and compile these package sources.
    testfx_package_files(
        name = "xunit_mtp_playground_content",
        package = "@testsupport.samples//xunit.v3.core.mtp-v2:files",
        suffixes = ["_content/DefaultResultWriters.cs", "_content/DefaultRunnerReporters.cs"],
    )
    testfx_binary(
        name = "xunit_mtp_playground",
        project = "samples/CtrfPlayground/XunitMtp",
        assembly_name = "XunitMtp",
        linked_srcs = ["//eng/bazel:XunitMtpMain.cs", ":xunit_mtp_playground_content"],
        target_frameworks = ["net10.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [
            "@testsupport.samples//microsoft.testing.extensions.telemetry",
            "@testsupport.samples//microsoft.testing.platform",
            "@testsupport.samples//microsoft.testing.platform.msbuild",
            "@testsupport.samples//xunit.v3.mtp-v2",
        ],
    )

    # NUnit3TestAdapter references its MTP adapter from build/net8.0 when EnableNUnitRunner is set.
    # Its MTP package dependencies resolve to the repository projects so the app carries one
    # Microsoft.Testing.Platform assembly.
    testfx_package_files(
        name = "nunit3_adapter_files",
        package = "@testsupport.samples//nunit3testadapter:files",
        suffixes = ["build/net8.0/" + assembly for assembly in [
            "NUnit3.TestAdapter.dll",
            "nunit.engine.api.dll",
            "nunit.engine.core.dll",
            "nunit.engine.dll",
            "testcentric.engine.metadata.dll",
        ]],
    )
    testfx_package_files(
        name = "nunit3_adapter_ref",
        package = "@testsupport.samples//nunit3testadapter:files",
        suffixes = ["build/net8.0/NUnit3.TestAdapter.dll"],
    )
    import_library(
        name = "nunit3_adapter",
        library_name = "NUnit3TestAdapter",
        version = "6.3.1",
        refs = [":nunit3_adapter_ref"],
        libs = [":nunit3_adapter_files"],
        deps = [
            ":platform",
            ":platform_msbuild",
            ":vstest_bridge",
            "@testsupport.samples//microsoft.extensions.dependencymodel",
            "@testsupport.samples//microsoft.testplatform.adapterutilities",
        ],
    )

    # Program.cs adds the preview AffectedTests package, which the Bazel graph does not model.
    testfx_binary(
        name = "nunit_playground",
        project = "samples/NUnitPlayground",
        exclude = ["samples/NUnitPlayground/Program.cs"],
        linked_srcs = ["//eng/bazel:NUnitPlaygroundMain.cs"],
        target_frameworks = ["net10.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [
            ":platform",
            ":telemetry",
            ":nunit3_adapter",
            "@testsupport.samples//nunit",
        ],
    )

    # Like the unit-test apps, Bazel replaces Program.cs (which adds the preview AffectedTests
    # extension) with the shared entry point.
    testfx_binary(
        name = "ctrf_playground",
        project = "samples/CtrfPlayground/Mtp",
        exclude = ["samples/CtrfPlayground/Mtp/Program.cs"],
        linked_srcs = ["//eng/bazel:TestMain.cs", "//eng/bazel:PortableTestFrameworkMetadata.cs"],
        target_frameworks = ["net10.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [":adapter", ":ctrf", ":framework", ":platform", ":trx"],
    )

    testfx_library(
        name = "fx_extensibility",
        project = "samples/FxExtensibility",
        assembly_name = "MSTest.Extensibility.Samples",
        linked_srcs = ["//eng/bazel:CompatibilityFrameworkMetadata.cs"],
        target_frameworks = ["net462"],
        polyfills = False,
        generate_documentation_file = False,
        tags = ["manual"],
        deps = [":framework"],
    )
    testfx_framework_variant(
        name = "fx_extensibility_net462",
        assembly = ":fx_extensibility",
        target_framework = "net462",
    )
