"""Compile analyzer assemblies using the repository's Roslyn API versions."""

load(":defs.bzl", "testfx_binary", "testfx_library")

def analyzer_libraries():
    testfx_library(
        name = "analyzers",
        project = "src/MSTest/MSTest.Analyzers",
        target_frameworks = ["netstandard2.0"],
        analyzer_configs = ["//eng/bazel:analyzers.globalconfig"],
        additionalfiles = [
            "src/MSTest/MSTest.Analyzers/AnalyzerReleases.Shipped.md",
            "src/MSTest/MSTest.Analyzers/AnalyzerReleases.Unshipped.md",
        ],
        linked_srcs = [
            "//eng/bazel:CompatibilityFrameworkMetadata.cs",
            "src/MTP/Microsoft.Testing.Platform/Helpers/RoslynHashCode.cs",
            "src/MTP/Microsoft.Testing.Platform/Helpers/ObjectPool.cs",
        ],
        resources = {"src/MSTest/MSTest.Analyzers/Resources.resx": "MSTest.Analyzers.Resources"},
        internals_visible_to = ["MSTest.Analyzers.CodeFixes"],
        deps = [
            "@paket.analyzers//microsoft.codeanalysis.common",
            "@paket.analyzers//microsoft.codeanalysis.analyzers",
        ],
    )
    testfx_library(
        name = "codefixes",
        project = "src/MSTest/MSTest.Analyzers.CodeFixes",
        target_frameworks = ["netstandard2.0"],
        linked_srcs = ["//eng/bazel:CompatibilityFrameworkMetadata.cs"],
        resources = {"src/MSTest/MSTest.Analyzers.CodeFixes/CodeFixResources.resx": "MSTest.Analyzers.CodeFixResources"},
        deps = [
            ":analyzers",
            "@paket.analyzers//microsoft.codeanalysis.csharp.workspaces",
            "@paket.analyzers//microsoft.codeanalysis.analyzers",
        ],
    )
    testfx_library(
        name = "mstest_generator",
        project = "src/MSTest/MSTest.SourceGeneration",
        target_frameworks = ["netstandard2.0"],
        assembly_version = "14.0.0.0",
        allow_unsafe_blocks = True,
        defines = ["EXCLUDE_RANGE_INDEX_POLYFILL"],
        linked_srcs = [
            "src/MSTest/Shared/EquatableArray.cs",
            "src/MSTest/Shared/SymbolAccessibilityHelper.cs",
            "src/MSTest/Shared/SourceGenModeHelper.cs",
        ],
        analyzer_configs = ["//eng/bazel:analyzers.globalconfig"],
        additionalfiles = [
            "src/MSTest/MSTest.SourceGeneration/BannedSymbols.txt",
            "src/MSTest/MSTest.SourceGeneration/AnalyzerReleases.Shipped.md",
            "src/MSTest/MSTest.SourceGeneration/AnalyzerReleases.Unshipped.md",
        ],
        internals_visible_to = ["MSTest.SourceGeneration.UnitTests"],
        deps = [
            "@paket.generator//microsoft.codeanalysis.csharp",
            "@paket.generator//microsoft.codeanalysis.analyzers",
        ],
    )
    testfx_binary(
        name = "globalconfigs_generator",
        project = "src/MSTest/MSTest.GlobalConfigsGenerator",
        polyfills = False,
        linked_srcs = [
            "//eng/bazel:GlobalConfigsEnvironment.cs",
            "src/MSTest/MSTest.Analyzers/Helpers/WellKnownCustomTags.cs",
        ],
        deps = ["@paket.analyzers//microsoft.codeanalysis.common"],
    )
