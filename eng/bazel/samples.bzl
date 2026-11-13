"""Existing portable samples, with native source dependencies and unchanged test bodies."""

load("@rules_dotnet//dotnet:defs.bzl", "publish_binary")
load(":defs.bzl", "testfx_analyzer", "testfx_binary", "testfx_library", "testfx_test")

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
            target_frameworks = ["net8.0"],
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
        target_frameworks = ["net8.0"],
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
        target_frameworks = ["net8.0"],
        polyfills = False,
        generate_documentation_file = False,
        deps = [":platform"],
    )
    testfx_binary(
        name = "ensure_sample",
        project = "samples/public/mstest-runner/EnsureTestFramework/MyTestProject",
        linked_srcs = ["//eng/bazel:PortableTestFrameworkMetadata.cs"],
        target_frameworks = ["net8.0"],
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
        target_frameworks = ["net9.0"],
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
        target_frameworks = ["net8.0"],
        polyfills = False,
        linked_srcs = ["//eng/bazel:TestMain.cs", "//eng/bazel:PortableTestFrameworkMetadata.cs"],
        analyzer_configs = ["//eng/bazel:reflection-free.globalconfig"],
        generate_documentation_file = False,
        deps = [":adapter", ":framework", ":platform_services", ":trx", ":sample_source_generator"],
    )
    publish_binary(
        name = "aot_publish",
        binary = ":aot_sample",
        target_framework = "net8.0",
        native_aot = True,
        tags = ["manual"],
    )

    native.filegroup(
        name = "extra_samples",
        srcs = [
            ":aot_sample",
            ":benchmarks",
            ":custom_report",
            ":ensure_sample",
            ":otel_sample",
            ":reliable_suite",
            ":sdk_runner_sample",
            ":scale_10c",
            ":scale_100c",
            ":scale_1000c",
        ],
    )
