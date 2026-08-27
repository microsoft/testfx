// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.TrxReport;

internal static class TrxModeHelpers
{
    // TRX requires a controller-managed test host to recover the report when the test host crashes,
    // hangs, or is terminated by --timeout. Platforms that cannot launch a test-host process at all
    // fall back to the in-process implementation as a compatibility fallback (see
    // TrxReportExtensions.AddTrxReportProvider).
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

    // Used from within the test host (child) process: rely on the controller-presence state that MTP
    // actually established for this process, rather than recomputing which extension requested
    // isolation. This stays true even when another extension (HangDump, --timeout, ...) is the one
    // that caused the controller to be used.
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool ShouldUseOutOfProcessTrxGeneration(ICommandLineOptions commandLineOptions)
        => IsTestHostControllerSupported
        && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.TestHostControllerPIDOptionKey);
}
