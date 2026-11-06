"""Assembly-free MSTest SDK and portable meta-packages, using the pinned native packer."""

load("@rules_dotnet//dotnet/private:providers.bzl", "NuGetPackInfo")
load(":defs.bzl", "PORTABLE_FRAMEWORKS", "testfx_binary", "testfx_test")

_SDK = "src/Package/MSTest.Sdk/"

def _sdk_package_impl(ctx):
    directory = ctx.label.name + "/"
    archive = ctx.actions.declare_file(directory + ctx.label.name + ".nupkg")
    nuspec = ctx.actions.declare_file(directory + ctx.label.name + ".nuspec")
    version = ctx.actions.declare_file(directory + "version.txt")
    request = ctx.actions.declare_file(directory + "pack.json")
    inputs = [
        ctx.file.project,
        ctx.file.versions,
        ctx.file.repository_properties,
        ctx.file.source_properties,
        ctx.file.central_packages,
        ctx.file.readme,
    ]
    files = [struct(source = ctx.file.readme.path, target = "PACKAGE.md")]
    templates = []
    generated = []

    if ctx.attr.package_id == "MSTest.Sdk":
        for file in ctx.files.sdk_assets:
            target = file.short_path[len(_SDK):]
            inputs.append(file)
            if target.endswith(".template"):
                target = target[:-len(".template")]
                output = ctx.actions.declare_file(directory + target)
                templates.append(struct(source = file.path, output = output.path))
                generated.append(output)
                files.append(struct(source = output.path, target = target))
            else:
                files.append(struct(source = file.path, target = target))
    else:
        inputs.append(ctx.file.placeholder)
        files += [
            struct(source = ctx.file.placeholder.path, target = "lib/" + framework + "/_._")
            for framework in PORTABLE_FRAMEWORKS
        ]

    configuration = ctx.actions.declare_file(directory + "inputs.json")
    ctx.actions.write(configuration, json.encode(struct(
        packageId = ctx.attr.package_id,
        project = ctx.file.project.path,
        versions = ctx.file.versions.path,
        repositoryProperties = ctx.file.repository_properties.path,
        sourceProperties = ctx.file.source_properties.path,
        centralPackages = ctx.file.central_packages.path,
        frameworks = PORTABLE_FRAMEWORKS,
        templates = templates,
        files = files,
        output = archive.path,
        nuspecOutput = nuspec.path,
        versionFile = version.path,
        requestOutput = request.path,
    )))
    ctx.actions.run(
        executable = ctx.executable._generator,
        arguments = [configuration.path],
        inputs = [configuration] + inputs,
        outputs = [version, request] + generated,
        mnemonic = "TestFxSdkPackageInputs",
    )
    ctx.actions.run(
        executable = ctx.attr._packer[DefaultInfo].files_to_run,
        arguments = [request.path],
        inputs = [request, version] + inputs + generated,
        outputs = [archive, nuspec],
        mnemonic = "TestFxSdkNuGetPack",
    )
    return [
        DefaultInfo(files = depset([archive])),
        OutputGroupInfo(
            nuspec = depset([nuspec]),
            templates = depset(generated),
            request = depset([request]),
            version = depset([version]),
        ),
        NuGetPackInfo(
            label = ctx.label,
            package_id = ctx.attr.package_id,
            version = None,
            version_file = version,
            nupkg = archive,
            snupkg = None,
            target_frameworks = PORTABLE_FRAMEWORKS if ctx.attr.package_id == "MSTest" else [],
            bundled_assemblies = [],
            is_tool = False,
        ),
    ]

_sdk_package = rule(
    implementation = _sdk_package_impl,
    attrs = {
        "package_id": attr.string(mandatory = True, values = ["MSTest", "MSTest.Sdk"]),
        "project": attr.label(allow_single_file = True, mandatory = True),
        "readme": attr.label(allow_single_file = True, mandatory = True),
        "sdk_assets": attr.label_list(allow_files = True),
        "placeholder": attr.label(allow_single_file = True),
        "versions": attr.label(default = "//:eng/Versions.props", allow_single_file = True),
        "repository_properties": attr.label(default = "//:Directory.Build.props", allow_single_file = True),
        "source_properties": attr.label(default = "//:src/Directory.Build.props", allow_single_file = True),
        "central_packages": attr.label(default = "//:Directory.Packages.props", allow_single_file = True),
        "_generator": attr.label(default = "//eng/bazel:sdk_package_tool", executable = True, cfg = "exec"),
        "_packer": attr.label(default = "@rules_dotnet//dotnet/private/tools/nuget_pack:nuget_pack", executable = True, cfg = "exec"),
    },
)

def sdk_packages():
    _sdk_package(
        name = "mstest_sdk_package",
        package_id = "MSTest.Sdk",
        project = _SDK + "MSTest.Sdk.csproj",
        readme = _SDK + "PACKAGE.md",
        sdk_assets = native.glob(
            [_SDK + "Sdk/**/*.targets", _SDK + "Sdk/**/*.props", _SDK + "Sdk/**/*.template"],
            exclude = [_SDK + "Sdk/Sdk.props", _SDK + "Sdk/Runner/Runner.targets"],
            allow_empty = True,
        ),
    )
    _sdk_package(
        name = "mstest_package",
        package_id = "MSTest",
        project = "src/Package/MSTest/MSTest.csproj",
        readme = "src/Package/MSTest/PACKAGE.md",
        placeholder = "src/Package/MSTest/_._",
    )
    testfx_binary(
        name = "sdk_package_tests_app",
        project = "test/UnitTests/Bazel.SdkPackages.UnitTests",
        polyfills = False,
        linked_srcs = ["//eng/bazel:TestMain.cs"],
        defines = ["TESTFX_METHOD_PARALLEL"],
        generate_documentation_file = False,
        data = ["//eng/bazel:sdk_package_tool"],
        envs = {"TESTFX_BAZEL_SDK_PACKAGE_TOOL": "$(rlocationpath //eng/bazel:sdk_package_tool)"},
        deps = [":adapter", ":framework", ":framework_extensions", ":trx", ":mstest_analyzer_plugin", "//eng/bazel:runfiles"],
    )
    testfx_test(
        name = "sdk_package_tests",
        binary = ":sdk_package_tests_app",
        args = ["--minimum-expected-tests", "12"],
        size = "medium",
    )
