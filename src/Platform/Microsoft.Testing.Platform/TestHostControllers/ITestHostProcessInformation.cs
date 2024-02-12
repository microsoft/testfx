// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents information about a test host process.
/// </summary>
public interface ITestHostProcessInformation
{
    /// <summary>
    /// Gets the process ID of the test host.
    /// </summary>
    int PID { get; }

    /// <summary>
    /// Gets the exit code of the test host process.
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// Gets a value indicating whether the test host process has exited gracefully.
    /// </summary>
    bool HasExitedGracefully { get; }
}
