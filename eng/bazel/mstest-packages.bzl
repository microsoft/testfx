"""The portable MSTest package layouts, with explicitly owned bundled payloads."""

load(
    "@rules_dotnet//dotnet/private:providers.bzl",
    "DotnetAssemblyCompileInfo",
    "DotnetAssemblyRuntimeInfo",
    "NuGetPackInfo",
)
load("@rules_dotnet//dotnet/private/rules/nuget_pack:layout.bzl", "extra_files_layout")
load(":defs.bzl", "PORTABLE_FRAMEWORKS")
load(":packages.bzl", "package_build_assets", "package_version")

def _frameworks_impl(_settings, attr):
    return {
        framework: {"@rules_dotnet//dotnet:target_framework": framework}
        for framework in attr.target_frameworks
    }

_frameworks = transition(
    implementation = _frameworks_impl,
    inputs = [],
    outputs = ["@rules_dotnet//dotnet:target_framework"],
)

def _tasks_impl(_settings, _attr):
    return {"@rules_dotnet//dotnet:target_framework": "netstandard2.0"}

_tasks = transition(
    implementation = _tasks_impl,
    inputs = [],
    outputs = ["@rules_dotnet//dotnet:target_framework"],
)

def _add_assembly(entries, target, framework, directory, resource_directory):
    compile_info = target[DotnetAssemblyCompileInfo]
    info = target[DotnetAssemblyRuntimeInfo]
    if compile_info.target_framework != framework or len(info.libs) != 1:
        fail("{} must provide one native {} implementation, not {}".format(target.label, framework, compile_info.target_framework))
    for file in info.libs + info.xml_docs + info.pdbs:
        entries.append((directory + "/" + file.basename, file))
    if resource_directory:
        for file in info.resource_assemblies:
            culture = file.dirname.split("/")[-1]
            entries.append((resource_directory + "/" + culture + "/" + file.basename, file))
    return info.name

def _package_impl(ctx):
    entries = extra_files_layout(ctx.attr.files, ctx.label)
    names = {}
    groups = []
    version_files = [ctx.file.version_file]
    dependencies = []
    for target in ctx.attr.deps:
        package = target[NuGetPackInfo]
        version_file = package.version_file
        if version_file:
            version_files.append(version_file)
        if ctx.attr.bound_platform and package.package_id == "Microsoft.Testing.Platform":
            if not version_file:
                fail("The native platform dependency must carry its generated version file.")
            bound = ctx.actions.declare_file(ctx.label.name + "/platform-dependency-range.txt")
            ctx.actions.run(
                executable = ctx.executable._range_tool,
                arguments = [version_file.path, bound.path],
                inputs = [version_file],
                outputs = [bound],
                mnemonic = "TestFxPlatformDependencyRange",
            )
            version_file = bound
            version_files.append(bound)
        dependencies.append(struct(
            id = package.package_id,
            version = package.version or "",
            versionFile = version_file.path if version_file else None,
            exclude = ctx.attr.dependency_excludes.get(package.package_id, []),
        ))

    if len(ctx.attr.directories) != len(ctx.attr.resource_directories):
        fail("Each assembly directory needs an explicit satellite-resource policy.")
    for framework in ctx.attr.target_frameworks:
        targets = ctx.split_attr.libraries.get(framework, [])
        if len(targets) != len(ctx.attr.directories):
            fail("{} is missing native assembly outputs for {}".format(ctx.label, framework))
        for index, target in enumerate(targets):
            directory = ctx.attr.directories[index].replace("{tfm}", framework)
            resource_directory = ctx.attr.resource_directories[index]
            # Adapter satellites are shared once, using the lowest portable TFM.
            if "{tfm}" not in resource_directory and framework != ctx.attr.target_frameworks[0]:
                resource_directory = ""
            name = _add_assembly(entries, target, framework, directory, resource_directory.replace("{tfm}", framework))
            names[name] = True

        external_dependencies = []
        if ctx.attr.nuget_deps and framework not in ctx.split_attr.nuget_deps:
            fail("{} is missing external dependency metadata for {}".format(ctx.label, framework))
        for target in ctx.split_attr.nuget_deps.get(framework, []):
            info = target[DotnetAssemblyRuntimeInfo]
            if info.nuget_info == None:
                fail("{} is not an external NuGet dependency".format(target.label))
            external_dependencies.append(struct(id = info.name, version = info.version, exclude = ["Build", "Analyzers"]))
        groups.append(struct(targetFramework = framework, dependencies = dependencies + external_dependencies))

    for target in ctx.attr.task_libraries:
        _add_assembly(entries, target, "netstandard2.0", "_MSBuildTasks/netstandard2.0", "_MSBuildTasks/netstandard2.0")

    entries.append(("PACKAGE.md", ctx.file.readme))
    by_path = {}
    for path, file in entries:
        if path in by_path:
            fail("{} repeats package path {}".format(ctx.label, path))
        by_path[path] = file

    nupkg = ctx.actions.declare_file(ctx.label.name + "/" + ctx.label.name + ".nupkg")
    nuspec = ctx.actions.declare_file(ctx.label.name + "/" + ctx.label.name + ".nuspec")
    request = ctx.actions.declare_file(ctx.label.name + "/nuget_pack_request.json")
    ctx.actions.write(request, json.encode(struct(
        output = nupkg.path,
        nuspecOutput = nuspec.path,
        versionFile = ctx.file.version_file.path,
        metadata = struct(
            id = ctx.attr.package_id,
            authors = ["Microsoft"],
            description = ctx.attr.description + " Experimental native Bazel build; not an official release package.",
            license = struct(type = "expression", value = "MIT"),
            readme = "PACKAGE.md",
            repository = struct(type = "git", url = "https://github.com/microsoft/testfx"),
        ),
        dependencyGroups = groups,
        files = [struct(source = by_path[path].path, target = path) for path in sorted(by_path.keys())],
    )))
    ctx.actions.run(
        executable = ctx.attr._packer[DefaultInfo].files_to_run,
        arguments = [request.path],
        inputs = depset([request] + version_files + by_path.values()),
        outputs = [nupkg, nuspec],
        mnemonic = "NuGetPack",
        progress_message = "Packing native MSTest closure %{label}",
    )
    return [
        DefaultInfo(files = depset([nupkg])),
        OutputGroupInfo(nuspec = depset([nuspec]), package_request = depset([request])),
        NuGetPackInfo(
            label = ctx.label,
            package_id = ctx.attr.package_id,
            version = None,
            version_file = ctx.file.version_file,
            nupkg = nupkg,
            snupkg = None,
            target_frameworks = ctx.attr.target_frameworks,
            bundled_assemblies = sorted(names.keys()),
            is_tool = False,
        ),
    ]

