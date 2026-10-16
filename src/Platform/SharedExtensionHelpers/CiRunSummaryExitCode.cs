// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.
internal static class CiRunSummaryExitCode
{
    public static bool IsTestResultOutcome(int exitCode)
        => exitCode is (int)ExitCode.Success or (int)ExitCode.AtLeastOneTestFailed;

    public static bool IndicatesFailure(int exitCode)
        => exitCode != (int)ExitCode.Success;

    public static string GetName(int exitCode)
        => Enum.IsDefined(typeof(ExitCode), exitCode)
            ? ((ExitCode)exitCode).ToString()
            : "Unknown";

    public static string GetReason(int exitCode, ResourceManager resources)
        => resources.GetString($"ExitCodeReason{GetName(exitCode)}", System.Globalization.CultureInfo.CurrentUICulture)
            ?? resources.GetString("ExitCodeReasonUnknown", System.Globalization.CultureInfo.CurrentUICulture)
            ?? "The test run reported a non-success exit code.";
}
#pragma warning restore RS0051
