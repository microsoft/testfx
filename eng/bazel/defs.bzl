"""Native TestFx build inputs that Arcade/MSBuild normally generates."""

load("@rules_dotnet//dotnet:defs.bzl", "csharp_binary", "csharp_library", "import_library")

PORTABLE_FRAMEWORKS = ["net10.0", "net11.0"]
STANDARD_FRAMEWORKS = PORTABLE_FRAMEWORKS + ["netstandard2.0"]
DESKTOP_FRAMEWORKS = PORTABLE_FRAMEWORKS + ["net462"]

TESTFX_PUBLIC_KEY_FILE = "@testfx_public_key//file"
_MICROSOFT_PUBLIC_KEY = "002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293"
_MOCK_PUBLIC_KEY = "0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7"

def testfx_friend_identities(names):
    """Match the repository's Microsoft and DynamicProxy friend identities."""
    return [
        name + ", PublicKey=" + (_MOCK_PUBLIC_KEY if name == "DynamicProxyGenAssembly2" else _MICROSOFT_PUBLIC_KEY)
        for name in names
    ]

def testfx_compatibility_select(compatibility, modern = []):
    """Select the exact legacy configurations after the assembly's TFM transition."""
    return select({
        "//eng/bazel/compatibility:netstandard2_0": compatibility,
        "//eng/bazel/compatibility:net462": compatibility,
        "//conditions:default": modern,
    })

def testfx_nuget(packages, compatibility_group = "compatibility"):
    """Keep compatibility-only package resolution separate from the modern lock."""
    return testfx_compatibility_select(
        ["@paket." + compatibility_group + "//" + package for package in packages],
        ["@paket.source//" + package for package in packages],
    )

def _matrix_transition_impl(_settings, attr):
    return {
        framework: {"@rules_dotnet//dotnet:target_framework": framework}
        for framework in attr.frameworks
    }

_matrix_transition = transition(
    implementation = _matrix_transition_impl,
    inputs = [],
    outputs = ["@rules_dotnet//dotnet:target_framework"],
)

def _matrix_impl(ctx):
    return [DefaultInfo(files = depset(transitive = [
        target[DefaultInfo].files
        for target in ctx.split_attr.target.values()
    ]))]

testfx_matrix = rule(
    implementation = _matrix_impl,
    attrs = {
        "target": attr.label(mandatory = True, cfg = _matrix_transition),
        "frameworks": attr.string_list(default = PORTABLE_FRAMEWORKS),
        "_allowlist_function_transition": attr.label(default = "@bazel_tools//tools/allowlists/function_transition_allowlist"),
    },
)

def _resx_impl(ctx):
    resources = ctx.actions.declare_file(ctx.label.name + "/" + ctx.attr.type_name + ".resources")
    source = ctx.actions.declare_file(ctx.label.name + "/" + ctx.attr.type_name + ".g.cs")
    ctx.actions.run(
        executable = ctx.executable._tool,
        arguments = ["resx", ctx.file.src.path, ctx.attr.type_name, resources.path, source.path],
        inputs = [ctx.file.src],
        outputs = [resources, source],
        mnemonic = "TestFxResx",
    )
    return [
        DefaultInfo(files = depset([resources, source])),
        OutputGroupInfo(sources = depset([source]), resources = depset([resources])),
    ]

testfx_resx = rule(
    implementation = _resx_impl,
    attrs = {
        "src": attr.label(allow_single_file = [".resx"], mandatory = True),
        "type_name": attr.string(mandatory = True),
        "_tool": attr.label(default = "//eng/bazel:build_tools", executable = True, cfg = "exec"),
    },
)

def _xlf_impl(ctx):
    output = ctx.actions.declare_file(ctx.label.name + "/" + ctx.attr.type_name + "." + ctx.attr.culture + ".resources")
    ctx.actions.run(
        executable = ctx.executable._tool,
        arguments = ["xlf", ctx.file.neutral.path, ctx.file.catalog.path, ctx.attr.culture, output.path],
        inputs = [ctx.file.neutral, ctx.file.catalog],
        outputs = [output],
        mnemonic = "TestFxXlf",
    )
    return [DefaultInfo(files = depset([output]))]

_testfx_xlf = rule(
    implementation = _xlf_impl,
    attrs = {
        "neutral": attr.label(allow_single_file = [".resx"], mandatory = True),
        "catalog": attr.label(allow_single_file = [".xlf"], mandatory = True),
        "type_name": attr.string(mandatory = True),
        "culture": attr.string(mandatory = True),
        "_tool": attr.label(default = "//eng/bazel:build_tools", executable = True, cfg = "exec"),
    },
)

