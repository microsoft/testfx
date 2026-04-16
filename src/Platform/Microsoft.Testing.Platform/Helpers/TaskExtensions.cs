// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

// The idea was taken from https://github.com/dotnet/aspnetcore/blob/main/src/Shared/TaskExtensions.cs
internal static class TaskExtensions
{
#if !NETCOREAPP
    private const uint MaxSupportedTimeout = 0xfffffffe;

    public static Task WaitAsync(this Task target, CancellationToken cancellationToken) =>
        target.WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task WaitAsync(
        this Task target,
        TimeSpan timeout) =>
        target.WaitAsync(timeout, default);

    public static async Task WaitAsync(
        this Task target,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        long milliseconds = (long)timeout.TotalMilliseconds;
        if (milliseconds is < -1 or > MaxSupportedTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (target.IsCompleted ||
            (!cancellationToken.CanBeCanceled && timeout == Timeout.InfiniteTimeSpan))
        {
            await target.ConfigureAwait(false);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
        }

        if (timeout == TimeSpan.Zero)
        {
            throw new TimeoutException();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var cancellationTask = new TaskCompletionSource<bool>();
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
        using CancellationTokenRegistration _ = cts.Token.Register(tcs => ((TaskCompletionSource<bool>)tcs!).TrySetResult(true), cancellationTask);
#pragma warning restore SA1312 // Variable names should begin with lower-case letter
        await Task.WhenAny(target, cancellationTask.Task).ConfigureAwait(false);

        if (!target.IsCompleted)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException();
        }

        await target.ConfigureAwait(false);
    }

    public static Task<TResult> WaitAsync<TResult>(
        this Task<TResult> target,
        CancellationToken cancellationToken) =>
        target.WaitAsync<TResult>(Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task<TResult> WaitAsync<TResult>(
        this Task<TResult> target,
        TimeSpan timeout) =>
        target.WaitAsync<TResult>(timeout, default);

    public static async Task<TResult> WaitAsync<TResult>(
        this Task<TResult> target,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await ((Task)target).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        return target.Result;
#pragma warning restore VSTHRD103 // Call async methods when in an async method
    }
#endif

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
                        await task.ConfigureAwait(false);
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
                        await task.ConfigureAwait(false);
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
                    await task.ConfigureAwait(false);
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
                        await task.ConfigureAwait(false);
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
