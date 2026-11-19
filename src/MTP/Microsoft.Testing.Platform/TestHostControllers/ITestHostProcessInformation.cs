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
    /// Gets a value indicating whether the controller received the completion message from the test host.
    /// </summary>
    /// <remarks>
    /// This value describes receipt of the completion message, not successful process termination.
    /// The test host can still crash or be killed after sending this message, so this value can be
    /// <see langword="true"/> even when the process exits abnormally. Inspect <see cref="ExitCode"/> separately.
    /// </remarks>
    bool HasReceivedTestHostCompletedMessage { get; }
}
