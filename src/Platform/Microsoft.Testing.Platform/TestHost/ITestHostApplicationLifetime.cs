// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHost;

/// <summary>
/// Represents the interface for test application lifecycle callbacks.
/// </summary>
[Obsolete("Use ITestHostApplicationLifetime instead. This interface will be removed in v2.")]
public interface ITestApplicationLifecycleCallbacks : ITestHostExtension
{
    /// <summary>
    /// Executes before the test run.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeforeRunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes after the test run.
    /// </summary>
    /// <param name="exitCode">The exit code of the test run.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AfterRunAsync(int exitCode, CancellationToken cancellation);
}

/// <summary>
/// Represents the interface for test application lifecycle callbacks.
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
public interface ITestHostApplicationLifetime : ITestHostExtension, ITestApplicationLifecycleCallbacks
#pragma warning restore CS0618 // Type or member is obsolete
{
    // In v2, move BeforeRunAsync and AfterRunAsync to ITestHostApplicationLifetime directly
}
