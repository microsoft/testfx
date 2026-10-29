"""The remaining portable MTP extensions, using their existing source and assets."""

load(":defs.bzl", "testfx_library")
load(":platform.bzl", "ipc_sources", "runtime_helpers")

_PLATFORM = "src/Platform/Microsoft.Testing.Platform"
_SHARED = "src/Platform/SharedExtensionHelpers"

def _extension(name, product, root_namespace = None, build_info = True, **kwargs):
    project = "src/Platform/" + product
    resources = {
        path: (root_namespace or product) + ".Resources." + path.rsplit("/", 1)[-1][:-5]
        for path in native.glob([project + "/Resources/*.resx"], allow_empty = True)
    }
    testfx_library(
        name = name,
        project = project,
        resources = resources,
        build_info = _SHARED + "/BuildInfo.cs.template" if build_info else None,
        **kwargs
    )

def _report_sources():
    return [
        _PLATFORM + "/Helpers/RoslynString.cs",
        _PLATFORM + "/Helpers/ApplicationStateGuard.cs",
        _PLATFORM + "/Helpers/ExitCodes.cs",
        _PLATFORM + "/Services/ArtifactNamingHelper.cs",
        _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
        _PLATFORM + "/Resources/PlatformResources.cs",
    ] + [
        _SHARED + "/" + name + ".cs"
        for name in [
            "ReportFileNameHelper",
            "ReportFileNameSanitizer",
            "CapturedTestResultBase",
            "TestResultCaptureHelper",
            "TargetFrameworkMonikerHelper",
            "ReportFileNameValidator",
            "MergeOutputFileHelper",
            "ArtifactPostProcessingHelper",
            "ReportEngineBase",
            "ReportGeneratorBase",
            "ReportGeneratorBase.Journal",
            "ReportProviderRegistration",
            "BoundedUtf8LineReader",
            "ReportJournalConfiguration",
            "ReportJournalRecord",
            "ReportProcessLifetimeHandler",
            "RetryArtifactManifest",
            "ReportProcessLifetimeHandler.Journal",
            "ReportGeneratorCommandLineBase",
        ]
    ]

def _ci_report_sources():
    return [
        _PLATFORM + "/Helpers/ExitCodes.cs",
        _PLATFORM + "/Helpers/EnvironmentVariableConstants.cs",
        _PLATFORM + "/Helpers/RoslynString.cs",
        _PLATFORM + "/Helpers/RuntimeFeatureHelper.cs",
        _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
        "test/Utilities/Microsoft.Testing.TestInfrastructure/RootFinder.cs",
    ] + [
        _SHARED + "/" + name + ".cs"
        for name in [
            "TargetFrameworkMonikerHelper",
            "SlowTestReporterBase",
            "ReporterLoggingExtensions",
            "TestNodeIdentity",
            "SummaryReporterHelpers",
            "CiRunSummaryExitCode",
            "CiCoverageSummary",
            "CiCoverageSummaryControllerHandler",
            "CiCoverageSummaryControllerHandlerRegistration",
            "CiRunSummaryAggregateReader",
            "CiRunSummaryAggregation",
            "CiRunSummaryFragmentIo",
            "CiRunSummaryModels",
            "StackTraceSourceLocationResolver",
        ]
    ]

