"""Native targets for existing tests; test bodies and scheduling are unchanged."""

load(":defs.bzl", "testfx_analyzer", "testfx_binary", "testfx_library", "testfx_test")
load(":platform.bzl", "ipc_sources")

_PLATFORM = "src/Platform/Microsoft.Testing.Platform"

def unit_tests():
    testfx_analyzer(name = "mstest_analyzer_plugin", assembly = ":analyzers")
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
            deps = [":platform", ":adapter", ":framework", ":framework_extensions", ":trx", ":mstest_analyzer_plugin"] + dependencies,
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
        deps = [":adapter", ":framework", ":framework_extensions", ":trx", ":mstest_analyzer_plugin", "@rules_dotnet//tools/runfiles"],
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
            ] if name == "adapter_tests" else []),
            defines = ["TESTFX_INTERNAL_TESTS"],
            usings = ["ExecutionScope=Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope"] if name == "adapter_tests" else [],
            appsetting_files = ["test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/DataTestSourceFile.xml"] if name == "platform_services_tests" else [],
            generate_documentation_file = False,
            deps = [
                ":framework",
                ":framework_extensions",
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
        testfx_test(
            name = name,
            binary = ":" + name + "_app",
            args = ["--minimum-expected-tests", "1"],
            size = "medium",
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
        linked_srcs = ["//eng/bazel:TestMain.cs"] + ipc_sources() + [
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
