// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions.TrxReport;

internal static class TrxModeHelpers
{
    // TRX requires a controller-managed test host to recover the report when the test host crashes,
    // hangs, or is terminated by --timeout. Platforms that cannot launch a test-host process at all
    // fall back to the in-process implementation as a compatibility fallback (see
    // TrxReportExtensions.AddTrxReportProvider). Android cannot use the platform's default
    // Process.Start-based launch path (TestHostControllersTestHost.ProcessLifecycle) to spawn an
    // arbitrary child test host, so it must fall back in-process too even though the package targets
    // it (see the csproj's SupportedPlatform entries for android/browser/ios/tvos/wasi).
    //
    // This assembly also ships a netstandard2.0 asset, so these checks must run for every target, not
    // just NETCOREAPP: under NETCOREAPP they resolve via the BCL OperatingSystem APIs, and under
    // netstandard2.0/.NET Framework they resolve via the Polyfills OperatingSystem extension, which
    // itself already returns constant false for these platforms on .NET Framework (which cannot run on
    // them) while evaluating the actual runtime via RuntimeInformation.IsOSPlatform for netstandard2.0
    // hosts (e.g. Mono/MAUI) that can.
    [UnsupportedOSPlatformGuard("android")]
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool IsTestHostControllerSupported { get; } =
        !OperatingSystem.IsAndroid()
        && !OperatingSystem.IsBrowser()
        && !OperatingSystem.IsIOS()
        && !OperatingSystem.IsTvOS()
        && !OperatingSystem.IsWasi();

    [UnsupportedOSPlatformGuard("android")]
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool ShouldUseControllerBackedTrxGeneration(ICommandLineOptions commandLineOptions)
        => IsTestHostControllerSupported
        && (!commandLineOptions.TryGetOptionArgumentListOrDefault(TrxReportGeneratorCommandLine.TrxModeOptionName, out string[]? arguments)
            || (arguments is [string mode] && TrxReportGeneratorCommandLine.OutOfProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase)));

    public static string GetControllerModeEnvironmentVariableName(string controllerPid) => $"TRXMODE_{controllerPid}";

    public static string? GetEffectiveMode(ICommandLineOptions commandLineOptions, IEnvironment environment)
    {
        string? controllerMode = commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.TestHostControllerPIDOptionKey, out string[]? controllerArguments)
            && controllerArguments is [string controllerPid]
                ? environment.GetEnvironmentVariable(GetControllerModeEnvironmentVariableName(controllerPid))
                : null;

        return controllerMode switch
        {
            null => commandLineOptions.TryGetOptionArgumentListOrDefault(TrxReportGeneratorCommandLine.TrxModeOptionName, out string[]? arguments)
                && arguments is [string mode] ? mode : null,
            TrxReportGeneratorCommandLine.InProcessMode or TrxReportGeneratorCommandLine.OutOfProcessMode => controllerMode,
            _ => throw new InvalidOperationException(ExtensionResources.TrxModeInvalidValue),
        };
    }

    [UnsupportedOSPlatformGuard("android")]
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool ShouldUseOutOfProcessTrxGeneration(ICommandLineOptions commandLineOptions)
        => ShouldUseOutOfProcessTrxGeneration(commandLineOptions, new SystemEnvironment());

    [UnsupportedOSPlatformGuard("android")]
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool ShouldUseOutOfProcessTrxGeneration(ICommandLineOptions commandLineOptions, IEnvironment environment)
    {
        if (!IsTestHostControllerSupported || !commandLineOptions.IsOptionSet(PlatformCommandLineProvider.TestHostControllerPIDOptionKey))
        {
            return false;
        }

        string? mode = GetEffectiveMode(commandLineOptions, environment);
        return mode is null || TrxReportGeneratorCommandLine.OutOfProcessMode.Equals(mode, StringComparison.OrdinalIgnoreCase);
    }
}