def _satellite_impl(ctx):
    output = ctx.actions.declare_file(ctx.label.name + "/" + ctx.attr.culture + "/" + ctx.file.assembly.basename)
    ctx.actions.symlink(output = output, target_file = ctx.file.assembly)
    return [DefaultInfo(files = depset([output]))]

_satellite = rule(
    implementation = _satellite_impl,
    attrs = {
        "assembly": attr.label(allow_single_file = [".dll"], mandatory = True),
        "culture": attr.string(mandatory = True),
    },
)

def _embedded_impl(ctx):
    output = ctx.actions.declare_file(ctx.label.name + "/" + ctx.attr.logical_name)
    ctx.actions.symlink(output = output, target_file = ctx.file.src)
    return [DefaultInfo(files = depset([output]))]

_embedded = rule(
    implementation = _embedded_impl,
    attrs = {
        "src": attr.label(allow_single_file = True, mandatory = True),
        "logical_name": attr.string(mandatory = True),
    },
)

def _metadata_impl(ctx):
    source = ctx.actions.declare_file(ctx.label.name + ".g.cs")
    outputs = [source]
    inputs = [ctx.file.versions]
    if ctx.attr.build_time:
        inputs.append(ctx.version_file)
    template_output = None
    if ctx.file.template:
        template_output = ctx.actions.declare_file(ctx.label.name + ".BuildInfo.g.cs")
        inputs.append(ctx.file.template)
        outputs.append(template_output)
    ctx.actions.run(
        executable = ctx.executable._tool,
        arguments = [
            "metadata",
            ctx.file.versions.path,
            ctx.attr.version_property,
            source.path,
            ctx.file.template.path if ctx.file.template else "-",
            template_output.path if template_output else "-",
            ";".join(ctx.attr.usings) or "-",
            ctx.attr.version_override or "-",
            ctx.attr.culture or "-",
            ctx.attr.assembly_version or "-",
            ctx.version_file.path if ctx.attr.build_time else "-",
        ],
        inputs = inputs,
        outputs = outputs,
        mnemonic = "TestFxMetadata",
    )
    return [DefaultInfo(files = depset(outputs))]

testfx_metadata = rule(
    implementation = _metadata_impl,
    attrs = {
        "versions": attr.label(default = "//:eng/Versions.props", allow_single_file = True),
        "version_property": attr.string(default = "VersionPrefix"),
        "version_override": attr.string(),
        "culture": attr.string(),
        "assembly_version": attr.string(),
        "build_time": attr.bool(),
        "template": attr.label(allow_single_file = True),
        "usings": attr.string_list(),
        "_tool": attr.label(default = "//eng/bazel:build_tools", executable = True, cfg = "exec"),
    },
)

