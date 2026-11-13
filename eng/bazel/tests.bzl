"""Native targets for existing tests; test bodies and scheduling are unchanged."""

load("@rules_dotnet//dotnet:defs.bzl", "publish_binary")
load(":defs.bzl", "PORTABLE_FRAMEWORKS", "testfx_analyzer", "testfx_binary", "testfx_library", "testfx_test")
load(":test-assets.bzl", "testfx_published_test")
load(":platform.bzl", "ipc_sources")

_PLATFORM = "src/Platform/Microsoft.Testing.Platform"

def native_unit_test_deps():
    return [
        ":platform",
        ":adapter",
        ":framework",
        ":trx",
        ":junit",
        ":ctrf",
        ":azdo_report",
        ":crashdump",
        ":hangdump",
        ":opentelemetry",
        ":mstest_analyzer_plugin",
    ]

def _terminal_contract_sources(standalone, include_reporter = True):
    reporter = native.glob(
        [_PLATFORM + "/OutputDevice/Terminal/*.cs"],
        exclude = [
            _PLATFORM + "/OutputDevice/Terminal/TerminalTestReporterCommandLineOptionsProvider.cs",
            _PLATFORM + "/OutputDevice/Terminal/TerminalResources.cs",
        ],
    ) if include_reporter else []
    return reporter + [
        _PLATFORM + "/" + path + ".cs"
        for path in [
            "Helpers/ExitCodes",
            "Helpers/RoslynString",
            "Helpers/ApplicationStateGuard",
            "Helpers/StackTraceHelper",
            "Helpers/System/IConsole",
            "Helpers/System/IStopwatch",
            "Helpers/System/SystemConsole",
            "Helpers/System/SystemStopwatch",
            "Logging/NopLogger",
            "OutputDevice/SlowTestThresholdState",
            "OutputDevice/TargetFrameworkParser",
            "OutputDevice/TestRunSummaryHelper",
            "CommandLine/ZeroTestsPolicy",
        ] + ([
            "Logging/ILogger",
            "Logging/LogLevel",
            "Logging/LoggingExtensions",
            "OutputDevice/IColor",
            "OutputDevice/SystemConsoleColor",
            "Helpers/RoslynHashCode",
            "Services/CoverageScopeSummary",
            "Services/CoverageMetricResult",
            "TestHost/SessionUid",
            "Messages/CoverageAggregation",
            "Messages/CoverageCounts",
            "Messages/CoverageMetric",
            "Messages/CoverageMetricHelper",
            "Messages/CoverageScope",
            "Messages/CoverageScopeLevel",
            "Messages/TestCoverageThresholdMessage",
            "Messages/DataWithSessionUid",
            "Messages/PropertyBagData",
            "Messages/PropertyBag",
            "Messages/PropertyBag.Property",
            "Messages/PropertyBag.PropertyBagEnumerable",
            "Messages/TestNodeStateProperties",
            "Messages/IData",
            "Messages/IProperty",
            "Messages/RetryAttemptProperty",
        ] if standalone else [])
    ]

