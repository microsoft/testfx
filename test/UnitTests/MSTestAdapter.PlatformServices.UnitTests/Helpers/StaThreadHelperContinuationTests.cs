// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Both the supported Windows contract and the explicit non-Windows rejection are tested.")]
public sealed class StaThreadHelperContinuationTests : TestContainer
{
    public async Task UnspecifiedApartmentRunsInlineWithoutWaiting()
    {
        int caller = Environment.CurrentManagedThreadId;
        List<string> events = [];
        int result = await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
            null, "unused", () =>
            {
                Environment.CurrentManagedThreadId.Should().Be(caller);
                events.Add("action");
                return Task.FromResult(23);
            },
            () => throw new InvalidOperationException("unexpected fallback"),
            _ => throw new InvalidOperationException("unexpected wait"),
            (_, _) => throw new InvalidOperationException("unexpected handler"),
            () => events.Add("warning"));

        result.Should().Be(23);
        events.Should().Equal("action");
    }

    public async Task RequestedApartmentUsesTheHostContract()
    {
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        int caller = Environment.CurrentManagedThreadId;
        int actionThread = 0;
        ApartmentState requested = windows && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? ApartmentState.MTA : ApartmentState.STA;
        List<string> events = [];
        int result = await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
            requested, "coverage-apartment", () =>
            {
                actionThread = Environment.CurrentManagedThreadId;
                if (windows)
                {
                    Thread.CurrentThread.GetApartmentState().Should().Be(requested);
                    Thread.CurrentThread.Name.Should().Be("coverage-apartment");
                }

                events.Add("action");
                return Task.FromResult(37);
            },
            () => -1,
            thread => Task.Run(thread.Join),
            (_, error) => throw error,
            () => events.Add("warning"));

        result.Should().Be(37);
        if (windows)
        {
            actionThread.Should().NotBe(caller);
            events.Should().Equal("action");
        }
        else
        {
            actionThread.Should().Be(caller);
            events.Should().Equal("warning", "action");
        }
    }

    public async Task WaitFailuresAreReportedWithTheOriginalThreadAndException()
    {
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var failure = new IOException("wait failure");
        Thread? waitedThread = null;
        Exception? reportedFailure = null;
        int warnings = 0;
        ApartmentState requested = windows && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? ApartmentState.MTA : ApartmentState.STA;

        int result = await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
            requested, "coverage-wait-failure", () => Task.FromResult(17), () => -1,
            thread =>
            {
                waitedThread = thread;
                thread.Join();
                return Task.FromException(failure);
            },
            (thread, error) =>
            {
                thread.Should().BeSameAs(waitedThread);
                reportedFailure = error;
                return 91;
            },
            () => warnings++);

        if (windows)
        {
            result.Should().Be(91);
            reportedFailure.Should().BeSameAs(failure);
            warnings.Should().Be(0);
        }
        else
        {
            result.Should().Be(17);
            waitedThread.Should().BeNull();
            reportedFailure.Should().BeNull();
            warnings.Should().Be(1);
        }
    }

    public async Task IncompleteApartmentWorkUsesFallbackUntilTheOwnedGateIsReleased()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int warnings = 0;
            var failure = new InvalidOperationException("inline failure");
            Func<Task> run = async () => await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
                ApartmentState.STA, "not-created", () => Task.FromException<int>(failure), () => -1,
                _ => throw new InvalidOperationException("unexpected wait"), (_, _) => -2, () => warnings++);
            (await run.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
            warnings.Should().Be(1);
            return;
        }

        var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread? ownedThread = null;
        int fallbacks = 0;
        ApartmentState requested = Thread.CurrentThread.GetApartmentState() == ApartmentState.STA ? ApartmentState.MTA : ApartmentState.STA;
        try
        {
            int result = await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
                requested, "coverage-gated-apartment", () => gate.Task,
                () =>
                {
                    fallbacks++;
                    return 73;
                },
                thread =>
                {
                    ownedThread = thread;
                    return Task.CompletedTask;
                },
                (_, error) => throw error, () => throw new InvalidOperationException("unexpected warning"));

            result.Should().Be(73);
            fallbacks.Should().Be(1);
            gate.Task.IsCompleted.Should().BeFalse();
        }
        finally
        {
            gate.TrySetResult(17);
            ownedThread?.Join();
        }
    }

    public async Task StaRunnerPropagatesFailuresAndHonorsHostSupport()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Action run = () => _ = StaThreadHelper.RunOnStaThreadAsync(() => Task.CompletedTask);
            run.Should().ThrowExactly<PlatformNotSupportedException>();
            return;
        }

        int executed = 0;
        await StaThreadHelper.RunOnStaThreadAsync(() =>
        {
            Thread.CurrentThread.GetApartmentState().Should().Be(ApartmentState.STA);
            executed++;
            return Task.CompletedTask;
        });
        executed.Should().Be(1);
        var failure = new InvalidOperationException("STA action failure");
        foreach (bool synchronous in new[] { true, false })
        {
            Func<Task> run = () => StaThreadHelper.RunOnStaThreadAsync(() => synchronous ? throw failure : Task.FromException(failure));
            (await run.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        }
    }
}
