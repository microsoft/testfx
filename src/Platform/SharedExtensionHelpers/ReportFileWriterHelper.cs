// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Shared helpers for report file writing with retry logic.
/// </summary>
internal static class ReportFileWriterHelper
{
    /// <summary>
    /// The duration for which file-write retry loops will keep retrying on <see cref="IOException"/>
    /// before propagating the exception to the caller.
    /// </summary>
    internal static readonly TimeSpan FileWriteRetryTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Executes <paramref name="func"/> and retries on any <see cref="IOException"/> for up to
    /// <see cref="FileWriteRetryTimeout"/>, after which the exception is rethrown.
    /// </summary>
    /// <typeparam name="T">The type of the value returned by <paramref name="func"/>.</typeparam>
    internal static async Task<T> RetryWhenIOExceptionAsync<T>(IClock clock, Func<Task<T>> func)
    {
        DateTimeOffset firstTryTime = clock.UtcNow;
        bool hasExceededTimeout = false;
        while (true)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Retry transient IO errors (e.g. file locked) until the timeout is exceeded.
                if (hasExceededTimeout)
                {
                    throw;
                }
            }

            // Keep retrying until the configured timeout is exceeded (then the next IOException is rethrown).
            if (clock.UtcNow - firstTryTime > FileWriteRetryTimeout)
            {
                hasExceededTimeout = true;
            }
        }
    }
}
