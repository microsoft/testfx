// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.TrxReport;

internal static class TrxModeHelpers
{
    [UnsupportedOSPlatformGuard("BROWSER")]
    public static bool ShouldUseOutOfProcessTrxGeneration(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName) &&
            !OperatingSystem.IsBrowser();
}
