"""Exact, opt-in source-project compatibility variants; never a lower-TFM fallback."""

load("@rules_dotnet//dotnet:defs.bzl", "csharp_binary", "import_library")
load("@rules_dotnet//dotnet/private:providers.bzl", "DotnetAssemblyCompileInfo", "DotnetAssemblyRuntimeInfo")
load(":defs.bzl", "testfx_test")

_PROJECTS = {
    "netstandard2.0": [
        "ai",
        "azdo_report",
        "azure_foundry",
        "configuration",
        "crashdump",
        "ctrf",
        "framework",
        "github_report",
        "hangdump",
        "hosting",
        "hotreload",
        "html",
        "junit",
        "logging",
        "msbuild_extension",
        "opentelemetry",
        "platform",
        "platform_msbuild",
        "retry",
        "server_client",
        "telemetry",
        "trx",
        "trx_abstractions",
        "video",
        "vstest_bridge",
    ],
    "net462": [
        "adapter",
        "framework",
        "mstest_hosting",
        "platform_services",
    ],
}

def _sdk_netframework_assembly_impl(ctx):
    toolchain = ctx.toolchains["@rules_dotnet//dotnet:toolchain_type"]
    suffix = "/sdk/{}/Microsoft/Microsoft.NET.Build.Extensions/net461/lib/{}.dll".format(toolchain.dotnetinfo.sdk_version, ctx.attr.assembly_name)
    files = [file for file in toolchain.default.files.to_list() if file.path.endswith(suffix)]
    if len(files) != 1:
        fail("Expected the selected SDK's .NET Framework compatibility assembly at " + suffix)
    return [DefaultInfo(files = depset(files))]

_sdk_netframework_assembly = rule(
    implementation = _sdk_netframework_assembly_impl,
    attrs = {"assembly_name": attr.string(mandatory = True)},
    toolchains = ["@rules_dotnet//dotnet:toolchain_type"],
)

def _framework_transition_impl(_settings, attr):
    return {"@rules_dotnet//dotnet:target_framework": attr.target_framework}

_framework_transition = transition(
    implementation = _framework_transition_impl,
    inputs = [],
    outputs = ["@rules_dotnet//dotnet:target_framework"],
)

def _variant_impl(ctx):
    target = ctx.attr.assembly[0]
    compile_info = target[DotnetAssemblyCompileInfo]
    if compile_info.target_framework != ctx.attr.target_framework:
        fail("{} requested {}, but {} compiled for {}".format(
            ctx.label,
            ctx.attr.target_framework,
            target.label,
            compile_info.target_framework,
        ))

    return [
        target[DefaultInfo],
        compile_info,
        target[DotnetAssemblyRuntimeInfo],
        OutputGroupInfo(runtime_files = target[DefaultInfo].default_runfiles.files),
    ]

testfx_framework_variant = rule(
    implementation = _variant_impl,
    attrs = {
        "assembly": attr.label(
            mandatory = True,
            cfg = _framework_transition,
            providers = [DotnetAssemblyCompileInfo, DotnetAssemblyRuntimeInfo],
        ),
        "target_framework": attr.string(mandatory = True),
        "_allowlist_function_transition": attr.label(default = "@bazel_tools//tools/allowlists/function_transition_allowlist"),
    },
)

def _inventory_impl(ctx):
    manifest = ctx.actions.declare_file(ctx.label.name + ".json")
    entries = []
    for target in ctx.attr.assemblies:
        compile_info = target[DotnetAssemblyCompileInfo]
        entries.append({
            "target": str(target.label),
            "assembly": compile_info.name,
            "target_framework": compile_info.target_framework,
            "files": [file.path for file in target[DefaultInfo].files.to_list()],
            "satellites": [file.path for file in target[DotnetAssemblyRuntimeInfo].resource_assemblies],
            "runtime_dependencies": [
                {
                    "assembly": dependency.name,
                    "files": [file.path for file in dependency.libs],
                }
                for dependency in target[DotnetAssemblyRuntimeInfo].deps.to_list()
            ],
        })
    ctx.actions.write(manifest, json.encode_indent(entries) + "\n")
    return [DefaultInfo(files = depset([manifest], transitive = [
        target[DefaultInfo].files
        for target in ctx.attr.assemblies
    ]))]

_inventory = rule(
    implementation = _inventory_impl,
    attrs = {
        "assemblies": attr.label_list(providers = [DotnetAssemblyCompileInfo, DotnetAssemblyRuntimeInfo]),
    },
)

def compatibility_libraries():
    """Declare only frameworks that the corresponding source projects actually target."""
    for name, assembly, version in [
        ("netframework_netstandard_facade", "netstandard", "2.0.0"),
        ("netframework_runtime_information", "System.Runtime.InteropServices.RuntimeInformation", "4.0.2"),
    ]:
        _sdk_netframework_assembly(name = name + "_file", assembly_name = assembly)
        import_library(
            name = name,
            library_name = assembly,
            version = version,
            refs = [":" + name + "_file"],
            libs = [":" + name + "_file"],
        )
    inventories = []
    for framework, projects in _PROJECTS.items():
        suffix = framework.replace(".", "_")
        for project in projects:
            testfx_framework_variant(
                name = project + "_" + suffix,
                assembly = ":" + project,
                target_framework = framework,
            )
        inventory = "managed_" + suffix
        _inventory(
            name = inventory,
            assemblies = [":" + project + "_" + suffix for project in projects],
        )
        inventories.append(":" + inventory)
    native.filegroup(name = "managed_compatibility", srcs = inventories)

    csharp_binary(
        name = "compatibility_explorer",
        srcs = ["//samples/public/TestingPlatformExamples/TestingPlatformExplorer:sources"],
        out = "TestingPlatformExplorer",
        appsetting_files = ["//samples/public/TestingPlatformExamples/TestingPlatformExplorer:TestingPlatformExplorer.testingplatformconfig.json"],
        generate_documentation_file = False,
        langversion = "preview",
        nullable = "enable",
        target_frameworks = ["net8.0"],
        deps = [
            ":platform_netstandard2_0",
            ":trx_netstandard2_0",
            ":trx_abstractions_netstandard2_0",
        ],
    )
    testfx_test(
        name = "compatibility_explorer_test",
        size = "small",
        binary = ":compatibility_explorer",
        args = [
            "--filter-uid",
            "TestingPlatformExplorer.UnitTests.SomeTests.TestMethod1",
            "--minimum-expected-tests",
            "1",
        ],
    )
    csharp_binary(
        name = "framework_netstandard2_0_consumer",
        srcs = ["//eng/bazel:CompatibilityConsumer.cs"],
        out = "CompatibilityConsumer",
        target_frameworks = ["net8.0"],
        langversion = "preview",
        nullable = "enable",
        generate_documentation_file = False,
        deps = [":framework_netstandard2_0"],
    )
    testfx_test(
        name = "framework_netstandard2_0_consumer_test",
        size = "small",
        binary = ":framework_netstandard2_0_consumer",
        args = [".NETStandard,Version=v2.0"],
    )
