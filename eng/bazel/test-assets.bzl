"""Stage declared fixtures beside the actual DLL, not just in its runfiles tree."""

load("@rules_dotnet//dotnet/private:providers.bzl", "DotnetAssemblyRuntimeInfo", "DotnetBinaryInfo")
load("@rules_dotnet//dotnet/private/transitions:tfm_transition.bzl", "tfm_transition")
load(":defs.bzl", "PORTABLE_FRAMEWORKS", "TEST_LAUNCHER_UNIX_PROLOGUE", "TEST_LAUNCHER_WINDOWS_PROLOGUE")

def _fixture_app_impl(ctx):
    binary = ctx.attr.binary
    info = binary[DefaultInfo]
    dll = binary[DotnetBinaryInfo].dll
    directory = dll.short_path.rsplit("/", 1)[0]
    if ctx.label.package:
        directory = directory[len(ctx.label.package) + 1:]
    outputs = []
    if ctx.attr.repository_marker:
        marker = ctx.actions.declare_file(directory + "/.git")
        if marker.dirname != dll.dirname:
            fail("Fixture and binary configurations must match: %s != %s" % (marker.dirname, dll.dirname))
        ctx.actions.write(marker, "Native Bazel test fixture root.\n")
        outputs.append(marker)

    for target, destination in ctx.attr.files.items():
        sources = target[DotnetAssemblyRuntimeInfo].libs if DotnetAssemblyRuntimeInfo in target else target[DefaultInfo].files.to_list()
        if len(sources) != 1:
            fail("Expected one runtime fixture from %s, found %s" % (target.label, len(sources)))
        output = ctx.actions.declare_file(directory + "/" + destination)
        if output.path != dll.dirname + "/" + destination:
            fail("Fixture and binary configurations must match: %s" % output.path)
        ctx.actions.symlink(output = output, target_file = sources[0])
        outputs.append(output)

    executable = ctx.actions.declare_file(ctx.label.name + "." + info.files_to_run.executable.extension)
    ctx.actions.symlink(output = executable, target_file = info.files_to_run.executable, is_executable = True)
    providers = [
        DefaultInfo(
            executable = executable,
            files = depset(outputs + [executable], transitive = [info.files]),
            runfiles = info.default_runfiles.merge(ctx.runfiles(files = outputs)),
        ),
    ]
    if RunEnvironmentInfo in binary:
        providers.append(binary[RunEnvironmentInfo])
    return providers

testfx_fixture_app = rule(
    implementation = _fixture_app_impl,
    executable = True,
    cfg = tfm_transition,
    attrs = {
        "binary": attr.label(executable = True, cfg = "target", mandatory = True, providers = [DotnetBinaryInfo]),
        "files": attr.label_keyed_string_dict(allow_files = True),
        "target_frameworks": attr.string_list(default = PORTABLE_FRAMEWORKS),
        "project_sdk": attr.string(default = "default"),
        "repository_marker": attr.bool(default = False),
        "_allowlist_function_transition": attr.label(default = "@bazel_tools//tools/allowlists/function_transition_allowlist"),
    },
)

