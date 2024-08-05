﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Testing.Platform.Helpers;

// The idea was taken from https://github.com/dotnet/aspnetcore/blob/main/src/Shared/TaskExtensions.cs
internal static class TaskExtensions
{
    // We observe by default because usually we're no more interested in the result of the task
    public static async Task<T> WithCancellationAsync<T>(this Task<T> task, CancellationToken cancellationToken, bool observeException = true)
    {
        if (observeException)
        {
            _ = task.ContinueWith(
                async task =>
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception)
                    {
                        // Observe the exception
                    }
                },
                cancellationToken);
        }

        // Don't create a timer if the task is already completed
        if (task.IsCompleted)
        {
            return await task.ConfigureAwait(false);
        }

        TaskCompletionSource<bool> taskCompletionSource = new();
        using CancellationTokenRegistration tokenRegistration = cancellationToken.Register(() => taskCompletionSource.TrySetCanceled());
        if (task == await Task.WhenAny(task, taskCompletionSource.Task).ConfigureAwait(false))
        {
            taskCompletionSource.SetResult(true);
            return await task.ConfigureAwait(false);
        }
        else
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    // We observe by default because usually we're no more interested in the result of the task
    public static async Task WithCancellationAsync(this Task task, CancellationToken cancellationToken, bool observeException = true)
    {
        if (observeException)
        {
            _ = task.ContinueWith(
                async task =>
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception)
                    {
                        // Observe the exception
                    }
                },
                cancellationToken);
        }

        // Don't create a timer if the task is already completed
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    // We observe by default because usually we're no more interested in the result of the task
    public static async Task TimeoutAfterAsync(this Task task, TimeSpan timeout, bool observeException = true,
       [CallerFilePath] string? filePath = null,
       [CallerLineNumber] int lineNumber = default)
    {
        if (observeException)
        {
            _ = task.ContinueWith(async task =>
            {
                try
                {
                    await task;
                }
                catch (Exception)
                {
                    // Observe the exception
                }
            });
        }

        // Don't create a timer if the task is already completed
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        try
        {
            await task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException ex) when (ex.Source == typeof(TaskExtensions).Namespace)
        {
            throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
        }
    }

    // We observe by default because usually we're no more interested in the result of the task
    public static async Task TimeoutAfterAsync(this Task task, TimeSpan timeout, CancellationToken token, bool observeException = true,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = default)
    {
        if (observeException)
        {
            _ = task.ContinueWith(
                async task =>
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception)
                    {
                        // Observe the exception
                    }
                },
                token);
        }

        // Don't create a timer if the task is already completed
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        try
        {
            await task.WaitAsync(timeout, token).ConfigureAwait(false);
        }
        catch (TimeoutException ex) when (ex.Source == typeof(TaskExtensions).Namespace)
        {
            throw new TimeoutException(CreateMessage(timeout, filePath, lineNumber));
        }
    }

    private static string CreateMessage(TimeSpan timeout, string? filePath, int lineNumber)
      => RoslynString.IsNullOrEmpty(filePath)
      ? $"The operation timed out after reaching the limit of {timeout.TotalMilliseconds}ms."
      : $"The operation at {filePath}:{lineNumber} timed out after reaching the limit of {timeout.TotalMilliseconds}ms.";
}
