"""Configured NuGet asset-selection tests for the Windows compatibility graph."""

load("@rules_dotnet//dotnet/private/rules/nuget:nuget_archive.bzl", "tfm_filegroup")
load("@rules_dotnet//dotnet/private/transitions:common.bzl", "FRAMEWORK_COMPATABILITY_TRANSITION_OUTPUTS")

def _configuration(framework):
    return analysis_test_transition(settings = {
        "@rules_dotnet" + key: value
        for key, value in FRAMEWORK_COMPATABILITY_TRANSITION_OUTPUTS[framework].items()
    } | {
        "@rules_dotnet//dotnet:target_framework": framework,
        "//command_line_option:allow_analysis_failures": "True",
    })

def _asset_selection_impl(ctx):
    failures = []
    for name, target, expected in [
        ("NewerRuntimeWinsWithoutAmbiguity", ctx.attr.windows8[0], ["LICENSE"]),
        ("PortableDoesNotConsumeWindows", ctx.attr.portable8[0], ["LICENSE"]),
        ("OlderWindowsAssetSelected", ctx.attr.windows6[0], [".bazelversion"]),
        ("IncompatibleWindowsAssetIsNotUsed", ctx.attr.portable6[0], []),
    ]:
        if AnalysisFailureInfo in target:
            failures.append(name + ": " + "\n".join([cause.message for cause in target[AnalysisFailureInfo].causes.to_list()]))
        else:
            actual = [file.basename for file in target[DefaultInfo].files.to_list()]
            if actual != expected:
                failures.append("{}: expected {}, actual {}".format(name, expected, actual))
    return [AnalysisTestResultInfo(
        success = not failures,
        message = "\n".join(failures) if failures else "Four configured Windows asset-selection assertions passed.",
    )]

_windows_asset_selection_test = rule(
    implementation = _asset_selection_impl,
    analysis_test = True,
    attrs = {
        "windows8": attr.label(default = "//:_windows_asset_fixture", cfg = _configuration("net8.0-windows10.0.18362.0")),
        "portable8": attr.label(default = "//:_windows_asset_fixture", cfg = _configuration("net8.0")),
        "windows6": attr.label(default = "//:_windows_asset_fixture", cfg = _configuration("net6.0-windows10.0.18362.0")),
        "portable6": attr.label(default = "//:_windows_asset_fixture", cfg = _configuration("net6.0")),
    },
)

def windows_asset_selection_tests():
    # Existing read-only files are markers, never compiler inputs in this test.
    tfm_filegroup(
        name = "_windows_asset_fixture",
        default_to_empty = True,
        tfms = {
            "net6.0-windows10.0.17763.0": ["//:.bazelversion"],
            "net8.0": ["//:LICENSE"],
        },
    )
    _windows_asset_selection_test(name = "windows_asset_selection_test", size = "small")