_package = rule(
    implementation = _package_impl,
    attrs = {
        "libraries": attr.label_list(providers = [DotnetAssemblyCompileInfo, DotnetAssemblyRuntimeInfo], cfg = _frameworks),
        "directories": attr.string_list(),
        "resource_directories": attr.string_list(),
        "task_libraries": attr.label_list(providers = [DotnetAssemblyCompileInfo, DotnetAssemblyRuntimeInfo], cfg = _tasks),
        "files": attr.label_keyed_string_dict(allow_files = True),
        "deps": attr.label_list(providers = [NuGetPackInfo]),
        "dependency_excludes": attr.string_list_dict(),
        "nuget_deps": attr.label_list(providers = [DotnetAssemblyRuntimeInfo], cfg = _frameworks),
        "bound_platform": attr.bool(),
        "package_id": attr.string(mandatory = True),
        "description": attr.string(mandatory = True),
        "readme": attr.label(allow_single_file = True, mandatory = True),
        "version_file": attr.label(allow_single_file = True, mandatory = True),
        "target_frameworks": attr.string_list(default = PORTABLE_FRAMEWORKS),
        "_range_tool": attr.label(default = "//eng/bazel:mstest_package_version", executable = True, cfg = "exec"),
        "_packer": attr.label(default = "@rules_dotnet//dotnet/private/tools/nuget_pack", executable = True, cfg = "exec"),
        "_allowlist_function_transition": attr.label(default = "@bazel_tools//tools/allowlists/function_transition_allowlist"),
    },
)

def _asset(files, name, source, destination):
    native.filegroup(name = name, srcs = [source])
    files[":" + name] = destination

