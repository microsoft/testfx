"""Native, experimental portable packages built from this checkout."""

load("@rules_dotnet//dotnet:defs.bzl", "csharp_binary", "nuget_pack", "publish_binary")
load(":defs.bzl", "PORTABLE_FRAMEWORKS", "testfx_matrix")

def _version_impl(ctx):
    output = ctx.actions.declare_file(ctx.label.name + ".txt")
    ctx.actions.run(
        executable = ctx.executable._tool,
        arguments = ["version", ctx.file.versions.path, ctx.attr.property_name, output.path],
        inputs = [ctx.file.versions],
        outputs = [output],
        mnemonic = "TestFxPackageVersion",
    )
    return [DefaultInfo(files = depset([output]))]

_package_version = rule(
    implementation = _version_impl,
    attrs = {
        "versions": attr.label(default = "//:eng/Versions.props", allow_single_file = True),
        "property_name": attr.string(mandatory = True),
        "_tool": attr.label(default = "//eng/bazel:build_tools", executable = True, cfg = "exec"),
    },
)

def _build_assets(name, project):
    files = {}
    for directory in ["build", "buildTransitive", "buildMultiTargeting"]:
        prefix = project + "/" + directory + "/"
        for index, path in enumerate(native.glob([prefix + "**"], allow_empty = True)):
            relative_path = path[len(prefix):]
            frameworks = [""] if directory == "buildMultiTargeting" else PORTABLE_FRAMEWORKS
            for framework in frameworks:
                alias = "{}_{}_{}_{}".format(name, directory, framework.replace(".", "_"), index)
                native.filegroup(name = alias, srcs = [path])
                files[":" + alias] = directory + "/" + (framework + "/" if framework else "") + relative_path
    return files

package_version = _package_version
package_build_assets = _build_assets

def native_packages():
    _package_version(
        name = "platform_package_version",
        property_name = "TestingPlatformVersionPrefix",
    )
    packages = []
    for name, product, dependencies, description in [
        ("platform", "Microsoft.Testing.Platform", [], "Runs .NET tests through the lightweight Microsoft Testing Platform host."),
        ("trx_abstractions", "Microsoft.Testing.Extensions.TrxReport.Abstractions", ["platform"], "Provides the test-result abstractions used by Microsoft Testing Platform TRX reporters."),
        ("trx", "Microsoft.Testing.Extensions.TrxReport", ["platform", "trx_abstractions"], "Generates TRX test reports from Microsoft Testing Platform test runs."),
    ]:
        project = "src/Platform/" + product
        package = name + "_package"
        nuget_pack(
            name = package,
            library = ":" + name,
            package_id = product,
            version_file = ":platform_package_version",
            target_frameworks = PORTABLE_FRAMEWORKS,
            deps = [":" + dependency + "_package" for dependency in dependencies],
            first_party_deps = "error",
            authors = ["Microsoft"],
            description = description + " Experimental native Bazel build; not an official release package.",
            license_expression = "MIT",
            readme = project + "/PACKAGE.md",
            repository_url = "https://github.com/microsoft/testfx",
            files = _build_assets(package, project),
            reference_assemblies = False,
            symbols = "embedded",
        )
        packages.append(":" + package)
    native.filegroup(name = "packages", srcs = packages)

    csharp_binary(
        name = "runtime_explorer",
        srcs = [
            "//samples/public/TestingPlatformExamples/TestingPlatformExplorer:sources",
            "//eng/bazel:RuntimeProvenance.cs",
        ],
        out = "TestingPlatformExplorer",
        target_frameworks = PORTABLE_FRAMEWORKS,
        appsetting_files = ["//samples/public/TestingPlatformExamples/TestingPlatformExplorer:TestingPlatformExplorer.testingplatformconfig.json"],
        generate_documentation_file = False,
        langversion = "preview",
        nullable = "enable",
        deps = [":platform", ":trx", ":trx_abstractions"],
    )
    published = []
    for framework in PORTABLE_FRAMEWORKS:
        name = "explorer_" + framework.replace(".", "_")
        publish_binary(
            name = name,
            binary = ":runtime_explorer",
            target_framework = framework,
            self_contained = True,
        )
        published.append(":" + name)
    native.filegroup(name = "_published_samples", srcs = published)
    testfx_matrix(
        name = "published_samples",
        target = ":_published_samples",
        frameworks = ["net11.0"],
    )
