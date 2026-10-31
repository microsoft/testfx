// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Helpers;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpProcessTreeBehaviorTests
{
    [TestMethod]
    public async Task QueryOnce_SchedulesWholeTreeWithOneSharedSnapshotBeforeAwaiting()
    {
        IProcess[] tree = [CreateProcess(330, "leaf"), CreateProcess(220, "child"), CreateProcess(110, "testhost")];
        (string, int)[] snapshot = [("Suite.LongRunning", 73), ("Suite.Other", 19)];
        List<Func<Task>> scheduled = [];
        List<CancellationToken> schedulerTokens = [];
        List<(IProcess Process, (string, int)[] Snapshot, CancellationToken Token)> dumps = [];
        TaskCompletionSource<bool>[] completions =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];
        Task[]? joined = null;
        var scheduler = new Mock<ITask>(MockBehavior.Strict);
        scheduler.Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> callback, CancellationToken token) =>
            {
                scheduled.Add(callback);
                schedulerTokens.Add(token);
                return completions[scheduled.Count - 1].Task;
            });
        scheduler.Setup(x => x.WhenAll(It.IsAny<Task[]>()))
            .Returns((Task[] tasks) =>
            {
                joined = tasks;
                return Task.WhenAll(tasks);
            });
        using var applicationCancellation = new CancellationTokenSource();
        applicationCancellation.Cancel();
        int queryCount = 0;
        CancellationToken queryToken = default;

        Task operation = HangDumpProcessLifetimeHandler.QueryOnceAndDumpTreeAsync(
            tree,
            scheduler.Object,
            token =>
            {
                queryCount++;
                queryToken = token;
                return Task.FromResult(snapshot);
            },
            (process, answer, token) =>
            {
                dumps.Add((process, answer, token));
                return Task.CompletedTask;
            },
            applicationCancellation.Token);

        try
        {
            Assert.AreEqual(1, queryCount);
            Assert.AreEqual(applicationCancellation.Token, queryToken);
            Assert.HasCount(3, scheduled);
            Assert.IsNotNull(joined);
            Assert.AreSequenceEqual(completions.Select(completion => completion.Task), joined);
            Assert.AreSequenceEqual(Enumerable.Repeat(CancellationToken.None, 3), schedulerTokens);
            Assert.IsFalse(operation.IsCompleted, "The helper must await the scheduled tasks, not merely queue them.");
            Assert.IsEmpty(dumps);

            for (int i = 0; i < scheduled.Count; i++)
            {
                await scheduled[i]();
                completions[i].SetResult(true);
            }

            await operation;
            Assert.AreSequenceEqual(tree, dumps.Select(dump => dump.Process));
            foreach ((IProcess _, (string, int)[] answer, CancellationToken token) in dumps)
            {
                Assert.AreSame(snapshot, answer);
                Assert.AreEqual(applicationCancellation.Token, token);
                Assert.IsTrue(token.IsCancellationRequested);
            }

            scheduler.Verify(x => x.Run(It.IsAny<Func<Task>>(), CancellationToken.None), Times.Exactly(3));
            scheduler.Verify(x => x.WhenAll(It.IsAny<Task[]>()), Times.Once);
            scheduler.VerifyNoOtherCalls();
        }
        finally
        {
            foreach (TaskCompletionSource<bool> completion in completions)
            {
                completion.TrySetResult(true);
            }
        }
    }

    [TestMethod]
    public async Task QueryOnce_DumpFailureStillSchedulesAndWaitsForSiblings()
    {
        IProcess[] tree = [CreateProcess(330, "leaf"), CreateProcess(220, "child"), CreateProcess(110, "testhost")];
        (string, int)[] snapshot = [("Suite.WaitingForChild", 42)];
        var failure = new InvalidOperationException("The child dump failed.");
        var releaseSiblings = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<IProcess> started = [];
        List<(string, int)[]> answers = [];
        var scheduler = new Mock<ITask>(MockBehavior.Strict);
        scheduler.Setup(x => x.Run(It.IsAny<Func<Task>>(), CancellationToken.None))
            .Returns((Func<Task> callback, CancellationToken _) => callback());
        scheduler.Setup(x => x.WhenAll(It.IsAny<Task[]>())).Returns((Task[] tasks) => Task.WhenAll(tasks));
        int queryCount = 0;

        Task operation = HangDumpProcessLifetimeHandler.QueryOnceAndDumpTreeAsync(
            tree,
            scheduler.Object,
            _ =>
            {
                queryCount++;
                return Task.FromResult(snapshot);
            },
            (process, answer, _) =>
            {
                started.Add(process);
                answers.Add(answer);
                return ReferenceEquals(process, tree[0]) ? Task.FromException(failure) : releaseSiblings.Task;
            },
            CancellationToken.None);

        try
        {
            Assert.AreSequenceEqual(tree, started);
            Assert.IsFalse(operation.IsCompleted, "A faulted child must not abandon its still-running siblings.");
        }
        finally
        {
            releaseSiblings.TrySetResult(true);
        }

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => operation);
        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, queryCount);
        Assert.HasCount(3, answers);
        foreach ((string, int)[] answer in answers)
        {
            Assert.AreSame(snapshot, answer);
        }

        scheduler.Verify(x => x.Run(It.IsAny<Func<Task>>(), CancellationToken.None), Times.Exactly(3));
        scheduler.Verify(x => x.WhenAll(It.IsAny<Task[]>()), Times.Once);
        scheduler.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(true, DisplayName = "TreeQuery_SynchronousFailureSchedulesNothing")]
    [DataRow(false, DisplayName = "TreeQuery_AsynchronousFailureSchedulesNothing")]
    public async Task QueryOnce_QueryFailureDoesNotScheduleDumps(bool synchronousFailure)
    {
        var failure = new IOException("The snapshot query failed.");
        var scheduler = new Mock<ITask>(MockBehavior.Strict);
        int queryCount = 0;
        int dumpCount = 0;

        IOException actual = await Assert.ThrowsExactlyAsync<IOException>(() => HangDumpProcessLifetimeHandler.QueryOnceAndDumpTreeAsync(
            [CreateProcess(110, "testhost")],
            scheduler.Object,
            _ =>
            {
                queryCount++;
                return synchronousFailure ? throw failure : Task.FromException<(string, int)[]>(failure);
            },
            (_, _, _) =>
            {
                dumpCount++;
                return Task.CompletedTask;
            },
            CancellationToken.None));

        Assert.AreSame(failure, actual);
        Assert.AreEqual(1, queryCount);
        Assert.AreEqual(0, dumpCount);
        scheduler.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task QueryOnce_EmptyTreeStillQueriesAndJoinsEmptySet()
    {
        List<string> operations = [];
        var scheduler = new Mock<ITask>(MockBehavior.Strict);
        Task[]? joined = null;
        scheduler.Setup(x => x.WhenAll(It.IsAny<Task[]>()))
            .Returns((Task[] tasks) =>
            {
                joined = tasks;
                operations.Add("join");
                return Task.CompletedTask;
            });

        await HangDumpProcessLifetimeHandler.QueryOnceAndDumpTreeAsync(
            [],
            scheduler.Object,
            _ =>
            {
                operations.Add("query");
                return Task.FromResult<(string, int)[]>([("Running but no process survived", 5)]);
            },
            (_, _, _) => throw new AssertFailedException("An empty tree must not schedule a dump."),
            CancellationToken.None);

        Assert.AreSequenceEqual(["query", "join"], operations);
        Assert.IsNotNull(joined);
        Assert.IsEmpty(joined);
        scheduler.Verify(x => x.WhenAll(It.IsAny<Task[]>()), Times.Once);
        scheduler.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TreeEnumeration_PreservesOrderedNodes")]
    [DataRow(true, DisplayName = "TreeEnumeration_EmptySuccessIsNotAFailure")]
    public async Task Enumerate_HealthyResultIsReturnedWithoutInventingFallbackNodes(bool empty)
    {
        IProcess root = CreateProcess(110, "testhost");
        IProcess child = CreateProcess(220, "child");
        List<ProcessTreeNode> expected = empty
            ? []
            : [new() { Process = root, Level = 0 }, new() { Process = child, ParentId = 110, Level = 1 }];
        using var applicationCancellation = new CancellationTokenSource();
        CancellationToken enumerationToken = default;
        int failureCount = 0;

        List<ProcessTreeNode> actual = await HangDumpProcessLifetimeHandler.GetProcessTreeWithTimeoutAsync(
            token =>
            {
                enumerationToken = token;
                return Task.FromResult(expected);
            },
            Timeout.InfiniteTimeSpan,
            _ =>
            {
                failureCount++;
                return Task.CompletedTask;
            },
            root,
            applicationCancellation.Token);

        Assert.AreSame(expected, actual);
        Assert.AreEqual(0, failureCount);
        Assert.IsTrue(enumerationToken.CanBeCanceled);
        Assert.AreNotEqual(applicationCancellation.Token, enumerationToken);
        applicationCancellation.Cancel();
        Assert.IsFalse(enumerationToken.IsCancellationRequested, "Completed enumeration must release its linked cancellation source.");
    }

    [TestMethod]
    [DataRow(0, DisplayName = "TreeEnumeration_SynchronousFailureAndBrokenDiagnostic")]
    [DataRow(1, DisplayName = "TreeEnumeration_AsynchronousFailureAndBrokenDiagnostic")]
    [DataRow(2, DisplayName = "TreeEnumeration_CanceledCallerAndBrokenDiagnostic")]
    public async Task Enumerate_FailureReturnsRootEvenWhenFailureDiagnosticThrows(int scenario)
    {
        IProcess root = CreateProcess(110, "testhost");
        using var cancellation = new CancellationTokenSource();
        if (scenario == 2)
        {
            cancellation.Cancel();
        }

        var failure = new IOException("Process enumeration failed.");
        CancellationToken enumerationToken = default;
        List<Exception> reported = [];

        List<ProcessTreeNode> actual = await HangDumpProcessLifetimeHandler.GetProcessTreeWithTimeoutAsync(
            token =>
            {
                enumerationToken = token;

                return scenario == 0
                    ? throw failure
                    : scenario == 2
                    ? Task.FromCanceled<List<ProcessTreeNode>>(token)
                    : Task.FromException<List<ProcessTreeNode>>(failure);
            },
            Timeout.InfiniteTimeSpan,
            exception =>
            {
                reported.Add(exception);
                throw new InvalidOperationException("The enumeration error logger also failed.");
            },
            root,
            cancellation.Token);

        Assert.HasCount(1, actual);
        Assert.AreSame(root, actual[0].Process);
        Assert.AreEqual(0, actual[0].Level);
        Assert.HasCount(1, reported);
        if (scenario == 2)
        {
            Assert.IsInstanceOfType<OperationCanceledException>(reported[0]);
            Assert.AreEqual(enumerationToken, ((OperationCanceledException)reported[0]).CancellationToken);
            Assert.IsTrue(enumerationToken.IsCancellationRequested);
        }
        else
        {
            Assert.AreSame(failure, reported[0]);
        }
    }

    [TestMethod]
    public void Lookup_UnexpectedFailurePropagatesUnchanged()
    {
        var failure = new InvalidOperationException("The process provider failed for a reason other than an exited PID.");
        var processHandler = new Mock<IProcessHandler>(MockBehavior.Strict);
        processHandler.Setup(x => x.GetProcessById(110)).Throws(failure);

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(
            () => HangDumpProcessLifetimeHandler.TryGetProcessById(processHandler.Object, 110));

        Assert.AreSame(failure, actual);
        processHandler.Verify(x => x.GetProcessById(110), Times.Once);
        processHandler.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(false, DisplayName = "InProgressQuery_NonemptyReplyPreservesSnapshot")]
    [DataRow(true, DisplayName = "InProgressQuery_EmptyReplyIsSuccessful")]
    public async Task Query_HealthyReplyPreservesSnapshotAndReleasesCancellationLink(bool empty)
    {
        (string, int)[] expected = empty ? [] : [("Suite.First", 41), ("Suite.Second", 7)];
        using var cancellation = new CancellationTokenSource();
        CancellationToken requestToken = default;
        CancellationTokenRegistration registration = default;
        int cancellationCallbacks = 0;
        int diagnosticCalls = 0;
        try
        {
            (string, int)[] actual = await HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
                token =>
                {
                    requestToken = token;
                    registration = token.Register(() => Interlocked.Increment(ref cancellationCallbacks));
                    return Task.FromResult(expected);
                },
                Timeout.InfiniteTimeSpan,
                _ =>
                {
                    diagnosticCalls++;
                    return Task.CompletedTask;
                },
                cancellation.Token);

            Assert.AreSame(expected, actual);
            Assert.IsTrue(requestToken.CanBeCanceled);
            Assert.AreEqual(0, diagnosticCalls);
            cancellation.Cancel();
            Assert.AreEqual(0, cancellationCallbacks, "The successful query must not retain an application-cancellation registration.");
            Assert.IsFalse(requestToken.IsCancellationRequested);
        }
        finally
        {
            registration.Dispose();
        }
    }

    [TestMethod]
    [DataRow(0, DisplayName = "InProgressQuery_SynchronousFailureCancelsRequest")]
    [DataRow(1, DisplayName = "InProgressQuery_AsynchronousFailureCancelsRequest")]
    [DataRow(2, DisplayName = "InProgressQuery_ThrowingCancellationCallbackIsBestEffort")]
    [DataRow(3, DisplayName = "InProgressQuery_FailingDiagnosticIsBestEffort")]
    public async Task Query_FailureCancelsRequestBeforeDiagnosticAndReturnsEmpty(int scenario)
    {
        var failure = new IOException("The consumer did not provide its running tests.");
        CancellationToken requestToken = default;
        CancellationTokenRegistration registration = default;
        int cancellationCallbacks = 0;
        int diagnosticCalls = 0;
        bool cancellationObservedByDiagnostic = false;
        Exception? reported = null;
        try
        {
            (string, int)[] actual = await HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
                token =>
                {
                    requestToken = token;
                    registration = token.Register(() =>
                    {
                        Interlocked.Increment(ref cancellationCallbacks);
                        if (scenario == 2)
                        {
                            throw new InvalidOperationException("The request cleanup callback failed.");
                        }
                    });
                    return scenario is 0 or 3 ? throw failure : Task.FromException<(string, int)[]>(failure);
                },
                Timeout.InfiniteTimeSpan,
                exception =>
                {
                    diagnosticCalls++;
                    reported = exception;
                    cancellationObservedByDiagnostic = requestToken.IsCancellationRequested && Volatile.Read(ref cancellationCallbacks) == 1;
                    return scenario == 3
                        ? Task.FromException(new InvalidOperationException("The query error logger failed."))
                        : Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.IsEmpty(actual);
            Assert.AreSame(failure, reported);
            Assert.AreEqual(1, diagnosticCalls);
            Assert.AreEqual(1, cancellationCallbacks);
            Assert.IsTrue(cancellationObservedByDiagnostic, "Cleanup must be requested before reporting the failed query.");
        }
        finally
        {
            registration.Dispose();
        }
    }

    [TestMethod]
    public async Task Query_CanceledCallerReachesRequestAndFallsBackToEmpty()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        CancellationToken requestToken = default;
        Exception? reported = null;

        (string, int)[] actual = await HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
            token =>
            {
                requestToken = token;
                return Task.FromCanceled<(string, int)[]>(token);
            },
            Timeout.InfiniteTimeSpan,
            exception =>
            {
                reported = exception;
                return Task.CompletedTask;
            },
            cancellation.Token);

        Assert.IsEmpty(actual);
        Assert.IsTrue(requestToken.IsCancellationRequested);
        Assert.IsInstanceOfType<OperationCanceledException>(reported);
        Assert.AreEqual(requestToken, ((OperationCanceledException)reported).CancellationToken);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Diagnostic_SynchronousFailureIsSwallowed")]
    [DataRow(1, DisplayName = "Diagnostic_AsynchronousFailureIsSwallowed")]
    [DataRow(2, DisplayName = "Diagnostic_CancellationIsSwallowed")]
    public async Task Diagnostic_FailureNeverEscapes(int scenario)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int calls = 0;

        await HangDumpProcessLifetimeHandler.RunBestEffortDiagnosticAsync(
            () =>
            {
                calls++;
                return scenario == 0
                    ? throw new IOException("The diagnostic failed synchronously.")
                    : scenario == 1
                    ? Task.FromException(new IOException("The diagnostic failed asynchronously."))
                    : Task.FromCanceled(cancellation.Token);
            },
            Timeout.InfiniteTimeSpan);

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Linux, IgnoreMessage = "The createdump quoting workaround is Windows-only; Linux must receive the literal path.")]
    public void DumpFileNames_LinuxSpaceContainingPathIsNotQuoted()
    {
        const string PathWithSpaces = "/results directory/nested test/testhost_110_hang.dmp";

        HangDumpProcessLifetimeHandler.DumpFileNames actual = HangDumpProcessLifetimeHandler.GetDumpFileNames(PathWithSpaces);

        Assert.AreEqual(PathWithSpaces, actual.WriteDumpFileName);
        Assert.AreEqual(PathWithSpaces, actual.ArtifactDumpFileName);
    }

    [TestMethod]
    [DataRow("2147483647", int.MaxValue, DisplayName = "ProcStat_MaximumSignedParentId")]
    [DataRow("-2147483648", int.MinValue, DisplayName = "ProcStat_MinimumSignedParentId")]
    [DataRow("+42", 42, DisplayName = "ProcStat_ExplicitPositiveSign")]
    [DataRow("2147483648", -1, DisplayName = "ProcStat_PositiveOverflowIsInvalid")]
    [DataRow("-2147483649", -1, DisplayName = "ProcStat_NegativeOverflowIsInvalid")]
    [DataRow("42tail", -1, DisplayName = "ProcStat_TrailingNonNumericContentIsInvalid")]
    public void ProcStat_ParentIdUsesInvariantSignedIntegerAndRejectsOverflow(string parentField, int expected)
    {
        string stat = $"93 (worker (with spaces)) S {parentField} 93 2 4294967295";

        Assert.AreEqual(expected, IProcessExtensions.ParseParentPidFromProcStat(stat));
    }

    private static IProcess CreateProcess(int id, string name)
    {
        var process = new Mock<IProcess>(MockBehavior.Strict);
        process.SetupGet(x => x.Id).Returns(id);
        process.SetupGet(x => x.Name).Returns(name);
        return process.Object;
    }
}
