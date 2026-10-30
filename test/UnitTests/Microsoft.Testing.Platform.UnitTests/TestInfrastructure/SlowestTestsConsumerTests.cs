// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.TestInfrastructure;

[TestClass]
public sealed class SlowestTestsConsumerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WritesIndentedDurationFirstEntries()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");

        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("FasterTest", TimeSpan.FromSeconds(1.25)), CancellationToken.None);
        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("SlowerTest", TimeSpan.FromSeconds(12)), CancellationToken.None);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.AreEqual(
            """
            Slowest 10 tests — Module (net8.0):
              12.00s [passed] SlowerTest
              1.25s [passed] FasterTest
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WritesOnlyTenSlowestEntries()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");

        for (int i = 0; i < 11; i++)
        {
            await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate($"Test{i}", TimeSpan.FromSeconds(i + 1)), CancellationToken.None);
        }

        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.AreEqual(
            """
            Slowest 10 tests — Module (net8.0):
              11.00s [passed] Test10
              10.00s [passed] Test9
              9.00s [passed] Test8
              8.00s [passed] Test7
              7.00s [passed] Test6
              6.00s [passed] Test5
              5.00s [passed] Test4
              4.00s [passed] Test3
              3.00s [passed] Test2
              2.00s [passed] Test1
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    [TestMethod]
    [DataRow("passed", "passed", DisplayName = "SlowestTerminalPassed")]
    [DataRow("failed", "failed", DisplayName = "SlowestTerminalFailed")]
    [DataRow("error", "failed", DisplayName = "SlowestTerminalError")]
    [DataRow("timeout", "failed", DisplayName = "SlowestTerminalTimeout")]
    [DataRow("cancelled", "failed", DisplayName = "SlowestTerminalCancelled")]
    [DataRow("skipped", "skipped", DisplayName = "SlowestTerminalSkipped")]
    public async Task ConsumeAsync_RecordsEveryTerminalOutcome(string state, string expectedOutcome)
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");
#pragma warning disable CS0618, MTP0001 // The consumer must still recognize the obsolete terminal cancellation state.
        TestNodeStateProperty property = state switch
        {
            "passed" => PassedTestNodeStateProperty.CachedInstance,
            "failed" => new FailedTestNodeStateProperty(),
            "error" => new ErrorTestNodeStateProperty(),
            "timeout" => new TimeoutTestNodeStateProperty(),
            "cancelled" => new CancelledTestNodeStateProperty(),
            "skipped" => SkippedTestNodeStateProperty.CachedInstance,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
#pragma warning restore CS0618, MTP0001

        await consumer.ConsumeAsync(null!, CreateTestNodeUpdate("Test", property, TimeSpan.FromSeconds(1.25)), CancellationToken.None);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.AreEqual(
            $"""
            Slowest 10 tests — Module (net8.0):
              1.25s [{expectedOutcome}] Test
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    [TestMethod]
    public async Task ConsumeAsync_IgnoresMissingNonpositiveAndNonterminalTimings()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add);
        TestNodeUpdateMessage[] ignored =
        [
            CreateTestNodeUpdate("MissingTiming", PassedTestNodeStateProperty.CachedInstance),
            CreateTestNodeUpdate("ZeroTiming", PassedTestNodeStateProperty.CachedInstance, TimeSpan.Zero),
            CreateTestNodeUpdate("NegativeTiming", PassedTestNodeStateProperty.CachedInstance, TimeSpan.FromTicks(-1)),
            CreateTestNodeUpdate("Discovered", DiscoveredTestNodeStateProperty.CachedInstance, TimeSpan.FromSeconds(30)),
            CreateTestNodeUpdate("InProgress", InProgressTestNodeStateProperty.CachedInstance, TimeSpan.FromSeconds(30)),
            CreateTestNodeUpdate("MissingState", state: null, TimeSpan.FromSeconds(30)),
        ];

        foreach (TestNodeUpdateMessage update in ignored)
        {
            await consumer.ConsumeAsync(null!, update, CancellationToken.None);
        }

        await consumer.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task ConsumeAsync_ExcludesSupersededRetryButKeepsFinalTiming()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");
        await consumer.ConsumeAsync(null!, CreateTestNodeUpdate(
            "Retry", new FailedTestNodeStateProperty(), TimeSpan.FromSeconds(30),
            new RetryAttemptProperty(1, isSuperseded: true)), CancellationToken.None);
        await consumer.ConsumeAsync(null!, CreateTestNodeUpdate(
            "Retry", PassedTestNodeStateProperty.CachedInstance, TimeSpan.FromSeconds(2),
            new RetryAttemptProperty(2, isSuperseded: false)), CancellationToken.None);

        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.AreEqual(
            """
            Slowest 10 tests — Module (net8.0):
              2.00s [passed] Retry
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_ClearsPreviousSessionTimings()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");
        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("Previous", TimeSpan.FromSeconds(30)), CancellationToken.None);

        await consumer.OnTestSessionStartingAsync(null!);
        await consumer.OnTestSessionFinishingAsync(null!);
        Assert.IsEmpty(lines);

        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("Current", TimeSpan.FromSeconds(1)), CancellationToken.None);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.AreEqual(
            """
            Slowest 10 tests — Module (net8.0):
              1.00s [passed] Current
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_EmptySessionWritesNothing()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add);

        await consumer.OnTestSessionStartingAsync(null!);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task DisabledConsumer_IgnoresTimingsAndWritesNothing()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, isEnabled: false);

        Assert.IsFalse(await consumer.IsEnabledAsync());
        await consumer.OnTestSessionStartingAsync(null!);
        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("Hidden", TimeSpan.FromSeconds(2)), CancellationToken.None);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.IsEmpty(lines);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "AzdoSummaryDisablesLocalTimings")]
    [DataRow(false, DisplayName = "NoAzdoSummaryKeepsLocalTimings")]
    public async Task CommandLineConstructor_DisablesOnlyWhenAzureDevOpsSummaryIsSet(bool reportSummary)
    {
        var options = new Mock<ICommandLineOptions>(MockBehavior.Strict);
        options.Setup(value => value.IsOptionSet("report-azdo-summary")).Returns(reportSummary);
        var consumer = new SlowestTestsConsumer(options.Object);

        Assert.AreEqual(!reportSummary, await consumer.IsEnabledAsync());
    }

    [TestMethod]
    public async Task ParameterlessConstructor_RemainsEnabled()
        => Assert.IsTrue(await new SlowestTestsConsumer().IsEnabledAsync());

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_OrdersEqualDurationsByOrdinalName()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");
        foreach (string name in new[] { "z", "a", "B", "A" })
        {
            await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate(name, TimeSpan.FromSeconds(2)), CancellationToken.None);
        }

        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.AreEqual(
            """
            Slowest 10 tests — Module (net8.0):
              2.00s [passed] A
              2.00s [passed] B
              2.00s [passed] a
              2.00s [passed] z
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_UsesInvariantTwoDecimalSeconds()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // CurrentCulture is ExecutionContext-local on every target here, including net462.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            List<string> lines = [];
            var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");
            await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("Fraction", TimeSpan.FromMilliseconds(1254)), CancellationToken.None);
            await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("Tick", TimeSpan.FromTicks(1)), CancellationToken.None);

            await consumer.OnTestSessionFinishingAsync(null!);

            Assert.AreEqual(
                """
                Slowest 10 tests — Module (net8.0):
                  1.25s [passed] Fraction
                  0.00s [passed] Tick
                """.Replace("\r\n", "\n"),
                string.Join("\n", lines));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_DefaultContextUsesEntryAssemblyAndFramework()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add);
        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("Test", TimeSpan.FromSeconds(1)), CancellationToken.None);

        await consumer.OnTestSessionFinishingAsync(null!);

        Assembly entryAssembly = Assembly.GetEntryAssembly()!;
        string framework = entryAssembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!.FrameworkName;
        Assert.AreEqual(
            $"""
            Slowest 10 tests — {entryAssembly.GetName().Name} ({framework}):
              1.00s [passed] Test
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    [TestMethod]
    public async Task ConsumeAsync_ConcurrentMessagesRetainEveryTopTenEntry()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add, moduleName: "Module", targetFramework: "net8.0");
        Task[] writers =
        [
            .. Enumerable.Range(1, 10).Select(index => Task.Run(
                () => consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate($"Test{index}", TimeSpan.FromSeconds(index)), CancellationToken.None),
                TestContext.CancellationToken)),
        ];

        await Task.WhenAll(writers);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.AreEqual(
            """
            Slowest 10 tests — Module (net8.0):
              10.00s [passed] Test10
              9.00s [passed] Test9
              8.00s [passed] Test8
              7.00s [passed] Test7
              6.00s [passed] Test6
              5.00s [passed] Test5
              4.00s [passed] Test4
              3.00s [passed] Test3
              2.00s [passed] Test2
              1.00s [passed] Test1
            """.Replace("\r\n", "\n"),
            string.Join("\n", lines));
    }

    private static TestNodeUpdateMessage CreatePassedTestNodeUpdate(string displayName, TimeSpan duration)
        => CreateTestNodeUpdate(displayName, PassedTestNodeStateProperty.CachedInstance, duration);

    private static TestNodeUpdateMessage CreateTestNodeUpdate(
        string displayName,
        TestNodeStateProperty? state,
        TimeSpan? duration = null,
        RetryAttemptProperty? retry = null)
    {
        DateTimeOffset startTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var properties = new PropertyBag();
        if (state is not null)
        {
            properties.Add(state);
        }

        if (duration is { } elapsed)
        {
            properties.Add(new TimingProperty(new(startTime, startTime + elapsed, elapsed)));
        }

        if (retry is not null)
        {
            properties.Add(retry);
        }

        return new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = displayName,
                DisplayName = displayName,
                Properties = properties,
            });
    }
}
