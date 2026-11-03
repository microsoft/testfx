"""The dependency-free platform and native report build graph."""

load(":defs.bzl", "STANDARD_FRAMEWORKS", "testfx_compatibility_select", "testfx_library", "testfx_nuget")

_PLATFORM = "src/Platform/Microsoft.Testing.Platform"
_SHARED = "src/Platform/SharedExtensionHelpers"

def modern_json_sources():
    """The System.Text.Json files removed by both non-.NETCoreApp project variants."""
    return [
        _PLATFORM + "/ServerMode/JsonRpc/Json/" + name + ".cs"
        for name in [
            "Json",
            "Json.Deserializers",
            "Json.Serializers",
            "Json.TestNodeSerializer",
            "JsonCollectionDeserializer",
            "JsonDeserializer",
            "JsonElementDeserializer",
            "JsonExtensions",
            "JsonObjectSerializer",
            "JsonObjectSerializer1",
            "JsonPropertyBagDeserializer",
            "JsonSerializer",
            "JsonValueSerializer",
            "JsonValueSerializer1",
        ]
    ]

def ipc_sources():
    return native.glob([_PLATFORM + "/IPC/*.cs"]) + [
        _PLATFORM + "/IPC/Serializers/BaseSerializer.cs",
        _PLATFORM + "/IPC/Serializers/NamedPipeSerializer.cs",
        _PLATFORM + "/IPC/Serializers/VoidResponseSerializer.cs",
        _PLATFORM + "/IPC/Models/VoidResponse.cs",
        _PLATFORM + "/ServerMode/DotnetTest/IPC/ObjectFieldIds.cs",
        _PLATFORM + "/Resources/PlatformResources.cs",
    ]

def runtime_helpers():
    return [
        _PLATFORM + "/Helpers/" + name + ".cs"
        for name in [
            "RoslynString",
            "ApplicationStateGuard",
            "EnvironmentVariableConstants",
            "ExitCodes",
            "TimeoutHelper",
            "TimeSpanParser",
        ]
    ]