def unit_tests():
    testfx_analyzer(name = "mstest_analyzer_plugin", assembly = ":analyzers")
    terminal_resources = {
        _PLATFORM + "/OutputDevice/Terminal/TerminalResources.resx": "Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalResources",
    }
    testfx_library(
        name = "terminal_contract_standalone",
        project = _PLATFORM + "/OutputDevice/Terminal",
        exclude = [
            _PLATFORM + "/OutputDevice/Terminal/TerminalTestReporterCommandLineOptionsProvider.cs",
            _PLATFORM + "/OutputDevice/Terminal/TerminalResources.cs",
        ],
        linked_srcs = _terminal_contract_sources(standalone = True, include_reporter = False),
        assembly_name = "TerminalReporterContract.Standalone",
        target_frameworks = ["net8.0"],
        resources = terminal_resources,
        usings = ["Polyfills", "Microsoft.Testing.Platform.Helpers"],
        generate_documentation_file = False,
    )
    for name, project, defines, dependencies, linked_sources, resources in [
        (
            "terminal_contract_tests",
            "Microsoft.Testing.Platform.TerminalReporterContract.UnitTests",
            [],
            [],
            _terminal_contract_sources(standalone = False),
            terminal_resources,
        ),
        (
            "self_real_tests",
            "MSTest.SelfRealExamples.UnitTests",
            ["TESTFX_CLASS_PARALLEL", "TESTFX_TELEMETRY"],
            [":telemetry"],
            [],
            {},
        ),
    ]:
        directory = "test/UnitTests/" + project
        testfx_binary(
            name = name + "_app",
            project = directory,
            exclude = [directory + "/Program.cs"],
            linked_srcs = [
                "//eng/bazel:NativeUnitTestMain.cs",
                "//eng/bazel:PortableTestFrameworkMetadata.cs",
            ] + linked_sources,
            resources = resources,
            compile_data = [":terminal_contract_standalone"] if name == "terminal_contract_tests" else [],
            defines = defines,
            usings = ["Polyfills"],
            generate_documentation_file = False,
            appsetting_files = native.glob([
                directory + "/*.testconfig.json",
                directory + "/*.launcher.config.json",
                directory + "/testsbaseline*.txt",
            ], allow_empty = True),
            deps = native_unit_test_deps() + dependencies,
        )
        testfx_test(
            name = name,
            binary = ":" + name + "_app",
            args = ["--minimum-expected-tests", "1"],
            size = "medium",
        )

    for name, project, dependencies, linked_sources, four_workers in [
        (
            "vstest_bridge_tests",
            "Microsoft.Testing.Extensions.VSTestBridge.UnitTests",
            [":vstest_bridge", "@paket.source//moq"],
            [],
            False,
        ),
        (
            "protocol_contract_tests",
            "Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests",
            [],
            [
                _PLATFORM + "/ServerMode/DotnetTest/IPC/ObjectFieldIds.cs",
                _PLATFORM + "/ServerMode/DotnetTest/IPC/Constants.cs",
                _PLATFORM + "/IPC/INamedPipeSerializer.cs",
                _PLATFORM + "/IPC/IRequest.cs",
                _PLATFORM + "/IPC/IResponse.cs",
                _PLATFORM + "/IPC/NamedPipeBase.cs",
            ] + native.glob([
                _PLATFORM + "/IPC/Serializers/*.cs",
                _PLATFORM + "/IPC/Models/*.cs",
                _PLATFORM + "/ServerMode/DotnetTest/IPC/Models/*.cs",
                _PLATFORM + "/ServerMode/DotnetTest/IPC/Serializers/*.cs",
            ]),
            False,
        ),
        (
            "source_generation_tests",
            "MSTest.SourceGeneration.UnitTests",
            [":mstest_generator", "@paket.generator//microsoft.codeanalysis.csharp", "@paket.source//awesomeassertions"],
            [],
            True,
        ),
    ]:
        directory = "test/UnitTests/" + project
        testfx_binary(
            name = name + "_app",
            project = directory,
            exclude = [directory + "/Program.cs"],
            linked_srcs = ["//eng/bazel:TestMain.cs"] + linked_sources,
            defines = ["TESTFX_FOUR_WORKERS" if four_workers else "TESTFX_METHOD_PARALLEL"],
            usings = ["Microsoft.Testing.Platform.Extensions"] if name == "vstest_bridge_tests" else [],
            generate_documentation_file = False,
            deps = [":platform", ":adapter", ":framework", ":trx", ":mstest_analyzer_plugin"] + dependencies,
        )
        testfx_test(
            name = name,
            binary = ":" + name + "_app",
            args = ["--minimum-expected-tests", "1"],
            size = "medium",
        )

    testfx_binary(
        name = "build_tools_tests_app",
        project = "test/UnitTests/Bazel.BuildTools.UnitTests",
        polyfills = False,
        linked_srcs = ["//eng/bazel:TestMain.cs"],
        defines = ["TESTFX_METHOD_PARALLEL"],
        generate_documentation_file = False,
        data = ["//eng/bazel:build_tools"],
        envs = {"TESTFX_BAZEL_BUILD_TOOLS": "$(rlocationpath //eng/bazel:build_tools)"},
        deps = [":adapter", ":framework", ":trx", ":mstest_analyzer_plugin", "//eng/bazel:runfiles"],
    )
    testfx_test(
        name = "build_tools_tests",
        binary = ":build_tools_tests_app",
        args = ["--minimum-expected-tests", "13"],
        size = "medium",
    )
    for name, project, assembly_name in [
        ("adapter_tests", "MSTestAdapter.UnitTests", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests"),
        ("platform_services_tests", "MSTestAdapter.PlatformServices.UnitTests", "MSTestAdapter.PlatformServices.UnitTests"),
    ]:
        directory = "test/UnitTests/" + project
        testfx_binary(
            name = name + "_app",
            project = directory,
            assembly_name = assembly_name,
            exclude = [directory + "/Program.cs"],
            linked_srcs = ["//eng/bazel:TestMain.cs"] + ([
                "test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/TestableImplementations/MockableReflectionOperations.cs",
                "test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/TestableImplementations/TestablePlatformServiceProvider.cs",
            ] if name == "adapter_tests" else ["test/Utilities/DiagnosticAssertionObserver.cs"]),
            defines = ["TESTFX_INTERNAL_TESTS"] + (["TESTFX_PLATFORM_SERVICES_PROBES"] if name == "platform_services_tests" else []),
            usings = ["Microsoft.Testing.Platform.Extensions", "ExecutionScope=Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope"] if name == "adapter_tests" else [],
            appsetting_files = ["test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/DataTestSourceFile.xml"] if name == "platform_services_tests" else [],
            generate_documentation_file = False,
            deps = [
                ":framework",
                ":adapter",
                ":platform_services",
                ":platform",
                ":mstest_hosting",
                ":internal_test_framework",
                ":trx",
                "@paket.source//awesomeassertions",
                "@paket.source//moq",
            ],
        )
        applications = {}
        for framework in PORTABLE_FRAMEWORKS:
            suffix = framework.replace(".", "_")
            configuration = name + "_" + suffix + "_configuration"
            application = name + "_" + suffix
            native.config_setting(
                name = configuration,
                flag_values = {"@rules_dotnet//dotnet:target_framework": framework},
            )
            publish_binary(
                name = application,
                binary = ":" + name + "_app",
                target_framework = framework,
                self_contained = False,
                roll_forward_behavior = "LatestMajor",
            )
            applications[":" + configuration] = ":" + application
        native.alias(name = name + "_layout", actual = select(applications))
        testfx_published_test(
            name = name,
            binary = ":" + name + "_layout",
            writable_layout = name == "platform_services_tests",
            args = ["--minimum-expected-tests", "1"],
            size = "medium",
            target_compatible_with = select({
                "@platforms//os:windows": [],
                "//conditions:default": ["@platforms//:incompatible"],
            }) if name == "platform_services_tests" else [],
        )

    testfx_library(
        name = "test_infrastructure",
        project = "test/Utilities/Microsoft.Testing.TestInfrastructure",
        version_property = "TestingPlatformVersionPrefix",
        build_info = "src/Platform/SharedExtensionHelpers/BuildInfo.cs.template",
        usings = ["Polyfills"],
        deps = [":platform", ":vstest_bridge", "@paket.source//microsoft.extensions.resilience"],
    )
    testfx_binary(
        name = "platform_tests_app",
        project = "test/UnitTests/Microsoft.Testing.Platform.UnitTests",
        exclude = ["test/UnitTests/Microsoft.Testing.Platform.UnitTests/Program.cs"],
        linked_srcs = [
            "//eng/bazel:TestMain.cs",
            "test/Utilities/DiagnosticAssertionObserver.cs",
            "test/Utilities/TestAssertExtensions.cs",
            "test/Utilities/ValidationResultExtensions.cs",
        ] + ipc_sources() + [
            _PLATFORM + "/ServerMode/DotnetTest/Transport/DotnetTestHttpClient.cs",
            _PLATFORM + "/IPC/Models/TestHostCompletedRequest.cs",
            _PLATFORM + "/IPC/Serializers/TestHostCompletedRequestSerializer.cs",
            _PLATFORM + "/IPC/Models/TestHostProcessPIDRequest.cs",
            _PLATFORM + "/IPC/Serializers/TestHostProcessPIDRequestSerializer.cs",
            _PLATFORM + "/ServerMode/DotnetTest/IPC/Constants.cs",
            _PLATFORM + "/Helpers/RoslynString.cs",
            _PLATFORM + "/Helpers/ApplicationStateGuard.cs",
            _PLATFORM + "/Helpers/DeadlineHelper.cs",
            _PLATFORM + "/Helpers/DisposeHelper.cs",
            _PLATFORM + "/Helpers/EnvironmentVariableConstants.cs",
            _PLATFORM + "/Helpers/ExitCodes.cs",
            _PLATFORM + "/Helpers/PasteArguments.cs",
            _PLATFORM + "/Helpers/TimeoutHelper.cs",
            _PLATFORM + "/Helpers/TimeSpanParser.cs",
            _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
            _PLATFORM + "/ServerMode/JsonRpc/RpcIdParser.cs",
            _PLATFORM + "/Services/ArtifactNamingHelper.cs",
            "test/Utilities/Microsoft.Testing.TestInfrastructure/SlowestTestsConsumer.cs",
        ] + native.glob([_PLATFORM + "/OutputDevice/Terminal/*.cs"]),
        build_info = _PLATFORM + "/BuildInfo.cs.template",
        defines = ["IS_MTP_UNIT_TESTS", "TESTFX_METHOD_PARALLEL"],
        usings = ["Polyfills", "Microsoft.Testing.Platform.Extensions", "Microsoft.Testing.TestInfrastructure"],
        internals_visible_to = ["DynamicProxyGenAssembly2"],
        generate_documentation_file = False,
        deps = [
            ":platform",
            ":ai",
            ":adapter",
            ":framework",
            ":mstest_analyzer_plugin",
            ":trx",
            "@paket.source//moq",
        ],
    )
    testfx_test(
        name = "platform_tests",
        binary = ":platform_tests_app",
        args = ["--minimum-expected-tests", "1"],
        size = "large",
    )
