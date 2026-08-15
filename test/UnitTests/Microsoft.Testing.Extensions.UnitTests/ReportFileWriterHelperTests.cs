// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ReportFileWriterHelperTests
{
    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_ReturnsResult_WhenFuncSucceedsImmediately()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        int result = await ReportFileWriterHelper.RetryWhenIOExceptionAsync(clock, () => Task.FromResult(42)).ConfigureAwait(false);

        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_RetriesAndSucceeds_WhenIOExceptionThrownBeforeTimeout()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        int attempts = 0;

        int result = await ReportFileWriterHelper.RetryWhenIOExceptionAsync(clock, () =>
        {
            attempts++;
            return attempts < 3
                ? throw new IOException("locked")
                : Task.FromResult(attempts);
        }).ConfigureAwait(false);

        Assert.AreEqual(3, result);
        Assert.AreEqual(3, attempts);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_PropagatesNonIOException_Immediately()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        int attempts = 0;

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ReportFileWriterHelper.RetryWhenIOExceptionAsync<int>(clock, () =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            }));

        Assert.AreEqual("boom", exception.Message);
        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_RethrowsIOException_AfterTimeoutExceeded()
    {
        // The clock jumps from "before timeout" to "after timeout" between the first and second call to
        // UtcNow, simulating that the retry loop's configured timeout has elapsed. The helper should retry
        // once more after observing the elapsed timeout, and rethrow on the next IOException.
        var clock = new SequenceClock(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow + ReportFileWriterHelper.FileWriteRetryTimeout + TimeSpan.FromSeconds(1),
            DateTimeOffset.UtcNow + ReportFileWriterHelper.FileWriteRetryTimeout + TimeSpan.FromSeconds(1));
        int attempts = 0;

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => ReportFileWriterHelper.RetryWhenIOExceptionAsync<int>(clock, () =>
            {
                attempts++;
                throw new IOException($"locked-{attempts}");
            }));

        Assert.AreEqual("locked-2", exception.Message);
        Assert.AreEqual(2, attempts);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    /// <summary>
    /// An <see cref="IClock"/> stub that returns successive values from a fixed sequence, repeating the
    /// last value once the sequence is exhausted. Used to simulate time passing across the retry loop's
    /// polling of <see cref="IClock.UtcNow"/> without depending on real elapsed wall-clock time.
    /// </summary>
    private sealed class SequenceClock(params DateTimeOffset[] values) : IClock
    {
        private int _index = -1;

        public DateTimeOffset UtcNow
        {
            get
            {
                _index = Math.Min(_index + 1, values.Length - 1);
                return values[_index];
            }
        }
    }
}