def _published_test_impl(ctx):
    info = ctx.attr.binary[DefaultInfo]
    application = info.files_to_run.executable
    path = application.short_path
    runfile = path[3:] if path.startswith("../") else ctx.workspace_name + "/" + path
    runtime = ctx.toolchains["@rules_dotnet//dotnet:toolchain_type"].dotnetinfo
    if not runtime.runtime_path.startswith("external/"):
        fail("Published tests require the declared downloadable .NET toolchain.")
    runtime_directory = runtime.runtime_path[len("external/"):].rsplit("/", 1)[0]
    directory = path.rsplit("/", 1)[0]
    fixtures = []
    if ctx.attr.repository_marker:
        marker = ctx.actions.declare_file(directory + "/.git")
        ctx.actions.write(marker, "Native Bazel test fixture root.\n")
        fixtures.append(marker)
    for target, destination in ctx.attr.files.items():
        sources = target[DefaultInfo].files.to_list()
        if len(sources) != 1:
            fail("Expected one published test fixture from %s" % target.label)
        output = ctx.actions.declare_file(directory + "/" + destination)
        ctx.actions.symlink(output = output, target_file = sources[0])
        fixtures.append(output)
    is_windows = application.extension == "exe"
    launcher = ctx.actions.declare_file(ctx.label.name + (".bat" if is_windows else ".sh"))
    runner = runfile
    arguments = ""
    extra_runfiles = ctx.runfiles()
    tool_variables = []
    for target, variable in ctx.attr.tools.items():
        tool = target[DefaultInfo]
        tool_executable = tool.files_to_run.executable
        if not tool_executable:
            fail("Expected an executable tool: %s" % target.label)
        # Only Windows apphosts carry an extension; dotted assembly names make File.extension unreliable elsewhere.
        main_dll = (tool_executable.path[:-len(".exe")] if tool_executable.extension == "exe" else tool_executable.path) + ".dll"
        dlls = [file for file in tool.files.to_list() if file.path == main_dll]
        if len(dlls) != 1:
            fail("Expected the framework-dependent main DLL beside %s" % tool_executable.path)
        tool_path = dlls[0].short_path
        tool_variables.append((variable, tool_path[3:] if tool_path.startswith("../") else ctx.workspace_name + "/" + tool_path))
        extra_runfiles = extra_runfiles.merge(tool.default_runfiles).merge(ctx.runfiles(transitive_files = tool.files))
    if ctx.attr.writable_layout:
        tool = ctx.attr._writable_launcher[DefaultInfo]
        tool_path = tool.files_to_run.executable.short_path
        runner = tool_path[3:] if tool_path.startswith("../") else ctx.workspace_name + "/" + tool_path
        extra_runfiles = extra_runfiles.merge(tool.default_runfiles).merge(ctx.runfiles(transitive_files = tool.files))
        arguments = (' "%TEST_SRCDIR%\\' + runtime_directory.replace("/", "\\") + '" "' + runtime.sdk_version + '" "%TEST_SRCDIR%\\' + runfile.replace("/", "\\") + '"') if is_windows else (
            ' "${TEST_SRCDIR}/' + runtime_directory + '" "' + runtime.sdk_version + '" "${TEST_SRCDIR}/' + runfile + '"'
        )
    if is_windows:
        content = "\r\n".join([
            "@echo off",
            "setlocal DisableDelayedExpansion",
        ] + TEST_LAUNCHER_WINDOWS_PROLOGUE + [
            'set "DOTNET_ROOT=%TEST_SRCDIR%\\' + runtime_directory.replace("/", "\\") + '"',
            'set "PATH=%DOTNET_ROOT%;%PATH%"',
        ] + [
            'set "' + variable + "=%TEST_SRCDIR%\\" + location.replace("/", "\\") + '"'
            for variable, location in tool_variables
        ] + [
            ('call ' if ctx.attr.writable_layout else "") + '"%TEST_SRCDIR%\\' + runner.replace("/", "\\") + '"' + arguments + ' %*',
            "exit /b %ERRORLEVEL%",
            "",
        ])
    else:
        # Do not exec: the prologue's EXIT trap removes the pipe directory; errexit keeps the exit code.
        content = "\n".join([
            "#!/usr/bin/env bash",
            "set -euo pipefail",
        ] + TEST_LAUNCHER_UNIX_PROLOGUE + [
            'export DOTNET_ROOT="${TEST_SRCDIR}/' + runtime_directory + '"',
            'export PATH="${DOTNET_ROOT}:${PATH}"',
        ] + [
            "export " + variable + '="${TEST_SRCDIR}/' + location + '"'
            for variable, location in tool_variables
        ] + [
            '"${TEST_SRCDIR}/' + runner + '"' + arguments + ' "$@"',
            "",
        ])
    ctx.actions.write(launcher, content, is_executable = True)
    return [
        DefaultInfo(
            executable = launcher,
            runfiles = info.default_runfiles.merge(extra_runfiles).merge(ctx.runfiles(files = fixtures, transitive_files = depset(transitive = [info.files, runtime.runtime_files]))),
        ),
        RunEnvironmentInfo(environment = {}, inherited_environment = ctx.attr.env_inherit),
    ]

testfx_published_test = rule(
    implementation = _published_test_impl,
    test = True,
    toolchains = ["@rules_dotnet//dotnet:toolchain_type"],
    attrs = {
        "binary": attr.label(executable = True, cfg = "target", mandatory = True),
        "files": attr.label_keyed_string_dict(allow_files = True),
        "repository_marker": attr.bool(default = False),
        "env_inherit": attr.string_list(),
        # Framework-dependent tools; each value names the variable that receives the absolute main DLL path.
        "tools": attr.label_keyed_string_dict(cfg = "target"),
        "writable_layout": attr.bool(default = False),
        "_writable_launcher": attr.label(default = "//eng/bazel:writable_test_launcher", executable = True, cfg = "exec"),
    },
)
