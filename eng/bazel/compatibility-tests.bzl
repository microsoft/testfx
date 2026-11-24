"""Analysis tests for package-reference selection using the actual patched rules."""

load("@rules_dotnet//dotnet/private:common.bzl", "collect_compile_info")
load("@rules_dotnet//dotnet/private:providers.bzl", "DotnetAssemblyCompileInfo", "DotnetAssemblyRuntimeInfo", "NuGetInfo")

def _file(owner, path):
    return struct(owner = owner, path = path, basename = path.rsplit("/", 1)[-1])

def _dependency(reference, version, package = True, dependencies = []):
    compile_info = DotnetAssemblyCompileInfo(
        name = reference.basename.removesuffix(".dll"),
        version = version,
        refs = [reference],
        irefs = [reference],
        internals_visible_to = [],
        exports = [],
        analyzers = [],
        analyzers_csharp = [],
        analyzers_fsharp = [],
        analyzers_vb = [],
        compile_data = [],
        transitive_refs = depset(transitive = [
            depset(dependency[DotnetAssemblyCompileInfo].refs, transitive = [dependency[DotnetAssemblyCompileInfo].transitive_refs])
            for dependency in dependencies
        ]),
        transitive_compile_data = depset(),
        transitive_analyzers = depset(),
        transitive_analyzers_csharp = depset(),
        transitive_analyzers_fsharp = depset(),
        transitive_analyzers_vb = depset(),
    )
    runtime_info = DotnetAssemblyRuntimeInfo(
        name = compile_info.name,
        version = version,
        libs = (reference,),
        nuget_info = NuGetInfo(nupkg = _file(reference.owner, "package.nupkg")) if package else None,
        deps = depset(
            [dependency[DotnetAssemblyRuntimeInfo] for dependency in dependencies],
            transitive = [dependency[DotnetAssemblyRuntimeInfo].deps for dependency in dependencies],
        ),
    )
    return {DotnetAssemblyCompileInfo: compile_info, DotnetAssemblyRuntimeInfo: runtime_info}

def _reference_selection_test_impl(ctx):
    older = _file(Label("@@older_package//:asset"), "older/Shared.dll")
    newer = _file(Label("@@newer_package//:asset"), "newer/Shared.dll")
    native = _file(Label("//:source"), "native/Shared.dll")
    alternate = _file(newer.owner, "alternate-tfm/Shared.dll")
    intermediate = _file(Label("//:intermediate"), "Intermediate.dll")

    if ctx.attr.scenario == "independent":
        dependencies = [_dependency(older, "8.0.0")]
        expected = [older.path]
    elif ctx.attr.scenario == "newer_package":
        dependencies = [_dependency(intermediate, "1.0.0", package = False, dependencies = [
            _dependency(older, "8.0.0"),
            _dependency(newer, "10.0.0"),
        ])]
        expected = [newer.path]
    elif ctx.attr.scenario == "native":
        dependencies = [_dependency(native, "1.0.0", package = False), _dependency(newer, "10.0.0")]
        expected = [native.path, newer.path]
    else:
        dependencies = [
            _dependency(newer, "10.0.0"),
            _dependency(intermediate, "1.0.0", package = False, dependencies = [_dependency(alternate, "10.0.0")]),
        ]
        expected = [newer.path]

    refs = collect_compile_info("Consumer", dependencies, None, [], False)[0]
    actual = sorted([reference.path for reference in refs.to_list() if reference.basename == "Shared.dll"])
    return [AnalysisTestResultInfo(
        success = actual == sorted(expected),
        message = "{}: expected {}, got {}".format(ctx.attr.scenario, sorted(expected), actual),
    )]

_reference_selection_test = rule(
    implementation = _reference_selection_test_impl,
    attrs = {"scenario": attr.string(mandatory = True, values = ["independent", "newer_package", "native", "direct_tfm"])},
    analysis_test = True,
    test = True,
)

def compatibility_reference_tests():
    tests = []
    for scenario in ["independent", "newer_package", "native", "direct_tfm"]:
        name = "compatibility_reference_" + scenario + "_test"
        _reference_selection_test(name = name, scenario = scenario)
        tests.append(":" + name)
    native.test_suite(name = "compatibility_reference_tests", tests = tests)
