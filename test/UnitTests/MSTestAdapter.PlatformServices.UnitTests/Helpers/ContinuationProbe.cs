// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

/// <summary>
/// Detects awaits that capture the ambient <see cref="SynchronizationContext"/> (that is, awaits that are not
/// <c>ConfigureAwait(false)</c>). Start the production call (and complete the awaited operations) through
/// <see cref="Run{T}"/>: every <see cref="Run{T}"/> executes on a brand new thread that owns a distinct counting
/// <see cref="SynchronizationContext"/>, and the continuation of an await that captured such a context is always
/// <see cref="SynchronizationContext.Post"/>ed (never run inline), which increments <see cref="PostCount"/>.
/// </summary>
/// <remarks>
/// An await only captures the context when it actually suspends, so the awaited operation must still be pending
/// when the await executes, and the await itself must run synchronously inside <see cref="Run{T}"/> (that is, on a
/// path where every earlier await completed synchronously, or inline on the thread that completed the operation).
/// </remarks>
internal sealed class ContinuationProbe
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    private readonly StrongBox<int> _postCount = new();

    /// <summary>
    /// Gets how many continuations were posted back to a context captured by an await.
    /// </summary>
    public int PostCount => Volatile.Read(ref _postCount.Value);

    /// <summary>
    /// Runs <paramref name="step"/> synchronously on a dedicated thread that has its own counting context.
    /// </summary>
    /// <typeparam name="T">The result type of the step.</typeparam>
    /// <param name="step">The step to run, typically starting an async operation or completing an awaited one.</param>
    /// <returns>The value returned by <paramref name="step"/>.</returns>
    public T Run<T>(Func<T> step)
    {
        T? result = default;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new CountingSynchronizationContext(_postCount));
            try
            {
                result = step();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        if (!thread.Join(StepTimeout))
        {
            throw new TimeoutException("The probed step did not finish in time.");
        }

        failure?.Throw();
        return result!;
    }

    /// <summary>
    /// Runs <paramref name="step"/> synchronously on a dedicated thread that has its own counting context.
    /// </summary>
    /// <param name="step">The step to run.</param>
    public void Run(Action step)
        => Run(() =>
        {
            step();
            return true;
        });

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private readonly StrongBox<int> _postCount;

        public CountingSynchronizationContext(StrongBox<int> postCount) => _postCount = postCount;

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount.Value);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }

        public override SynchronizationContext CreateCopy() => new CountingSynchronizationContext(_postCount);
    }
}
