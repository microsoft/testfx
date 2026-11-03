"""Exact, opt-in source-project compatibility variants; never a lower-TFM fallback."""

load("@rules_dotnet//dotnet/private:providers.bzl", "DotnetAssemblyCompileInfo", "DotnetAssemblyRuntimeInfo")

_PROJECTS = {
    "netstandard2.0": [
        "msbuild_extension",
        "platform",
        "platform_msbuild",
    ],
}

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
