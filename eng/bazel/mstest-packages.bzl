"""MSTest package layouts, with explicitly owned core and platform payloads."""

load(
    "@rules_dotnet//dotnet/private:providers.bzl",
    "DotnetAssemblyCompileInfo",
    "DotnetAssemblyRuntimeInfo",
    "NuGetPackInfo",
)
load("@rules_dotnet//dotnet/private/rules/nuget_pack:layout.bzl", "extra_files_layout")
load(":defs.bzl", "PORTABLE_FRAMEWORKS", "STANDARD_FRAMEWORKS")
load(":packages.bzl", "package_build_assets", "package_version")

def mstest_package_tfm(framework):
    """Match NuGet's Windows folder normalization without shortening classic UAP."""
    if "-windows" in framework and len(framework.split("-windows")[1].split(".")) == 4:
        return framework.removesuffix(".0")
    return framework

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
    platform_version_file = None
    core_version_file = None
    dependencies = []
    for target in ctx.attr.deps:
        package = target[NuGetPackInfo]
        version_file = package.version_file
        if version_file:
            version_files.append(version_file)
        if ctx.attr.bound_platform and package.package_id == "Microsoft.Testing.Platform":
            if not version_file:
                fail("The native platform dependency must carry its generated version file.")
            platform_version_file = version_file
        if ctx.attr.exact_core_dependency and package.package_id == "MSTest.TestFramework":
            if not version_file:
                fail("The native core dependency must carry its generated version file.")
            core_version_file = version_file
        dependencies.append(struct(
            id = package.package_id,
            version = package.version or "",
            versionFile = version_file.path if version_file else None,
            exclude = ctx.attr.dependency_excludes.get(package.package_id, []),
        ))

    if ctx.attr.bound_platform and not platform_version_file:
        fail("A bounded package must declare the native platform dependency.")
    if ctx.attr.exact_core_dependency and not core_version_file:
        fail("An exact core package must declare the native framework dependency.")
    if core_version_file and platform_version_file:
        fail("Core pinning and platform bounds must use separate package contracts.")
    if len(ctx.attr.directories) != len(ctx.attr.resource_directories):
        fail("Each assembly directory needs an explicit satellite-resource policy.")
    for framework in ctx.attr.target_frameworks:
        targets = ctx.split_attr.libraries.get(framework, [])
        if len(targets) != len(ctx.attr.directories):
            fail("{} is missing native assembly outputs for {}".format(ctx.label, framework))
        for index, target in enumerate(targets):
            package_tfm = mstest_package_tfm(framework)
            directory = ctx.attr.directories[index].replace("{tfm}", package_tfm)
            resource_directory = ctx.attr.resource_directories[index]
            # Ordinary adapter satellites are shared once, from its first TFM.
            if "{tfm}" not in resource_directory and framework != ctx.attr.target_frameworks[0]:
                resource_directory = ""
            name = _add_assembly(entries, target, framework, directory, resource_directory.replace("{tfm}", package_tfm))
            names[name] = True

        external_dependencies = []
        if ctx.attr.nuget_deps and framework not in ctx.split_attr.nuget_deps:
            fail("{} is missing external dependency metadata for {}".format(ctx.label, framework))
        external_targets = list(ctx.split_attr.nuget_deps.get(framework, []))
        if framework == "net462":
            external_targets += ctx.split_attr.netframework_nuget_deps.get(framework, [])
        for target in external_targets:
            info = target[DotnetAssemblyRuntimeInfo]
            if info.nuget_info == None:
                fail("{} is not an external NuGet dependency".format(target.label))
            external_dependencies.append(struct(id = info.name, version = info.version, exclude = ["Build", "Analyzers"]))
        groups.append(struct(targetFramework = mstest_package_tfm(framework), dependencies = dependencies + external_dependencies))

    for target in ctx.attr.resolver_libraries:
        info = target[DotnetAssemblyRuntimeInfo]
        if target[DotnetAssemblyCompileInfo].target_framework != "net462" or len(info.libs) != 1:
            fail("The VSTest resolver payload must be an exact net462 core implementation.")
        for file in info.libs:
            entries.append(("buildTransitive/net462/" + file.basename, file))
        names[info.name] = True

    for target in ctx.attr.task_libraries:
        _add_assembly(entries, target, "netstandard2.0", "_MSBuildTasks/netstandard2.0", "_MSBuildTasks/netstandard2.0")
        # Match the shipping task project's RuntimeTargetsCopyLocalItems payload.
        # MSBuild's reference-only Framework/Utilities assemblies remain host-owned.
        for dependency in target[DotnetAssemblyRuntimeInfo].deps.to_list():
            if dependency.nuget_info == None:
                continue
            for file in dependency.libs + dependency.native:
                if "/runtimes/" in file.path:
                    entries.append(("_MSBuildTasks/netstandard2.0/" + file.basename, file))

    entries.append(("PACKAGE.md", ctx.file.readme))
    by_path = {}
    for path, file in entries:
        if path in by_path:
            fail("{} repeats package path {}".format(ctx.label, path))
        by_path[path] = file

    nupkg = ctx.actions.declare_file(ctx.label.name + "/" + ctx.label.name + ".nupkg")
    nuspec = ctx.actions.declare_file(ctx.label.name + "/" + ctx.label.name + ".nuspec")
    request = ctx.actions.declare_file(ctx.label.name + "/nuget_pack_request.json")
    range_version_file = core_version_file or platform_version_file
    template = ctx.actions.declare_file(ctx.label.name + "/nuget_pack_request.template.json") if range_version_file else request
    ctx.actions.write(template, json.encode(struct(
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
    if range_version_file:
        arguments = [template.path, range_version_file.path, request.path]
        inputs = [template, range_version_file]
        if core_version_file:
            arguments.append("--exact-framework")
            inputs.append(ctx.file.version_file)
        ctx.actions.run(
            executable = ctx.executable._range_tool,
            arguments = arguments,
            inputs = depset(inputs),
            outputs = [request],
            mnemonic = "TestFxFrameworkDependencyRange" if core_version_file else "TestFxPlatformDependencyRange",
        )
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
        "netframework_nuget_deps": attr.label_list(providers = [DotnetAssemblyRuntimeInfo], cfg = _frameworks),
        "resolver_libraries": attr.label_list(providers = [DotnetAssemblyCompileInfo, DotnetAssemblyRuntimeInfo]),
        "bound_platform": attr.bool(),
        "exact_core_dependency": attr.bool(),
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
    """Declare ordinary packages and opt-in modern Windows platform packages."""
    package_version(name = "mstest_package_version", property_name = "VersionPrefix")
    framework_project = "src/TestFramework/TestFramework"
    adapter_project = "src/Adapter/MSTest.TestAdapter"
    framework_files = {framework_project + "/MSTest.TestFramework.targets": "buildTransitive/MSTest.TestFramework.targets"}
    adapter_files = {}
    windows_adapter_files = {}
    for framework in PORTABLE_FRAMEWORKS + ["net462"]:
        suffix = framework.replace(".", "_")
        files = windows_adapter_files if framework == "net462" else adapter_files
        for extension in ["props", "targets"]:
            filename = "MSTest.TestAdapter." + extension
            _asset(files, "adapter_build_" + suffix + "_" + extension, adapter_project + "/build/" + framework + "/" + filename, "build/" + framework + "/" + filename)
            _asset(files, "adapter_transitive_" + suffix + "_" + extension, adapter_project + "/buildTransitive/common/" + filename, "buildTransitive/" + framework + "/" + filename)
        _asset(files, "adapter_parallelize_" + suffix, adapter_project + "/buildTransitive/Parallelize.targets", "buildTransitive/" + framework + "/Parallelize.targets")

    _package(
        name = "framework_package",
        package_id = "MSTest.TestFramework",
        version_file = ":mstest_package_version",
        description = "Provides the libraries for writing tests with MSTest. MSTest is Microsoft supported Test Framework.",
        readme = framework_project + "/PACKAGE.md",
        libraries = [":framework"],
        target_frameworks = STANDARD_FRAMEWORKS + ["net462"],
        directories = ["lib/{tfm}"],
        resource_directories = ["lib/{tfm}"],
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
        target_frameworks = select({
            "@platforms//os:windows": ["net462"] + PORTABLE_FRAMEWORKS,
            "//conditions:default": PORTABLE_FRAMEWORKS,
        }),
        directories = ["buildTransitive/{tfm}", "buildTransitive/{tfm}"],
        resource_directories = ["buildTransitive/_localization", "buildTransitive/_localization"],
        files = select({
            "@platforms//os:windows": adapter_files | windows_adapter_files,
            "//conditions:default": adapter_files,
        }),
        resolver_libraries = select({
            "@platforms//os:windows": [":framework_net462"],
            "//conditions:default": [],
        }),
        deps = [":trx_abstractions_package", ":telemetry_package", ":platform_msbuild_package"] + select({
            "@platforms//os:windows": [":framework_package"],
            "//conditions:default": [],
        }),
        nuget_deps = ["@paket.source//microsoft.testplatform.objectmodel"],
        netframework_nuget_deps = [
            "@paket.compatibility_mstest//system.memory",
            "@paket.compatibility_mstest//system.threading.tasks.extensions",
            "@paket.compatibility_mstest//system.reflection.metadata",
        ],
    )
    native.filegroup(
        name = "mstest_packages",
        srcs = [":framework_package", ":adapter_package", ":telemetry_package", ":platform_msbuild_package"],
    )
    native.filegroup(
        name = "mstest_package_closure",
        srcs = [":mstest_packages", ":packages", ":analyzers_package"],
    )

    platform_packages = []
    for flavor, package_id, framework in [
        ("uwp", "MSTest.Uwp", "net10.0-windows10.0.17763.0"),
        ("winui", "MSTest.WinUI", "net10.0-windows10.0.18362.0"),
    ]:
        name = "mstest_" + flavor + "_package"
        project = "src/TestFramework/" + package_id
        tfm = mstest_package_tfm(framework)
        files = {
            adapter_project + "/buildTransitive/common/MSTest.TestAdapter.targets": "buildTransitive/" + tfm + "/" + package_id + ".targets",
            adapter_project + "/buildTransitive/Parallelize.targets": "buildTransitive/" + tfm + "/Parallelize.targets",
            "src/TestFramework/PlatformPackage/MSTest.Platform.props": "buildTransitive/" + tfm + "/" + package_id + ".props",
        }
        _package(
            name = name,
            package_id = package_id,
            version_file = ":mstest_package_version",
            description = "Provides UI-thread test attributes and matching MSTest adapters for " + package_id + ".",
            readme = project + "/PACKAGE.md",
            libraries = [":mstest_" + flavor, ":adapter_" + flavor, ":platform_services_" + flavor],
            target_frameworks = [framework],
            directories = ["lib/{tfm}", "buildTransitive/{tfm}", "buildTransitive/{tfm}"],
            resource_directories = ["lib/{tfm}", "buildTransitive/{tfm}/_localization", "buildTransitive/{tfm}/_localization"],
            files = files,
            deps = [":framework_package", ":trx_abstractions_package", ":telemetry_package", ":platform_msbuild_package"],
            exact_core_dependency = True,
            nuget_deps = ["@paket.source//microsoft.testplatform.objectmodel"],
            target_compatible_with = ["@platforms//os:windows"],
        )
        platform_packages.append(":" + name)
    native.filegroup(
        name = "mstest_platform_package_closure",
        srcs = [":mstest_package_closure"] + platform_packages,
        target_compatible_with = ["@platforms//os:windows"],
    )
