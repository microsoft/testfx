"""Executable analysis tests for the native Windows targeting contract."""

load(
    "@rules_dotnet//dotnet/private:common.bzl",
    "FRAMEWORK_COMPATIBILITY",
    "framework_preprocessor_symbols",
    "generate_runtimeconfig",
    "get_highest_compatible_target_framework",
    "get_nearest_compatible_target_framework",
    "tfm_to_semver",
)
load(
    "@rules_dotnet//dotnet/private/sdk:packs.bzl",
    "APPHOST_PACK_LOOKUP_TABLE",
    "RUNTIME_PACK_LOOKUP_TABLE",
    "TARGETING_PACK_LOOKUP_TABLE",
)

def _targeting_tests_impl(_ctx):
    target = "net8.0-windows10.0.19041.0"
    symbols = framework_preprocessor_symbols(target)
    runtime = generate_runtimeconfig(target, "default", False, "LatestMajor", framework_version = "8.0.0")
    desktop = generate_runtimeconfig("net8.0-windows7.0", "windowsdesktop", False, "LatestMajor", framework_version = "8.0.0")
    cases = [
        ("PortableCannotConsumeWindows", None, get_highest_compatible_target_framework("net8.0", [target])),
        ("NewerPortableCannotConsumeOlderWindows", None, get_highest_compatible_target_framework("net9.0", [target])),
        ("OlderWindowsCannotConsumeNewerWindows", None, get_highest_compatible_target_framework("net8.0-windows10.0.17763.0", [target])),
        ("RuntimeUpgradeCannotRaiseWindowsMinimum", None, get_highest_compatible_target_framework("net9.0-windows10.0.17763.0", [target])),
        ("WindowsSelectsClosestPlatform", "net8.0-windows10.0.17763.0", get_nearest_compatible_target_framework(target, ["net8.0", "net8.0-windows10.0.17763.0"])),
        ("SameRuntimeBeatsOlderWindowsRuntime", "net9.0", get_nearest_compatible_target_framework("net9.0-windows10.0.19041.0", ["net9.0", target])),
        ("NewRuntimeStillHonorsWindowsMinimum", "net8.0", get_nearest_compatible_target_framework("net9.0-windows10.0.17763.0", ["net8.0", target])),
        ("ExactWindowsMatch", target, get_highest_compatible_target_framework(target, ["net8.0", target])),
        ("WindowsRuntimeVersionIsPortable", "8.0.0", tfm_to_semver(target)),
        ("CoreSymbolsPreserved", True, all([symbol in symbols for symbol in ["NET", "NETCOREAPP", "NET8_0", "NET8_0_OR_GREATER"]])),
        ("WindowsSymbolsPresent", True, all([symbol in symbols for symbol in ["WINDOWS", "WINDOWS10_0_19041_0", "WINDOWS10_0_17763_0_OR_GREATER"]])),
        ("FutureSymbolsAbsent", False, any([symbol in symbols for symbol in ["NET9_0_OR_GREATER", "WINDOWS10_0_22621_0_OR_GREATER"]])),
        ("CsWinRT3IsNotSilentlyAliased", False, "net10.0-windows10.0.19041.1" in FRAMEWORK_COMPATIBILITY),
        ("CoreReferencePackReused", TARGETING_PACK_LOOKUP_TABLE["user"]["default"]["net8.0"], TARGETING_PACK_LOOKUP_TABLE["user"]["default"][target]),
        ("WindowsApphostHasNoLinuxRid", False, "linux-x64" in APPHOST_PACK_LOOKUP_TABLE[target]),
        ("WindowsRuntimeHasNoLinuxRid", False, "linux-x64" in RUNTIME_PACK_LOOKUP_TABLE["default"][target]),
        ("DesktopLegacyArm64IsNotAdvertised", ["win-x64"], RUNTIME_PACK_LOOKUP_TABLE["windowsdesktop"]["netcoreapp3.1"].keys()),
        ("WindowsApphostUsesMatchingCoreBand", APPHOST_PACK_LOOKUP_TABLE["net8.0"]["win-x64"], APPHOST_PACK_LOOKUP_TABLE[target]["win-x64"]),
        ("RuntimeConfigUsesBaseTfm", "net8.0", runtime["runtimeOptions"]["tfm"]),
        ("RuntimeConfigPreservesRollForward", "LatestMajor", runtime["runtimeOptions"]["rollForward"]),
        ("WindowsProfileDoesNotEnableXaml", False, runtime["runtimeOptions"]["configProperties"]["CSWINRT_USE_WINDOWS_UI_XAML_PROJECTIONS"]),
        ("DesktopRuntimeDeclared", [
            {"name": "Microsoft.NETCore.App", "version": "8.0.0"},
            {"name": "Microsoft.WindowsDesktop.App", "version": "8.0.0"},
        ], desktop["runtimeOptions"]["frameworks"]),
    ]
    failures = [
        "{}: expected {}, actual {}".format(name, repr(expected), repr(actual))
        for name, expected, actual in cases
        if expected != actual
    ]
    return [AnalysisTestResultInfo(
        success = not failures,
        message = "\n".join(failures) if failures else "{} Windows targeting assertions passed.".format(len(cases)),
    )]

windows_targeting_test = rule(
    implementation = _targeting_tests_impl,
    analysis_test = True,
)