def native_mstest_packages():
    """Declare portable packages using the explicit compatibility task wrappers."""
    package_version(name = "mstest_package_version", property_name = "VersionPrefix")
    framework_project = "src/TestFramework/TestFramework.Extensions"
    adapter_project = "src/Adapter/MSTest.TestAdapter"
    framework_files = {}
    adapter_files = {}
    for framework in PORTABLE_FRAMEWORKS:
        suffix = framework.replace(".", "_")
        _asset(framework_files, "framework_build_" + suffix, framework_project + "/build/" + framework + "/MSTest.TestFramework.targets", "build/" + framework + "/MSTest.TestFramework.targets")
        _asset(framework_files, "framework_transitive_" + suffix, framework_project + "/buildTransitive/net8.0AndLater/MSTest.TestFramework.targets", "buildTransitive/" + framework + "/MSTest.TestFramework.targets")
        for extension in ["props", "targets"]:
            filename = "MSTest.TestAdapter." + extension
            _asset(adapter_files, "adapter_build_" + suffix + "_" + extension, adapter_project + "/build/" + framework + "/" + filename, "build/" + framework + "/" + filename)
            _asset(adapter_files, "adapter_transitive_" + suffix + "_" + extension, adapter_project + "/buildTransitive/common/" + filename, "buildTransitive/" + framework + "/" + filename)
        _asset(adapter_files, "adapter_parallelize_" + suffix, adapter_project + "/buildTransitive/Parallelize.targets", "buildTransitive/" + framework + "/Parallelize.targets")

    _package(
        name = "framework_package",
        package_id = "MSTest.TestFramework",
        version_file = ":mstest_package_version",
        description = "Provides the libraries for writing tests with MSTest. MSTest is Microsoft supported Test Framework.",
        readme = framework_project + "/PACKAGE.md",
        libraries = [":framework", ":framework_extensions"],
        directories = ["lib/{tfm}", "buildTransitive/{tfm}"],
        resource_directories = ["lib/{tfm}", ""],
        files = framework_files,
        deps = [":analyzers_package"],
        dependency_excludes = {"MSTest.Analyzers": ["Compile", "Runtime"]},
    )
    _package(
        name = "telemetry_package",
        package_id = "Microsoft.Testing.Extensions.Telemetry",
        version_file = ":platform_package_version",
        description = "Provides telemetry for Microsoft Testing Platform.",
        readme = "src/Platform/Microsoft.Testing.Extensions.Telemetry/PACKAGE.md",
        libraries = [":telemetry"],
        directories = ["lib/{tfm}"],
        resource_directories = ["lib/{tfm}"],
        files = package_build_assets("telemetry_package", "src/Platform/Microsoft.Testing.Extensions.Telemetry"),
        deps = [":platform_package"],
        dependency_excludes = {"Microsoft.Testing.Platform": ["Build", "Analyzers"]},
        bound_platform = True,
        nuget_deps = ["@paket.source//microsoft.applicationinsights", "@paket.source//system.diagnostics.diagnosticsource"],
    )
    msbuild_project = "src/Platform/Microsoft.Testing.Platform.MSBuild"
    _package(
        name = "platform_msbuild_package",
        package_id = "Microsoft.Testing.Platform.MSBuild",
        version_file = ":platform_package_version",
        description = "Provides MSBuild integration, generated entry points, and configuration for Microsoft Testing Platform.",
        readme = msbuild_project + "/PACKAGE.md",
        libraries = [":msbuild_extension"],
        directories = ["lib/{tfm}"],
        resource_directories = ["lib/{tfm}"],
        task_libraries = [":platform_msbuild_netstandard2_0", ":platform_netstandard2_0"],
        files = {path: path[len(msbuild_project) + 1:] for path in native.glob([msbuild_project + "/build*/**/*.props", msbuild_project + "/build*/**/*.targets"])},
        deps = [":platform_package"],
        dependency_excludes = {"Microsoft.Testing.Platform": ["Build", "Analyzers"]},
        bound_platform = True,
    )
    _package(
        name = "adapter_package",
        package_id = "MSTest.TestAdapter",
        version_file = ":mstest_package_version",
        description = "Discovers and runs MSTest tests using VSTest or Microsoft Testing Platform. MSTest is Microsoft supported Test Framework.",
        readme = adapter_project + "/PACKAGE.md",
        libraries = [":adapter", ":platform_services"],
        directories = ["buildTransitive/{tfm}", "buildTransitive/{tfm}"],
        resource_directories = ["buildTransitive/_localization", "buildTransitive/_localization"],
        files = adapter_files,
        deps = [":trx_abstractions_package", ":telemetry_package", ":platform_msbuild_package"] + select({
            "@platforms//os:windows": [":framework_package"],
            "//conditions:default": [],
        }),
        nuget_deps = ["@paket.source//microsoft.testplatform.objectmodel"],
    )
    native.filegroup(
        name = "mstest_packages",
        srcs = [":framework_package", ":adapter_package", ":telemetry_package", ":platform_msbuild_package"],
    )
    native.filegroup(
        name = "mstest_package_closure",
        srcs = [":mstest_packages", ":packages", ":analyzers_package"],
    )
