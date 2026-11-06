"""Native MSTest.Analyzers package layout; compiler-host dependencies stay private."""

load(
    "@rules_dotnet//dotnet/private:providers.bzl",
    "DotnetAssemblyCompileInfo",
    "DotnetAssemblyRuntimeInfo",
    "NuGetPackInfo",
)

_PROJECT = "src/Analyzers/MSTest.Analyzers.Package"
_MODES = ["none", "default", "recommended", "all"]

def _assembly(target, expected_name):
    compile_info = target[DotnetAssemblyCompileInfo]
    runtime_info = target[DotnetAssemblyRuntimeInfo]
    if compile_info.target_framework != "netstandard2.0":
        fail("{} must retain its netstandard2.0 compiler-host contract".format(target.label))
    if len(runtime_info.libs) != 1 or runtime_info.libs[0].basename != expected_name + ".dll":
        fail("{} must produce exactly {}.dll".format(target.label, expected_name))
    return runtime_info

def _analyzer_package_impl(ctx):
    analyzer = _assembly(ctx.attr.analyzer, "MSTest.Analyzers")
    fixes = _assembly(ctx.attr.codefixes, "MSTest.Analyzers.CodeFixes")
    version = ctx.actions.declare_file(ctx.label.name + "/version.txt")
    ctx.actions.run(
        executable = ctx.attr._version_tool[DefaultInfo].files_to_run,
        arguments = ["version", ctx.file._versions.path, "VersionPrefix", version.path],
        inputs = [ctx.file._versions],
        outputs = [version],
        mnemonic = "TestFxAnalyzerPackageVersion",
    )

    staged_analyzer = ctx.actions.declare_file(ctx.label.name + "/generated/MSTest.Analyzers.dll")
    ctx.actions.symlink(output = staged_analyzer, target_file = analyzer.libs[0])
    configs = [
        ctx.actions.declare_file(ctx.label.name + "/generated/globalconfigs/mstest-" + mode + ".globalconfig")
        for mode in _MODES
    ]
    ctx.actions.run(
        executable = ctx.attr._generator[DefaultInfo].files_to_run,
        inputs = [staged_analyzer],
        outputs = configs,
        env = {"OUTPUT_PATH": staged_analyzer.dirname},
        mnemonic = "TestFxAnalyzerGlobalConfigs",
    )

    entries = []
    for language, assemblies in [("cs", [analyzer, fixes]), ("vb", [analyzer])]:
        directory = "analyzers/dotnet/" + language + "/"
        for assembly in assemblies:
            entries.append((directory + assembly.libs[0].basename, assembly.libs[0]))
            for satellite in assembly.resource_assemblies:
                culture = satellite.dirname.rsplit("/", 1)[-1]
                entries.append((directory + culture + "/" + satellite.basename, satellite))
    entries += [("globalconfigs/" + file.basename, file) for file in configs]
    entries += [("PACKAGE.md", ctx.file.readme)]
    for file in ctx.files.assets:
        if not file.short_path.startswith(_PROJECT + "/"):
            fail("Unexpected analyzer package asset: " + file.short_path)
        entries.append((file.short_path[len(_PROJECT) + 1:], file))
    entries = sorted(entries, key = lambda entry: entry[0])

    package = ctx.actions.declare_file(ctx.label.name + "/" + ctx.label.name + ".nupkg")
    nuspec = ctx.actions.declare_file(ctx.label.name + "/MSTest.Analyzers.nuspec")
    request = ctx.actions.declare_file(ctx.label.name + "/nuget_pack_request.json")
    ctx.actions.write(request, json.encode(struct(
        output = package.path,
        nuspecOutput = nuspec.path,
        versionFile = version.path,
        metadata = struct(
            id = "MSTest.Analyzers",
            authors = ["Microsoft"],
            description = "Provides code analyzers and C# code fixes for MSTest. Experimental native Bazel build; not an official release package.",
            developmentDependency = True,
            license = struct(type = "expression", value = "MIT"),
            readme = "PACKAGE.md",
            repository = struct(type = "git", url = "https://github.com/microsoft/testfx"),
        ),
        dependencyGroups = [],
        files = [struct(source = file.path, target = path) for path, file in entries],
    )))
    ctx.actions.run(
        executable = ctx.attr._packer[DefaultInfo].files_to_run,
        arguments = [request.path],
        inputs = depset([request, version] + [file for _, file in entries]),
        outputs = [package, nuspec],
        mnemonic = "NuGetPack",
    )
    return [
        DefaultInfo(files = depset([package])),
        OutputGroupInfo(nuspec = depset([nuspec]), globalconfigs = depset(configs), version = depset([version])),
        NuGetPackInfo(
            label = ctx.label,
            package_id = "MSTest.Analyzers",
            version = None,
            version_file = version,
            nupkg = package,
            snupkg = None,
            target_frameworks = ["netstandard2.0"],
            bundled_assemblies = ["MSTest.Analyzers", "MSTest.Analyzers.CodeFixes"],
            is_tool = False,
        ),
    ]

_analyzer_package = rule(
    implementation = _analyzer_package_impl,
    attrs = {
        "analyzer": attr.label(mandatory = True, providers = [DotnetAssemblyCompileInfo, DotnetAssemblyRuntimeInfo]),
        "codefixes": attr.label(mandatory = True, providers = [DotnetAssemblyCompileInfo, DotnetAssemblyRuntimeInfo]),
        "readme": attr.label(mandatory = True, allow_single_file = [".md"]),
        "assets": attr.label_list(allow_files = True),
        "_versions": attr.label(default = "//:eng/Versions.props", allow_single_file = True),
        "_version_tool": attr.label(default = "//eng/bazel:build_tools", executable = True, cfg = "exec"),
        "_generator": attr.label(default = "//:globalconfigs_generator", executable = True, cfg = "exec"),
        "_packer": attr.label(default = "@rules_dotnet//dotnet/private/tools/nuget_pack", executable = True, cfg = "exec"),
    },
)

def analyzer_packages():
    _analyzer_package(
        name = "analyzers_package",
        analyzer = ":analyzers",
        codefixes = ":codefixes",
        readme = _PROJECT + "/PACKAGE.md",
        assets = native.glob([
            _PROJECT + "/buildTransitive/*",
            _PROJECT + "/tools/*.ps1",
        ]),
    )
