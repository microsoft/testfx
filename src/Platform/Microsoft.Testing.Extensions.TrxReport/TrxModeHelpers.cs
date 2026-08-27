// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.TrxReport;

internal static class TrxModeHelpers
{
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool IsTestHostControllerSupported { get; } =
#if NETCOREAPP
        !OperatingSystem.IsBrowser()
        && !OperatingSystem.IsIOS()
        && !OperatingSystem.IsTvOS()
        && !OperatingSystem.IsWasi();
#else
        true;
#endif

    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool ShouldUseControllerBackedTrxGeneration(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxModeOptionName, out string[]? arguments)
            ? arguments.Length == 1
                && TrxReportGeneratorCommandLine.OutOfProcessMode.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
                && IsTestHostControllerSupported
            : IsTestHostControllerSupported;

    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool ShouldUseOutOfProcessTrxGeneration(ICommandLineOptions commandLineOptions)
        => ShouldUseControllerBackedTrxGeneration(commandLineOptions)
        && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.TestHostControllerPIDOptionKey);
}
