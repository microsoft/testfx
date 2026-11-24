// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.InvocationLifetime;

/// <summary>
/// The context passed to <see cref="IInvocationLifetimeHandler.OnInvocationCompletedAsync"/>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class InvocationCompletedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvocationCompletedContext"/> class.
    /// </summary>
    /// <param name="exitCode">The exit code of the invocation before cleanup.</param>
    /// <param name="isCanceled">A value indicating whether the invocation was canceled.</param>
    public InvocationCompletedContext(int exitCode, bool isCanceled)
    {
        ExitCode = exitCode;
        IsCanceled = isCanceled;
    }

    /// <summary>
    /// Gets the exit code of the invocation before cleanup.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Gets a value indicating whether the invocation was canceled, stopped, or aborted (for example by a timeout).
    /// </summary>
    /// <remarks>
    /// Always <see langword="true"/> when <see cref="ExitCode"/> is the test session aborted exit code (3).
    /// </remarks>
    public bool IsCanceled { get; }
}
