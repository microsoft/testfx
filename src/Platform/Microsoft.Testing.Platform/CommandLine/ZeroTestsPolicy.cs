// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Controls how a run that executed zero (non-skipped) tests is treated when computing the
/// process exit code and the run verdict. See <c>--zero-tests-policy</c>.
/// </summary>
internal enum ZeroTestsPolicy
{
    /// <summary>
    /// Skipped tests count as tests that ran. Only a run where no test was discovered at all yields
    /// <see cref="Helpers.ExitCode.ZeroTests"/> (8); an all-skipped run is considered successful. This is the default.
    /// </summary>
    AllowSkipped,

    /// <summary>
    /// A run is considered to have run zero tests when no test was executed, treating skipped tests
    /// as if they had not run. An all-skipped (or truly empty) run therefore yields
    /// <see cref="Helpers.ExitCode.ZeroTests"/> (8).
    /// </summary>
    Strict,
}
