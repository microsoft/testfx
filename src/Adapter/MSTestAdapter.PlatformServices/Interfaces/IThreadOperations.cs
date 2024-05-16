// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// This service is responsible for any thread operations specific to a platform.
/// </summary>
public interface IThreadOperations
{
    /// <summary>
    /// Execute the given action synchronously on a background thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="cancelToken">Token to cancel the execution.</param>
    /// <returns>Returns true if the action executed before the timeout. returns false otherwise.</returns>
    bool Execute(Action action, CancellationToken cancelToken);

    /// <summary>
    /// Execute the given action synchronously on a background thread in the given timeout.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="timeout">Timeout for the specified action in milliseconds.</param>
    /// <param name="cancelToken">Token to cancel the execution.</param>
    /// <returns>Returns true if the action executed before the timeout. returns false otherwise.</returns>
    bool Execute(Action action, int timeout, CancellationToken cancelToken);
}
