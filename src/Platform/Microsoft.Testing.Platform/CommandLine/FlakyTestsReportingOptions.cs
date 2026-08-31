// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Owns the name and the resolution of the <c>--show-flaky-tests</c> option.
/// </summary>
/// <remarks>
/// Deliberately kept out of <c>TerminalTestReporterCommandLineOptionsProvider</c>, which is marked
/// <c>[Embedded]</c> and therefore cannot be referenced from other assemblies even through
/// <c>InternalsVisibleTo</c>. The retry orchestrator runs in a separate process from the terminal reporter but must
/// honour the very same option, so the shared piece lives here.
/// </remarks>
internal static class FlakyTestsReportingOptions
{
    public const string ShowFlakyTestsOptionName = "show-flaky-tests";

    /// <summary>
    /// Resolves <c>--show-flaky-tests</c>. Flaky reporting is <b>on by default</b>: it costs nothing on a run where
    /// nothing was retried, and a failure that silently recovered is exactly what the user needs to be told about.
    /// Passing the option without an argument keeps it on; only an explicit off value (<c>off</c>, <c>false</c>,
    /// <c>disable</c>, <c>0</c>) turns it off.
    /// </summary>
    public static bool IsEnabled(ICommandLineOptions commandLineOptions)
        => !commandLineOptions.TryGetOptionArgumentList(ShowFlakyTestsOptionName, out string[]? arguments)
            || arguments is not { Length: > 0 }
            || !CommandLineOptionArgumentValidator.IsOffValue(arguments[0]);
}
