// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ReportFileWriterHelperTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_ImmediateSuccess_ReturnsResultWithoutRetrying()
    {
        var clock = new SequenceClock(StartTime);
        int invocationCount = 0;

        int result = await ReportFileWriterHelper.RetryWhenIOExceptionAsync(clock, () =>
        {
            invocationCount++;
            return Task.FromResult(42);
        });

        Assert.AreEqual(42, result);
        Assert.AreEqual(1, invocationCount);
        Assert.AreEqual(1, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_TransientIOException_RetriesUntilSuccess()
    {
        var clock = new SequenceClock(
            StartTime,
            StartTime + ReportFileWriterHelper.FileWriteRetryTimeout - TimeSpan.FromTicks(1));
        int invocationCount = 0;

        int result = await ReportFileWriterHelper.RetryWhenIOExceptionAsync(clock, () =>
        {
            invocationCount++;
            return invocationCount == 1
                ? Task.FromException<int>(new IOException("The report file is temporarily locked."))
                : Task.FromResult(42);
        });

        Assert.AreEqual(42, result);
        Assert.AreEqual(2, invocationCount);
        Assert.AreEqual(2, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_IOExceptionAtTimeoutBoundary_Retries()
    {
        var clock = new SequenceClock(
            StartTime,
            StartTime + ReportFileWriterHelper.FileWriteRetryTimeout,
            StartTime + ReportFileWriterHelper.FileWriteRetryTimeout);
        int invocationCount = 0;

        int result = await ReportFileWriterHelper.RetryWhenIOExceptionAsync(clock, () =>
        {
            invocationCount++;
            return invocationCount <= 2
                ? Task.FromException<int>(new IOException("The report file is temporarily locked."))
                : Task.FromResult(42);
        });

        Assert.AreEqual(42, result);
        Assert.AreEqual(3, invocationCount);
        Assert.AreEqual(3, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_TimeoutExceeded_RethrowsNextIOException()
    {
        var clock = new SequenceClock(
            StartTime,
            StartTime + ReportFileWriterHelper.FileWriteRetryTimeout + TimeSpan.FromTicks(1));
        var firstException = new IOException("The report file is temporarily locked.");
        var expectedException = new IOException("The report file remains locked.");
        int invocationCount = 0;

        IOException actualException = await Assert.ThrowsExactlyAsync<IOException>(
            () => ReportFileWriterHelper.RetryWhenIOExceptionAsync<int>(clock, () =>
            {
                invocationCount++;
                return Task.FromException<int>(invocationCount == 1 ? firstException : expectedException);
            }));

        Assert.AreSame(expectedException, actualException);
        Assert.AreEqual(2, invocationCount);
        Assert.AreEqual(2, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_TimeoutExceeded_ReturnsResultWhenNextInvocationSucceeds()
    {
        var clock = new SequenceClock(
            StartTime,
            StartTime + ReportFileWriterHelper.FileWriteRetryTimeout + TimeSpan.FromTicks(1));
        int invocationCount = 0;

        int result = await ReportFileWriterHelper.RetryWhenIOExceptionAsync(clock, () =>
        {
            invocationCount++;
            return invocationCount == 1
                ? Task.FromException<int>(new IOException("The report file is temporarily locked."))
                : Task.FromResult(42);
        });

        Assert.AreEqual(42, result);
        Assert.AreEqual(2, invocationCount);
        Assert.AreEqual(2, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_NonIOExceptionAfterRetry_PropagatesWithoutFurtherRetrying()
    {
        var clock = new SequenceClock(
            StartTime,
            StartTime + ReportFileWriterHelper.FileWriteRetryTimeout - TimeSpan.FromTicks(1));
        var expectedException = new InvalidOperationException("Report generation failed.");
        int invocationCount = 0;

        InvalidOperationException actualException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ReportFileWriterHelper.RetryWhenIOExceptionAsync<int>(clock, () =>
            {
                invocationCount++;
                return Task.FromException<int>(
                    invocationCount == 1
                        ? new IOException("The report file is temporarily locked.")
                        : expectedException);
            }));

        Assert.AreSame(expectedException, actualException);
        Assert.AreEqual(2, invocationCount);
        Assert.AreEqual(2, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_NonIOException_PropagatesWithoutRetrying()
    {
        var clock = new SequenceClock(StartTime);
        var expectedException = new InvalidOperationException("Report generation failed.");
        int invocationCount = 0;

        InvalidOperationException actualException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ReportFileWriterHelper.RetryWhenIOExceptionAsync<int>(clock, () =>
            {
                invocationCount++;
                return Task.FromException<int>(expectedException);
            }));

        Assert.AreSame(expectedException, actualException);
        Assert.AreEqual(1, invocationCount);
        Assert.AreEqual(1, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    [TestMethod]
    public async Task RetryWhenIOExceptionAsync_CanceledOperation_PropagatesWithoutRetrying()
    {
        var clock = new SequenceClock(StartTime);
        var cancellationToken = new CancellationToken(canceled: true);
        int invocationCount = 0;

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => ReportFileWriterHelper.RetryWhenIOExceptionAsync<int>(clock, () =>
            {
                invocationCount++;
                return Task.FromCanceled<int>(cancellationToken);
            }));

        Assert.AreEqual(cancellationToken, exception.CancellationToken);
        Assert.AreEqual(1, invocationCount);
        Assert.AreEqual(1, clock.ReadCount);
        Assert.AreEqual(0, clock.RemainingCount);
    }

    private sealed class SequenceClock(params DateTimeOffset[] timestamps) : IClock
    {
        private readonly Queue<DateTimeOffset> _timestamps = new(timestamps);

        public int ReadCount { get; private set; }

        public int RemainingCount => _timestamps.Count;

        public DateTimeOffset UtcNow
        {
            get
            {
                ReadCount++;
                return _timestamps.Count > 0
                    ? _timestamps.Dequeue()
                    : throw new InvalidOperationException("The retry helper read the clock more times than expected.");
            }
        }
    }
}
