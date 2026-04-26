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
    /// Gets a value indicating whether the test host app has exited gracefully.
    /// </summary>
    /// <remarks>
    /// This flag is based on a message that the TestApplication sends at its end.
    /// The test host process might still crash or be killed after sending this message, in which
    /// case this flag will be true but the process hasn't truly exited gracefully.
    /// </remarks>
    // TODO: As a follow-up, obsolete this property as error, and rename it to ReceivedTestHostCompletedEvent or TestHostCompletedEventReceived.
    // Note that this is a public API
    bool HasExitedGracefully { get; }
}
