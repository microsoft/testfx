// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions.TrxReport;

internal static class TrxModeHelpers
{
    // TRX requires a controller-managed test host to recover the report when the test host crashes,
    // hangs, or is terminated by --timeout. Platforms that cannot launch a test-host process at all
    // fall back to the in-process implementation as a compatibility fallback (see
    // TrxReportExtensions.AddTrxReportProvider).
    //
    // This assembly also ships a netstandard2.0 asset (see the csproj's SupportedPlatform entries for
    // browser/ios/tvos/wasi), so these checks must run for every target, not just NETCOREAPP: under
    // NETCOREAPP they resolve via the BCL OperatingSystem APIs, and under netstandard2.0/.NET Framework
    // they resolve via the Polyfills OperatingSystem extension, which itself already returns constant
    // false for these platforms on .NET Framework (which cannot run on them) while evaluating the
    // actual runtime via RuntimeInformation.IsOSPlatform for netstandard2.0 hosts (e.g. Mono/MAUI) that
    // can.
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool IsTestHostControllerSupported { get; } =
        !OperatingSystem.IsBrowser()
        && !OperatingSystem.IsIOS()
        && !OperatingSystem.IsTvOS()
        && !OperatingSystem.IsWasi();

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
