"""Native MSTest source assemblies and their compiler generator."""

load(":defs.bzl", "DESKTOP_FRAMEWORKS", "STANDARD_FRAMEWORKS", "testfx_analyzer", "testfx_compatibility_select", "testfx_library", "testfx_nuget")

_PLATFORM = "src/Platform/Microsoft.Testing.Platform"
_SHARED = "src/Platform/SharedExtensionHelpers"
_FRAMEWORK = "src/TestFramework/TestFramework"

def mstest_libraries():
    testfx_library(
        name = "assert_generator",
        project = "src/TestFramework/TestFramework.SourceGeneration",
        target_frameworks = ["netstandard2.0"],
        polyfills = False,
        analyzer_configs = ["//eng/bazel:analyzers.globalconfig"],
        deps = ["@paket.generator//microsoft.codeanalysis.csharp"],
    )
    testfx_analyzer(
        name = "assert_generator_plugin",
        assembly = ":assert_generator",
        # Keep the analyzer-only provider distinct from the runtime test subject.
        library_name = "TestFramework.SourceGeneration.Analyzer",
    )
    testfx_library(
        name = "framework",
        project = _FRAMEWORK,
        target_frameworks = STANDARD_FRAMEWORKS + ["net462"],
        assembly_name = "MSTest.TestFramework",
        defines = ["EXCLUDE_RANGE_INDEX_POLYFILL", "EXCLUDE_OS_POLYFILL", "TESTFRAMEWORK_CI_DETECTOR"],
        linked_srcs = [
            _PLATFORM + "/Helpers/RoslynHashCode.cs",
            _PLATFORM + "/Helpers/CIEnvironmentDetector.cs",
        ] + testfx_compatibility_select(["//eng/bazel:CompatibilityFrameworkMetadata.cs"]),
        resources = {_FRAMEWORK + "/Resources/FrameworkMessages.resx": "Microsoft.VisualStudio.TestTools.UnitTesting.Resources.FrameworkMessages"},
        usings = ["Polyfills", "Microsoft.VisualStudio.TestTools.UnitTesting.Resources"],
        internals_visible_to = [
            "DynamicProxyGenAssembly2",
            "MSTestAdapter.PlatformServices",
            "MSTest.TestAdapter",
            "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests",
            "MSTest.TestFramework.Extensions",
            "Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests",
            "MSTestAdapter.PlatformServices.UnitTests",
            "MSTest.Performance.Benchmarks",
        ],
        deps = [":assert_generator_plugin"],
    )
    testfx_library(
        name = "framework_extensions",
        project = "src/TestFramework/TestFramework.Extensions",
        target_frameworks = STANDARD_FRAMEWORKS + ["net462"],
        linked_srcs = testfx_compatibility_select(["//eng/bazel:CompatibilityFrameworkMetadata.cs"]),
        assembly_name = "MSTest.TestFramework.Extensions",
        defines = ["EXCLUDE_RANGE_INDEX_POLYFILL", "EXCLUDE_OS_POLYFILL"],
        usings = ["Polyfills", "Microsoft.VisualStudio.TestTools.UnitTesting.Resources"],
        resources = {
            "src/TestFramework/TestFramework.Extensions/Resources/FrameworkExtensionsMessages.resx": "Microsoft.VisualStudio.TestTools.UnitTesting.Resources.FrameworkExtensionsMessages",
        },
        internals_visible_to = [
            "MSTestAdapter.PlatformServices",
            "MSTestAdapter.PlatformServices.UnitTests",
            "MSTest.TestAdapter",
            "MSTest.Extensions.Hosting",
            "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests",
        ],
        deps = [":framework"],
    )
    testfx_library(
        name = "platform_services",
        project = "src/Adapter/MSTestAdapter.PlatformServices",
        target_frameworks = DESKTOP_FRAMEWORKS,
        allow_unsafe_blocks = True,
        defines = ["EXCLUDE_OS_POLYFILL"],
        usings = ["Polyfills", "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources"],
        linked_srcs = native.glob(["src/Platform/Microsoft.Testing.Extensions.TrxReport/Hashing/**/*.cs"]) + testfx_compatibility_select(["//eng/bazel:CompatibilityFrameworkMetadata.cs"]),
        resources = {
            "src/Adapter/MSTestAdapter.PlatformServices/Resources/Resource.resx": "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources.Resource",
        },
        internals_visible_to = [
            "DynamicProxyGenAssembly2",
            "MSTest.TestAdapter",
            "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests",
            "MSTestAdapter.PlatformServices.UnitTests",
            "MSTest.Acceptance.IntegrationTests",
            "MSTest.Performance.Benchmarks",
        ],
        deps = [":framework", ":framework_extensions"] + testfx_compatibility_select([
            "@paket.compatibility_mstest//system.memory",
            "@paket.compatibility_mstest//system.threading.tasks.extensions",
        ]),
    )
    testfx_library(
        name = "adapter",
        project = "src/Adapter/MSTest.TestAdapter",
        target_frameworks = DESKTOP_FRAMEWORKS,
        defines = ["MSTEST_TESTADAPTER"],
        usings = ["Polyfills", "ExecutionScope=Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope"],
        linked_srcs = [
            "src/Adapter/Shared/BannerMessageHelper.cs",
            _SHARED + "/RunSettingsProviderHelper.cs",
            _SHARED + "/RunSettingsCommandLineOptionsProviderBase.cs",
            _SHARED + "/RunSettingsConfigurationProviderBase.cs",
            _SHARED + "/RunSettingsEnvironmentVariableProviderBase.cs",
            _SHARED + "/TestRunParametersCommandLineOptionsProviderBase.cs",
            _SHARED + "/TestCaseFilterCommandLineOptionsProviderBase.cs",
            "@paket.source//microsoft.testplatform.filter.source:content_files",
        ] + testfx_compatibility_select(["//eng/bazel:CompatibilityFrameworkMetadata.cs"]),
        resources = {
            "src/Adapter/MSTest.TestAdapter/Resources/PlatformAdapterResources.resx": "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources.PlatformAdapterResources",
        },
        build_info = "src/Adapter/MSTest.TestAdapter/BuildInfo.cs.template",
        internals_visible_to = [
            "MSTest.Extensions.Hosting",
            "DynamicProxyGenAssembly2",
            "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests",
            "MSTestAdapter.PlatformServices.UnitTests",
            "MSTest.Acceptance.IntegrationTests",
            "MSTest.Performance.Benchmarks",
            "MSTest.Analyzers.UnitTests",
        ],
        deps = [
            ":platform",
            ":platform_services",
            ":framework",
            ":trx_abstractions",
            ":trx",
            ":telemetry",
            ":msbuild_extension",
            ":retry",
            ":platform_msbuild",
            ":framework_extensions",
        ] + testfx_nuget(["microsoft.testplatform.objectmodel"], compatibility_group = "compatibility_mstest") + testfx_compatibility_select([
            "@paket.compatibility_mstest//system.memory",
            "@paket.compatibility_mstest//system.threading.tasks.extensions",
        ]) + select({
            "//eng/bazel/compatibility:net462": [":netframework_netstandard_facade", ":netframework_runtime_information"],
            "//conditions:default": [],
        }),
    )
    testfx_library(
        name = "internal_test_framework",
        project = "test/Utilities/TestFramework.ForTestingMSTest",
        deps = [":platform", ":trx_abstractions", ":platform_msbuild"],
    )
