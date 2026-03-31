// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Helpers;

internal static class DisposeHelper
{
    public static async Task DisposeAsync(object? obj)
    {
        if (obj is null)
        {
            return;
        }

        if (obj is IAsyncCleanableExtension async)
        {
            await async.CleanupAsync().ConfigureAwait(false);
        }

#if NETCOREAPP
        if (obj is IAsyncDisposable dcAsyncDisposable)
        {
            await dcAsyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            if (obj is IDisposable dcDisposable)
            {
                dcDisposable.Dispose();
            }
        }
#else
        if (obj is IDisposable dcDisposable)
        {
            dcDisposable.Dispose();
        }
#endif
    }
}