def extension_libraries():
    for name, product, dependencies in [
        ("configuration", "Configuration", ["@paket.source//microsoft.extensions.configuration.abstractions"]),
        ("logging", "Logging", ["@paket.source//microsoft.extensions.logging"]),
        ("hosting", "Hosting", [":configuration", ":logging", "@paket.source//microsoft.extensions.hosting.abstractions"]),
        ("opentelemetry", "OpenTelemetry", ["@paket.source//opentelemetry", "@paket.source//opentelemetry.exporter.opentelemetryprotocol"]),
    ]:
        _extension(
            name = name,
            product = "Microsoft.Testing.Extensions." + product,
            deps = [":platform"] + dependencies,
            internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests"] + (["DynamicProxyGenAssembly2"] if name in ["logging", "opentelemetry"] else []),
        )

    _extension(
        name = "crashdump",
        product = "Microsoft.Testing.Extensions.CrashDump",
        root_namespace = "Microsoft.Testing.Extensions.Diagnostics",
        linked_srcs = [
            _PLATFORM + "/Helpers/ApplicationStateGuard.cs",
            _PLATFORM + "/Helpers/RoslynString.cs",
            _PLATFORM + "/Resources/PlatformResources.cs",
            _PLATFORM + "/Services/ArtifactNamingHelper.cs",
            _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
        ],
        internals_visible_to = ["Microsoft.Testing.Extensions.VSTestBridge", "Microsoft.Testing.Extensions.UnitTests"],
        deps = [":platform", ":trx_abstractions"],
    )
    _extension(
        name = "hangdump",
        product = "Microsoft.Testing.Extensions.HangDump",
        root_namespace = "Microsoft.Testing.Extensions.Diagnostics",
        linked_srcs = ipc_sources() + runtime_helpers() + [
            _SHARED + "/NamedPipeServerFactory.cs",
            _SHARED + "/PipeNameEnvironmentVariableProviderBase.cs",
            _PLATFORM + "/Helpers/DeadlineHelper.cs",
            _PLATFORM + "/Services/ArtifactNamingHelper.cs",
            _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
        ],
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests"],
        deps = [":platform", "@paket.source//microsoft.diagnostics.netcore.client"],
    )
    _extension(
        name = "hotreload",
        product = "Microsoft.Testing.Extensions.HotReload",
        root_namespace = "Microsoft.Testing.Extensions.Hosting",
        build_info = False,
        linked_srcs = [_PLATFORM + "/Helpers/EnvironmentVariableConstants.cs"],
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests"],
        deps = [":platform"],
    )
    _extension(
        name = "video",
        product = "Microsoft.Testing.Extensions.VideoRecorder",
        version_override = "1.0.0",
        linked_srcs = [
            _PLATFORM + "/Helpers/ApplicationStateGuard.cs",
            _PLATFORM + "/Helpers/RoslynString.cs",
            _PLATFORM + "/Resources/PlatformResources.cs",
            _PLATFORM + "/Services/ArtifactNamingHelper.cs",
            _PLATFORM + "/OutputDevice/TargetFrameworkParser.cs",
        ],
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests", "DynamicProxyGenAssembly2"],
        deps = [":platform"],
    )
    _extension(
        name = "vstest_bridge",
        product = "Microsoft.Testing.Extensions.VSTestBridge",
        linked_srcs = [
            _PLATFORM + "/Helpers/RoslynString.cs",
            _PLATFORM + "/Helpers/RoslynDebug.cs",
            "@paket.source//microsoft.testplatform.filter.source:content_files",
        ] + [
            _SHARED + "/" + name + ".cs"
            for name in [
                "RunSettingsProviderHelper",
                "RunSettingsCommandLineOptionsProviderBase",
                "RunSettingsConfigurationProviderBase",
                "RunSettingsEnvironmentVariableProviderBase",
                "TestRunParametersCommandLineOptionsProviderBase",
                "TestCaseFilterCommandLineOptionsProviderBase",
            ]
        ],
        internals_visible_to = [
            "MSTest.TestAdapter",
            "Microsoft.Testing.Extensions.VSTestBridge.UnitTests",
            "MSTest.Performance.Benchmarks",
        ],
        deps = [":platform", ":trx_abstractions", ":telemetry", "@paket.source//microsoft.testplatform.objectmodel"],
    )
    for name, product in [("html", "HtmlReport"), ("junit", "JUnitReport"), ("ctrf", "CtrfReport")]:
        _extension(
            name = name,
            product = "Microsoft.Testing.Extensions." + product,
            version_override = "1.0.0" if name == "junit" else "",
            linked_srcs = _report_sources() + ([_PLATFORM + "/Helpers/EnvironmentVariableConstants.cs"] if name == "ctrf" else []),
            embedded_resources = {
                "src/Platform/Microsoft.Testing.Extensions.HtmlReport/Templates/report-template.html": "Microsoft.Testing.Extensions.HtmlReport.Templates.report-template.html",
            } if name == "html" else {},
            internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests", "DynamicProxyGenAssembly2"],
            deps = [":platform", "@paket.source//system.text.json"],
        )
    _extension(
        name = "azdo_report",
        product = "Microsoft.Testing.Extensions.AzureDevOpsReport",
        linked_srcs = _ci_report_sources() + [_SHARED + "/ReportFileNameSanitizer.cs"],
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests", "DynamicProxyGenAssembly2"],
        deps = [":platform", "@paket.source//system.text.json", "@paket.source//microsoft.extensions.filesystemglobbing"],
    )
    _extension(
        name = "github_report",
        product = "Microsoft.Testing.Extensions.GitHubActionsReport",
        linked_srcs = _ci_report_sources() + [_PLATFORM + "/Helpers/TimeSpanParser.cs"],
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests", "DynamicProxyGenAssembly2"],
        deps = [":platform", "@paket.source//system.text.json"],
    )
    _extension(
        name = "packaged_app",
        product = "Microsoft.Testing.Extensions.PackagedApp",
        linked_srcs = [
            _PLATFORM + "/Helpers/PasteArguments.cs",
            _SHARED + "/RetryArtifactManifest.cs",
            _SHARED + "/TestCaseFilterCommandLineOptionsProviderBase.cs",
        ],
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests", "mstest-appmodel-controller"],
        deps = [":platform"],
    )
    _extension(
        name = "ai",
        product = "Microsoft.Testing.Platform.AI",
        build_info = False,
        version_override = "1.0.0",
        deps = [":platform", "@paket.source//microsoft.extensions.ai"],
    )
    _extension(
        name = "azure_foundry",
        product = "Microsoft.Testing.Extensions.AzureFoundry",
        build_info = False,
        version_override = "1.0.0",
        linked_srcs = [_PLATFORM + "/Helpers/RoslynString.cs"],
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests"],
        deps = [
            ":ai",
            "@paket.source//azure.ai.openai",
            "@paket.source//azure.core",
            "@paket.source//microsoft.extensions.ai.openai",
        ],
    )
    testfx_library(
        name = "mstest_hosting",
        project = "src/Adapter/MSTest.Extensions.Hosting",
        resources = {
            "src/Adapter/MSTest.Extensions.Hosting/Resources/HostingResources.resx": "Microsoft.VisualStudio.TestTools.UnitTesting.Hosting.Resources.HostingResources",
        },
        internals_visible_to = ["Microsoft.Testing.Extensions.UnitTests", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests"],
        deps = [":adapter", ":hosting", ":framework_extensions", "@paket.source//microsoft.extensions.dependencyinjection.abstractions"],
    )