def platform_libraries():
    modern_sources = modern_json_sources() + [_PLATFORM + "/Configurations/JsonConfigurationFileParser.cs"]
    testfx_library(
        name = "platform",
        project = _PLATFORM,
        target_frameworks = STANDARD_FRAMEWORKS,
        allow_unsafe_blocks = True,
        defines = ["IS_CORE_MTP"],
        usings = ["Polyfills"],
        exclude = modern_sources,
        linked_srcs = [_SHARED + "/StackTraceRegexHelper.cs"] + testfx_compatibility_select([], modern_sources),
        resources = {
            _PLATFORM + "/Resources/PlatformResources.resx": "Microsoft.Testing.Platform.Resources.PlatformResources",
            _PLATFORM + "/OutputDevice/Terminal/TerminalResources.resx": "Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalResources",
        },
        build_info = _PLATFORM + "/BuildInfo.cs.template",
        internals_visible_to = [
            "DynamicProxyGenAssembly2",
            "Microsoft.Testing.Extensions.OpenTelemetry",
            "Microsoft.Testing.Extensions.PackagedApp",
            "Microsoft.Testing.Extensions.Hosting",
            "Microsoft.Testing.Extensions.AzureDevOpsReport",
            "Microsoft.Testing.Extensions.GitHubActionsReport",
            "Microsoft.Testing.Extensions.CrashDump",
            "Microsoft.Testing.Extensions.CtrfReport",
            "Microsoft.Testing.Extensions.HangDump",
            "Microsoft.Testing.Extensions.HotReload",
            "Microsoft.Testing.Extensions.HtmlReport",
            "Microsoft.Testing.Extensions.JUnitReport",
            "Microsoft.Testing.Extensions.MSBuild",
            "Microsoft.Testing.Extensions.Retry",
            "Microsoft.Testing.Extensions.Telemetry",
            "Microsoft.Testing.Extensions.TrxReport",
            "Microsoft.Testing.Extensions.UnitTests",
            "Microsoft.Testing.Extensions.VSTestBridge",
            "Microsoft.Testing.Extensions.VSTestBridge.UnitTests",
            "Microsoft.Testing.Extensions.VideoRecorder",
            "Microsoft.Testing.Framework",
            "Microsoft.Testing.Platform.Acceptance.IntegrationTests",
            "Microsoft.Testing.Platform.AI",
            "Microsoft.Testing.Platform.UnitTests",
            "Microsoft.Testing.TestInfrastructure",
            "MSTest.Engine",
            "mstest-appmodel-controller",
            "MSTest.TestAdapter",
            "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests",
            "TestFramework.ForTestingMSTest",
        ],
    )
    testfx_library(
        name = "trx_abstractions",
        project = "src/Platform/Microsoft.Testing.Extensions.TrxReport.Abstractions",
        deps = [":platform"],
    )
    testfx_library(
        name = "trx",
        project = "src/Platform/Microsoft.Testing.Extensions.TrxReport",
        allow_unsafe_blocks = True,
        defines = ["USE_TRX_NAMESPACE"],
        linked_srcs = native.glob([_PLATFORM + "/IPC/*.cs"]) + [
            _PLATFORM + "/IPC/Serializers/BaseSerializer.cs",
            _PLATFORM + "/IPC/Serializers/NamedPipeSerializer.cs",
            _PLATFORM + "/IPC/Serializers/VoidResponseSerializer.cs",
            _PLATFORM + "/IPC/Models/VoidResponse.cs",
            _PLATFORM + "/ServerMode/DotnetTest/IPC/ObjectFieldIds.cs",
            _PLATFORM + "/Resources/PlatformResources.cs",
            _PLATFORM + "/Helpers/RoslynString.cs",
            _PLATFORM + "/Helpers/RuntimeFeatureHelper.cs",
            _PLATFORM + "/Helpers/ApplicationStateGuard.cs",
            _PLATFORM + "/Helpers/DisposeHelper.cs",
            _PLATFORM + "/Helpers/EnvironmentVariableConstants.cs",
            _PLATFORM + "/Helpers/ExitCodes.cs",
            _PLATFORM + "/Helpers/TimeoutHelper.cs",
            _PLATFORM + "/Helpers/TimeSpanParser.cs",
            _PLATFORM + "/Services/ArtifactNamingHelper.cs",
            _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
            _SHARED + "/ReportFileNameHelper.cs",
            _SHARED + "/ReportFileNameSanitizer.cs",
            _SHARED + "/ReportGeneratorCommandLineBase.cs",
            _SHARED + "/ReportEngineBase.cs",
            _SHARED + "/ReportFileNameValidator.cs",
            _SHARED + "/MergeOutputFileHelper.cs",
            _SHARED + "/ArtifactPostProcessingHelper.cs",
            _SHARED + "/ReportFileWriterHelper.cs",
            _SHARED + "/TargetFrameworkMonikerHelper.cs",
            _SHARED + "/NamedPipeServerFactory.cs",
            _SHARED + "/PipeNameEnvironmentVariableProviderBase.cs",
            "src/Platform/Microsoft.Testing.Extensions.CrashDump/CrashDumpCommandLineOptions.cs",
        ],
        resources = {
            "src/Platform/Microsoft.Testing.Extensions.TrxReport/Resources/ExtensionResources.resx": "Microsoft.Testing.Extensions.TrxReport.Resources.ExtensionResources",
        },
        build_info = _SHARED + "/BuildInfo.cs.template",
        internals_visible_to = [
            "Microsoft.Testing.Extensions.UnitTests",
            "Microsoft.Testing.Extensions.VSTestBridge",
        ],
        deps = [":platform", ":trx_abstractions"],
    )
    testfx_library(
        name = "msbuild_extension",
        project = "src/Platform/Microsoft.Testing.Extensions.MSBuild",
        target_frameworks = STANDARD_FRAMEWORKS,
        linked_srcs = ipc_sources() + runtime_helpers(),
        resources = {
            "src/Platform/Microsoft.Testing.Extensions.MSBuild/Resources/ExtensionResources.resx": "Microsoft.Testing.Extensions.MSBuild.Resources.ExtensionResources",
        },
        build_info = _SHARED + "/BuildInfo.cs.template",
        internals_visible_to = ["Microsoft.Testing.Platform.MSBuild.UnitTests"],
        deps = [":platform"],
    )
    testfx_library(
        name = "platform_msbuild",
        project = "src/Platform/Microsoft.Testing.Platform.MSBuild",
        target_frameworks = STANDARD_FRAMEWORKS,
        defines = ["MTP_MSBUILD_TASKS"],
        usings = ["Polyfills"],
        linked_srcs = ipc_sources() + runtime_helpers() + native.glob([_PLATFORM + "/ServerMode/JsonRpc/Json/Jsonite/*.cs"]) + [
            _PLATFORM + "/Helpers/StringBuilderExtensions.cs",
            _SHARED + "/StackTraceRegexHelper.cs",
            _PLATFORM + "/Helpers/System/IEnvironment.cs",
            _PLATFORM + "/Helpers/System/SystemEnvironment.cs",
            _PLATFORM + "/Helpers/System/ITask.cs",
            _PLATFORM + "/Helpers/System/SystemTask.cs",
            _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
            "src/Platform/Microsoft.Testing.Extensions.MSBuild/MSBuildConstants.cs",
            "src/Platform/Microsoft.Testing.Extensions.MSBuild/Serializers/FailedTestInfoRequestSerializer.cs",
            "src/Platform/Microsoft.Testing.Extensions.MSBuild/Serializers/ModuleInfoRequestSerializer.cs",
            "src/Platform/Microsoft.Testing.Extensions.MSBuild/Serializers/RunSummaryRequestSerializer.cs",
        ],
        resources = {
            "src/Platform/Microsoft.Testing.Platform.MSBuild/Resources/MSBuildResources.resx": "Microsoft.Testing.Platform.MSBuild.Resources.MSBuildResources",
        },
        internals_visible_to = ["Microsoft.Testing.Platform.MSBuild.UnitTests"],
        deps = [
            ":platform",
            ":msbuild_extension",
        ] + testfx_nuget([
            "microsoft.build.framework",
            "microsoft.build.utilities.core",
        ]) + testfx_compatibility_select(["@paket.compatibility//microsoft.win32.registry"]),
    )
    testfx_library(
        name = "retry",
        project = "src/Platform/Microsoft.Testing.Extensions.Retry",
        linked_srcs = ipc_sources() + runtime_helpers() + [
            _PLATFORM + "/Helpers/ArgumentGuard.cs",
            _PLATFORM + "/Helpers/PasteArguments.cs",
            _SHARED + "/RetryArtifactManifest.cs",
        ],
        resources = {
            "src/Platform/Microsoft.Testing.Extensions.Retry/Resources/ExtensionResources.resx": "Microsoft.Testing.Extensions.Policy.Resources.ExtensionResources",
        },
        build_info = _SHARED + "/BuildInfo.cs.template",
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests"],
        deps = [":platform"],
    )
    testfx_library(
        name = "telemetry",
        project = "src/Platform/Microsoft.Testing.Extensions.Telemetry",
        linked_srcs = [
            _PLATFORM + "/Helpers/CIEnvironmentDetector.cs",
            _PLATFORM + "/Helpers/RoslynString.cs",
            _PLATFORM + "/Helpers/RoslynDebug.cs",
            _PLATFORM + "/Helpers/RuntimeFeatureHelper.cs",
            _PLATFORM + "/Messages/SingleConsumerUnboundedChannel.cs",
        ],
        resources = {
            "src/Platform/Microsoft.Testing.Extensions.Telemetry/Resources/ExtensionResources.resx": "Microsoft.Testing.Extensions.Telemetry.Resources.ExtensionResources",
        },
        internals_visible_to = [
            "Microsoft.Testing.Platform.UnitTests",
            "Microsoft.Testing.Extensions.UnitTests",
            "DynamicProxyGenAssembly2",
        ],
        deps = [
            ":platform",
            "@paket.source//microsoft.applicationinsights",
            "@paket.source//system.diagnostics.diagnosticsource",
        ],
    )
