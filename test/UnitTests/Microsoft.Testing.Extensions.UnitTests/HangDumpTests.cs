// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Helpers;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HangDumpTests
{
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    public TestContext TestContext { get; set; } = null!;

    private HangDumpCommandLineProvider GetProvider()
    {
        var testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        _ = testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("FullPath");
        return new();
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainer pipe authorization is Windows-only.")]
    public async Task BeforeTestHostProcessStartAsync_UsesControllerAuthorizedSecurityIdentities()
    {
        var endpoint = new NamedPipeServerEndpoint($"hang_{Guid.NewGuid():N}");
        var options = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [HangDumpCommandLineProvider.HangDumpOptionName] = [],
        });
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        Mock<ITask> task = new();
        task.Setup(x => x.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
        ServiceProvider serviceProvider = new()
        {
            TestHostControllerAuthorizedSecurityIdentities = [ContosoPackageSid],
        };

        using var handler = new HangDumpProcessLifetimeHandler(
            endpoint,
            new Mock<IMessageBus>().Object,
            new Mock<IOutputDevice>().Object,
            options,
            task.Object,
            new Mock<IEnvironment>().Object,
            loggerFactory.Object,
            new Mock<IConfiguration>().Object,
            new Mock<IProcessHandler>().Object,
            clock.Object,
            serviceProvider);

        await handler.BeforeTestHostProcessStartAsync(TestContext.CancellationToken);

        Assert.IsTrue(endpoint.PipeName.StartsWith(@"LOCAL\", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task IsValid_If_Timeout_Value_Has_CorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["32"]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_Timeout_Value_Has_IncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTimeoutOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(ExtensionResources.HangDumpTimeoutOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
#if NETCOREAPP
    [DataRow("Triage")]
#endif
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    [DataRow("None")]
    public async Task IsValid_If_HangDumpType_Has_CorrectValue(string dumpType)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_HangDumpType_Has_IncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(
            string.Format(
                CultureInfo.InvariantCulture,
                ExtensionResources.HangDumpTypeOptionInvalidType,
                "invalid",
                GetExpectedFormattedOptions()),
            validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public void HangDumpTypeOptionDescription_ListsValidValues()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeOptionName);

        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeOptionDescription, GetExpectedFormattedOptions()),
            option.Description);
    }

    [TestMethod]
    [DataRow("hang.dmp", "hang_%p.dmp")]
    [DataRow("hang", "hang_%p")]
    [DataRow("subdirectory/hang.dmp", "subdirectory/hang_%p.dmp")]
    [DataRow("hang_%p.dmp", "hang_%p.dmp")]
    [DataRow("hang_{pid}.dmp", "hang_{pid}.dmp")]
    public void EnsureProcessIdPlaceholder_MakesCustomDumpPathUnique(string pattern, string expected)
        => Assert.AreEqual(
            expected.Replace('/', Path.DirectorySeparatorChar),
            HangDumpProcessLifetimeHandler.EnsureProcessIdPlaceholder(pattern));

    [TestMethod]
    [DataRow(null, "testhost", 123, 123, "testhost_%p_hang.dmp")]
    [DataRow("hang.dmp", "testhost", 123, 123, "hang.dmp")]
    [DataRow("hang.dmp", "child", 456, 123, "hang_%p.dmp")]
    [DataRow("hang_%p.dmp", "child", 456, 123, "hang_%p.dmp")]
    public void GetDumpFileNamePattern_PreservesRootNameAndMakesChildNamesUnique(
        string? configuredPattern,
        string processName,
        int processId,
        int rootProcessId,
        string expected)
        => Assert.AreEqual(
            expected,
            HangDumpProcessLifetimeHandler.GetDumpFileNamePattern(configuredPattern, processName, processId, rootProcessId));

    [TestMethod]
    public void TryGetProcessById_WhenProcessHasExited_ReturnsNull()
    {
        Mock<IProcessHandler> processHandler = new();
        processHandler.Setup(x => x.GetProcessById(123)).Throws<ArgumentException>();

        Assert.IsNull(HangDumpProcessLifetimeHandler.TryGetProcessById(processHandler.Object, 123));
    }

    [TestMethod]
    [DataRow(HangDumpCommandLineProvider.HangDumpFileNameOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTimeoutOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeOptionName)]
    public async Task Missing_HangDumpMainOption_ShouldReturn_IsInvalid(string hangDumpArgument)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { hangDumpArgument, [] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options));
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual("You specified one or more hang dump parameters but did not enable it, add --hangdump to the command line", validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(HangDumpCommandLineProvider.HangDumpFileNameOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTimeoutOptionName)]
    [DataRow(HangDumpCommandLineProvider.HangDumpTypeOptionName)]
    public async Task If_HangDumpMainOption_IsSpecified_ShouldReturn_IsValid(string hangDumpArgument)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { hangDumpArgument, [] },
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options));
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates Windows-specific quoting workaround for dotnet/diagnostics#5020")]
    public void GetDumpFileNames_WindowsPathWithSpaces_QuotesOnlyWriteDumpArgument()
    {
        string dumpFileName = @"C:\results directory with spaces\hangdump.dmp";

        HangDumpProcessLifetimeHandler.DumpFileNames dumpFileNames = HangDumpProcessLifetimeHandler.GetDumpFileNames(dumpFileName);

        Assert.AreEqual($"\"{dumpFileName}\"", dumpFileNames.WriteDumpFileName);
        Assert.AreEqual(dumpFileName, dumpFileNames.ArtifactDumpFileName);
    }

    [TestMethod]
    public async Task QueryOnceAndDumpTree_WithStalledQuery_QueriesOncePerDumpAndStillDumpsWholeTree()
    {
        // A wedged test host never answers the in-progress-test query, so the query costs a full
        // InProgressTestsQueryTimeout. Issuing it per process would multiply that bound by the size of
        // the tree, so a six-process tree must still pay it exactly once and then dump every process.
        int queryCount = 0;
        IProcess[] bottomUpTree = [.. Enumerable.Range(0, 6).Select(_ => Mock.Of<IProcess>())];
        ConcurrentQueue<IProcess> dumped = [];
        ConcurrentQueue<(string, int)[]> annotations = [];
        (string, int)[] expectedAnnotations = [];

        await HangDumpProcessLifetimeHandler.QueryOnceAndDumpTreeAsync(
            bottomUpTree,
            new SystemTask(),
            cancellationToken =>
            {
                Interlocked.Increment(ref queryCount);

                // The real bounded query, against a reply that never arrives: the product's own bound
                // cancels the wait and the dump proceeds with an empty list.
                return HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
                    async queryCancellationToken =>
                    {
                        await Task.Delay(Timeout.Infinite, queryCancellationToken);
                        return expectedAnnotations;
                    },
                    TimeSpan.FromMilliseconds(50),
                    _ => Task.CompletedTask,
                    cancellationToken);
            },
            (process, inProgressTests, _) =>
            {
                dumped.Enqueue(process);
                annotations.Enqueue(inProgressTests);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.AreEqual(1, queryCount);
        Assert.HasCount(bottomUpTree.Length, dumped);
        foreach (IProcess process in bottomUpTree)
        {
            Assert.Contains(process, dumped);
        }

        // Every dump is annotated with the answer from that one query, so no process triggers another.
        Assert.HasCount(bottomUpTree.Length, annotations);
        foreach ((string, int)[] annotation in annotations)
        {
            Assert.AreSame(expectedAnnotations, annotation);
        }
    }

    [TestMethod]
    public async Task QueryOnceAndDumpTree_StartsEveryDumpBeforeAwaitingCompletion()
    {
        IProcess[] bottomUpTree = [.. Enumerable.Range(0, 6).Select(_ => Mock.Of<IProcess>())];
        TaskCompletionSource<bool> allDumpsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releaseDumps = new();
        int startedDumpCount = 0;

        Task dumpTreeTask = HangDumpProcessLifetimeHandler.QueryOnceAndDumpTreeAsync(
            bottomUpTree,
            new SystemTask(),
            _ => Task.FromResult<(string, int)[]>([]),
            (_, _, _) =>
            {
                if (Interlocked.Increment(ref startedDumpCount) == bottomUpTree.Length)
                {
                    allDumpsStarted.TrySetResult(true);
                }

                releaseDumps.Wait(TestContext.CancellationToken);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        try
        {
            Task completed = await Task.WhenAny(allDumpsStarted.Task, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            Assert.AreSame(allDumpsStarted.Task, completed, "A dump was awaited before the remaining process dumps were started.");
            Assert.IsFalse(dumpTreeTask.IsCompleted);
        }
        finally
        {
            releaseDumps.Set();
        }

        await dumpTreeTask;

        Assert.AreEqual(bottomUpTree.Length, startedDumpCount);
    }

    [TestMethod]
    public async Task QueryInProgressTestsWithTimeout_WhenTheReplyNeverArrives_ReturnsEmptyList()
    {
        // A connected-but-wedged host accepts the request and never replies, and the application token is
        // not cancelled while the run is still in progress -- which is exactly when the deadline dump
        // fires. So the bound inside the product is the only thing that can end this wait: the request
        // here honors the token it is handed and nothing else, and never times out on its own.
        Exception? loggedFailure = null;

        Task<(string, int)[]> query = HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
            async queryCancellationToken =>
            {
                await Task.Delay(Timeout.Infinite, queryCancellationToken);
                return [];
            },
            TimeSpan.FromMilliseconds(200),
            ex =>
            {
                loggedFailure = ex;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Fail with a message rather than hanging the run if the bound is ever removed.
        Task completed = await Task.WhenAny(query, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreSame(query, completed, "The query did not give up on a reply that never arrives, so a wedged host would block the dump.");

        Assert.IsEmpty(await query);

        // The give-up is reported, so a missing in-progress-test list in a dump can be explained.
        Assert.IsNotNull(loggedFailure);
    }

    [TestMethod]
    public async Task QueryInProgressTestsWithTimeout_WhenTheReplyIgnoresCancellation_ReturnsEmptyList()
    {
        TaskCompletionSource<bool> neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? loggedFailure = null;

        try
        {
            Task<(string, int)[]> query = HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
                async _ =>
                {
                    await neverCompletes.Task;
                    return [];
                },
                TimeSpan.FromMilliseconds(50),
                ex =>
                {
                    loggedFailure = ex;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Task completed = await Task.WhenAny(query, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            Assert.AreSame(query, completed, "A query that ignores cancellation blocked the dump.");
            Assert.IsEmpty(await query);
            Assert.IsNotNull(loggedFailure);
        }
        finally
        {
            neverCompletes.TrySetResult(true);
        }
    }

    [TestMethod]
    public async Task QueryInProgressTestsWithTimeout_WhenTheHostReplies_ReturnsTheAnswer()
    {
        // The bound must not get in the way of the healthy path, which answers in milliseconds.
        (string, int)[] expected = [("Test1", 3), ("Test2", 7)];

        (string, int)[] inProgressTests = await HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
            _ => Task.FromResult(expected),
            TimeSpan.FromSeconds(30),
            _ => Task.CompletedTask,
            CancellationToken.None);

        Assert.AreSequenceEqual(expected, inProgressTests);
    }

    [TestMethod]
    public async Task QueryInProgressTestsWithTimeout_WhenReportingTheFailureThrows_StillReturnsEmptyList()
    {
        // The empty list is what lets the dump go ahead after a failed query, and the delegate that reports
        // the failure is a logger call -- logger providers can fail. If that throw escaped, a query failure
        // would take the dump down with it, even though the query is explicitly best-effort.
        (string, int)[] inProgressTests = await HangDumpProcessLifetimeHandler.QueryInProgressTestsWithTimeoutAsync(
            _ => throw new InvalidOperationException("The consumer pipe is not connected."),
            TimeSpan.FromSeconds(30),
            _ => throw new InvalidOperationException("This logger provider is broken too."),
            CancellationToken.None);

        Assert.IsEmpty(inProgressTests);
    }

    [TestMethod]
    public async Task RunBestEffortDiagnostic_WhenDiagnosticNeverCompletes_ReturnsAfterTimeout()
    {
        TaskCompletionSource<bool> neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task diagnostic = HangDumpProcessLifetimeHandler.RunBestEffortDiagnosticAsync(
            () => neverCompletes.Task,
            TimeSpan.FromMilliseconds(50));

        Task completed = await Task.WhenAny(diagnostic, Task.Delay(TimeSpan.FromSeconds(30), TestContext.CancellationToken));

        Assert.AreSame(diagnostic, completed);
        await diagnostic;
    }

    [TestMethod]
    public async Task GetProcessTreeWithTimeout_WhenEnumerationNeverCompletes_FallsBackToRootProcess()
    {
        TaskCompletionSource<bool> neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IProcess rootProcess = Mock.Of<IProcess>();

        try
        {
            List<ProcessTreeNode> processTree = await HangDumpProcessLifetimeHandler.GetProcessTreeWithTimeoutAsync(
                async _ =>
                {
                    await neverCompletes.Task;
                    return [];
                },
                TimeSpan.FromMilliseconds(50),
                _ => Task.CompletedTask,
                rootProcess,
                TestContext.CancellationToken);

            Assert.HasCount(1, processTree);
            Assert.AreSame(rootProcess, processTree[0].Process);
        }
        finally
        {
            neverCompletes.TrySetResult(true);
        }
    }

    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    [DataRow("Triage")]
    [DataRow("None")]
    public async Task IsValid_If_HangDumpTypeIfSupported_HasAnyKnownValue_RegardlessOfTfm(string dumpType)
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, [dumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_HangDumpTypeIfSupported_HasIncorrectValue()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        // The "-if-supported" variant lists the full set of dump types in its error so the user
        // is not misled into thinking values like Triage are unavailable on the current runtime.
        Assert.AreEqual(
            string.Format(
                CultureInfo.InvariantCulture,
                ExtensionResources.HangDumpTypeOptionInvalidType,
                "invalid",
                "'Mini', 'Heap', 'Full', 'Triage', 'None'"),
            validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task Missing_HangDumpMainOption_WithHangDumpTypeIfSupported_ShouldReturn_IsInvalid()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, ["Mini"] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(ExtensionResources.MissingHangDumpMainOption, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task If_HangDumpTypeIfSupported_IsSpecified_WithHangDump_ShouldReturn_IsValid()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, ["Triage"] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task ActivityIndicator_ExecutionCompleted_RemovesInProgressTest()
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
        using var indicator = new HangDumpActivityIndicator(
            new TestCommandLineOptions([]),
            Mock.Of<IEnvironment>(),
            Mock.Of<ITask>(),
            loggerFactory.Object,
            clock.Object);
        typeof(HangDumpActivityIndicator)
            .GetField("_exitSignalActivityIndicatorAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(indicator, true);

        await indicator.ConsumeAsync(null!, CreateUpdate(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        Assert.AreEqual(1, GetInProgressCount(indicator));

        await indicator.ConsumeAsync(null!, CreateUpdate(TestNodeExecutionCompletedProperty.CachedInstance), CancellationToken.None);

        Assert.AreEqual(0, GetInProgressCount(indicator));
    }

    [TestMethod]
    public async Task HangDumpType_And_HangDumpTypeIfSupported_AreMutuallyExclusive()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        var options = new Dictionary<string, string[]>
        {
            { HangDumpCommandLineProvider.HangDumpOptionName, [] },
            { HangDumpCommandLineProvider.HangDumpTypeOptionName, ["Mini"] },
            { HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, ["Heap"] },
        };

        ValidationResult validateOptionsResult = await hangDumpCommandLineProvider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(ExtensionResources.HangDumpTypeAndIfSupportedAreMutuallyExclusiveErrorMessage, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public void HangDumpTypeIfSupportedOption_IsRegisteredWithExactlyOneArity()
    {
        HangDumpCommandLineProvider hangDumpCommandLineProvider = GetProvider();
        CommandLineOption option = hangDumpCommandLineProvider.GetCommandLineOptions().First(x => x.Name == HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName);
        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
    }

    [TestMethod]
    public void IsHangDumpTypeSupportedOnCurrentRuntime_ReturnsTrue_ForAlwaysAvailableTypes()
    {
        // Mini/Heap/Full/None are supported on every runtime the platform targets, so the
        // -if-supported variant must never trigger a runtime fallback for these values.
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Mini"));
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Heap"));
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Full"));
        Assert.IsTrue(HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("None"));
    }

    private static TestNodeUpdateMessage CreateUpdate(IProperty property)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "uid",
                DisplayName = "DroppedTest",
                Properties = new PropertyBag(property),
            });

    private static int GetInProgressCount(HangDumpActivityIndicator indicator)
    {
        object state = typeof(HangDumpActivityIndicator)
            .GetField("_testsCurrentExecutionState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(indicator)!;
        return (int)state.GetType().GetProperty("Count")!.GetValue(state)!;
    }

    [TestMethod]
    public void IsHangDumpTypeSupportedOnCurrentRuntime_TriageMatchesCurrentTfm()
    {
        // 'Triage' is only available on .NET (Core), because the .NET Framework hang dump
        // path goes through MiniDumpWriteDump which has no equivalent flag.
#if NETCOREAPP
        bool expected = true;
#else
        bool expected = false;
#endif
        Assert.AreEqual(expected, HangDumpCommandLineProvider.IsHangDumpTypeSupportedOnCurrentRuntime("Triage"));
    }

    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Full")]
    [DataRow("None")]
    public void MapToSupportedDumpType_ReturnsRequested_WhenAlreadySupported(string value)
        // Supported values must round-trip unchanged so the user does not get a surprise
        // substitution when the runtime can actually honor their request.
        => Assert.AreEqual(value, HangDumpCommandLineProvider.MapToSupportedDumpType(value));

    [TestMethod]
    public void MapToSupportedDumpType_TriageOnNetFramework_FallsBackToMini()
        // The mapping intentionally prefers a "closest in size/intent" fallback over the
        // global default 'Full' so a user asking for the lightest dump does not end up with
        // the heaviest one. 'Mini' is the closest .NET Framework equivalent of 'Triage'.
#if NETCOREAPP
        => Assert.AreEqual("Triage", HangDumpCommandLineProvider.MapToSupportedDumpType("Triage"));
#else
        => Assert.AreEqual("Mini", HangDumpCommandLineProvider.MapToSupportedDumpType("Triage"));
#endif

#if NETCOREAPP
    private static string GetExpectedFormattedOptions() => "'Mini', 'Heap', 'Full', 'Triage', 'None'";
#else
    private static string GetExpectedFormattedOptions() => "'Mini', 'Heap', 'Full', 'None'";
#endif
}
