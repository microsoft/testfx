// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal static class BoundedResourceCleanup
{
    public static async Task<T?> ObserveCreationAsync<T>(
        Task<T> creationTask,
        TimeSpan timeout,
        Action<T> dispose,
        Action<string> report)
        where T : class
    {
        try
        {
            return await creationTask.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            report("Resource creation did not finish during bounded cleanup; disposal will continue in the background.");
            _ = DisposeWhenCreatedAsync(creationTask, timeout, dispose, report);
            return null;
        }
        catch (Exception ex)
        {
            report($"Resource creation failed during cleanup: {ex.Message}");
            return null;
        }
    }

    public static async Task DisposeAsync(
        Action dispose,
        TimeSpan timeout,
        Action<string> report)
    {
        try
        {
            await Task.Run(dispose).WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            report($"Resource disposal did not complete cleanly: {ex.Message}");
        }
    }

    private static async Task DisposeWhenCreatedAsync<T>(
        Task<T> creationTask,
        TimeSpan timeout,
        Action<T> dispose,
        Action<string> report)
    {
        try
        {
            T resource = await creationTask.ConfigureAwait(false);
            await DisposeAsync(() => dispose(resource), timeout, report).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            report($"Late resource creation could not be cleaned up: {ex.Message}");
        }
    }
}