def testfx_library(
        name,
        project,
        assembly_name = None,
        linked_srcs = [],
        exclude = [],
        resources = {},
        embedded_resources = {},
        build_info = None,
        usings = [],
        defines = [],
        deps = [],
        internals_visible_to = [],
        polyfills = True,
        version_override = "",
        version_property = None,
        assembly_version = "",
        target_frameworks = PORTABLE_FRAMEWORKS,
        analyzer_configs = [],
        compiler_options = [],
        binary = False,
        **kwargs):
    """Compile one source assembly without invoking or consuming MSBuild outputs."""
    testfx_metadata(
        name = name + "_metadata",
        version_property = version_property or ("TestingPlatformVersionPrefix" if project.startswith("src/Platform/") else "VersionPrefix"),
        version_override = version_override,
        assembly_version = assembly_version,
        template = build_info,
        usings = usings,
        build_time = project == "src/Platform/Microsoft.Testing.Platform",
    )
    sources = native.glob(
        [project + "/**/*.cs"],
        exclude = [project + "/bin/**", project + "/obj/**"] + exclude,
    )
    generated_sources = ["//eng/bazel:GlobalUsings.cs", ":" + name + "_metadata"]
    if polyfills:
        generated_sources += native.glob(["src/Polyfills/**/*.cs"])
    embedded = []
    localized = {}
    for index, (path, type_name) in enumerate(resources.items()):
        resource_name = name + "_resx_" + str(index)
        testfx_resx(name = resource_name, src = path, type_name = type_name)
        native.filegroup(name = resource_name + "_sources", srcs = [":" + resource_name], output_group = "sources")
        native.filegroup(name = resource_name + "_resources", srcs = [":" + resource_name], output_group = "resources")
        generated_sources.append(":" + resource_name + "_sources")
        embedded.append(":" + resource_name + "_resources")
        directory, filename = path.rsplit("/", 1)
        stem = filename.removesuffix(".resx")
        for catalog in native.glob([directory + "/xlf/" + stem + ".*.xlf"], allow_empty = True):
            culture = catalog.rsplit("/", 1)[-1][len(stem) + 1:-4]
            translated_name = resource_name + "_" + culture
            _testfx_xlf(
                name = translated_name,
                neutral = path,
                catalog = catalog,
                type_name = type_name,
                culture = culture,
            )
            if culture not in localized:
                localized[culture] = []
            localized[culture].append(":" + translated_name)

    satellites = []
    for culture, translated_resources in localized.items():
        satellite_name = name + "_satellite_" + culture
        testfx_metadata(
            name = satellite_name + "_metadata",
            version_property = version_property or ("TestingPlatformVersionPrefix" if project.startswith("src/Platform/") else "VersionPrefix"),
            version_override = version_override,
            assembly_version = assembly_version,
            culture = culture,
        )
        csharp_library(
            name = satellite_name + "_dll",
            srcs = [":" + satellite_name + "_metadata"],
            out = (assembly_name or project.split("/")[-1]) + ".resources",
            target_frameworks = target_frameworks,
            resources = translated_resources,
            keyfile = TESTFX_PUBLIC_KEY_FILE,
            compiler_options = ["/publicsign+"],
            generate_documentation_file = False,
        )
        _satellite(name = satellite_name, assembly = ":" + satellite_name + "_dll", culture = culture)
        satellites.append(":" + satellite_name)

    for index, (path, logical_name) in enumerate(embedded_resources.items()):
        embedded_name = name + "_embedded_" + str(index)
        _embedded(name = embedded_name, src = path, logical_name = logical_name)
        embedded.append(":" + embedded_name)

    if project == "src/Platform/Microsoft.Testing.Platform" and deps:
        fail("Microsoft.Testing.Platform must remain dependency-free.")

    assembly_rule = csharp_binary if binary else csharp_library
    assembly_rule(
        name = name,
        srcs = sources + linked_srcs + generated_sources,
        resources = embedded,
        resource_assemblies = satellites,
        out = assembly_name or project.split("/")[-1],
        target_frameworks = target_frameworks,
        langversion = "preview",
        nullable = "enable",
        defines = ["TRACE"] + defines,
        deps = deps,
        internals_visible_to = testfx_friend_identities(internals_visible_to),
        keyfile = TESTFX_PUBLIC_KEY_FILE,
        compiler_options = ["/publicsign+"] + compiler_options,
        analyzer_configs = ["//:.editorconfig"] + analyzer_configs,
        nowarn = ["CS1701", "CS1702", "TPEXP", "TPINTERNAL", "MSTESTOBS"],
        treat_warnings_as_errors = True,
        **kwargs
    )

def testfx_binary(**kwargs):
    """Build a native executable with the same source/metadata rules as libraries."""
    testfx_library(binary = True, **kwargs)

def _test_impl(ctx):
    info = ctx.attr.binary[DefaultInfo]
    executable = ctx.actions.declare_file(ctx.label.name + "." + info.files_to_run.executable.extension)
    ctx.actions.symlink(output = executable, target_file = info.files_to_run.executable, is_executable = True)
    providers = [DefaultInfo(executable = executable, runfiles = info.default_runfiles)]
    if RunEnvironmentInfo in ctx.attr.binary:
        providers.append(ctx.attr.binary[RunEnvironmentInfo])
    return providers

testfx_test = rule(
    implementation = _test_impl,
    test = True,
    attrs = {"binary": attr.label(executable = True, cfg = "target", mandatory = True)},
)

def _assembly_dll_impl(ctx):
    info = ctx.attr.assembly[DefaultInfo]
    dlls = [file for file in info.files.to_list() if file.extension == "dll"]
    if len(dlls) != 1:
        fail("Expected one implementation DLL for " + str(ctx.attr.assembly.label))
    return [DefaultInfo(files = depset(dlls), runfiles = info.default_runfiles)]

_assembly_dll = rule(
    implementation = _assembly_dll_impl,
    attrs = {"assembly": attr.label(mandatory = True)},
)

def testfx_analyzer(name, assembly, library_name = "MSTest.Analyzers"):
    """Use a compiled analyzer both as a normal test dependency and a compiler plugin."""
    _assembly_dll(name = name + "_dll", assembly = assembly)
    import_library(
        name = name,
        library_name = library_name,
        version = "1.0.0",
        analyzers = [":" + name + "_dll"],
    )
