// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Bounded wait usable on every target framework (Task.WaitAsync does not exist on net462): a regression that makes
/// the awaited task hang fails fast with an assertion instead of blocking the whole test run.
/// </summary>
internal static class BoundedTaskWait
{
    public static async Task CompleteWithinAsync(Task task, int seconds = 30)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)));
        Assert.AreSame(task, finished, $"The task did not complete within {seconds} seconds.");
        await task;
    }
}
