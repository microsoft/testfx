// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxIncrementalWriterPrototypeTests
{
    private const string MachineName = "prototype-machine";
    private const string TestModule = "Prototype.Tests.dll";
    private const string FrameworkUid = "prototype-framework";
    private const string FrameworkVersion = "1.0.0";
    private const string RunName = "prototype-user@prototype-machine 2026-07-20 10:00:00.0000000";
    private const int CounterWidth = 10;

    private static readonly Guid RunId = new("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset StartTime = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FinishTime = new(2026, 7, 20, 10, 5, 0, TimeSpan.Zero);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly XNamespace Ns = TrxDocumentClassifier.TeamTest2010Namespace;
    private static readonly string[] PrototypeRootOrder =
        ["Times", "TestSettings", "TestDefinitions", "TestEntries", "TestLists", "ResultSummary", "Results"];

    [TestMethod]
    public void Initialize_AbsentTarget_WritesCompleteFailedZeroCounterDocument()
    {
        using var harness = new PrototypeHarness();
        Assert.IsFalse(File.Exists(harness.Path));

        harness.Writer.Initialize();

        AssertTruthful(harness.Bytes, [], [], StartTime, "Failed");
        XDocument document = Load(harness.Bytes);
        XElement counters = Required(document, "ResultSummary").Element(Ns + "Counters")!;
        foreach (XAttribute counter in counters.Attributes())
        {
            Assert.AreEqual(new string('0', CounterWidth), counter.Value, counter.Name.LocalName);
        }
    }

    [TestMethod]
    public void Initialize_PadsAreWhitespaceAndAllClosersArePresent()
    {
        const int definitionPad = 1_003;
        const int entryPad = 809;
        const int slotCount = 2;
        const int slotCapacity = 311;
        using var harness = new PrototypeHarness(
            definitionPadBytes: definitionPad,
            entryPadBytes: entryPad,
            runningSlotCount: slotCount,
            runningSlotByteCapacity: slotCapacity);

        harness.Writer.Initialize();

        XDocument document = Load(harness.Bytes);
        AssertWhitespacePad(Required(document, "TestDefinitions"), definitionPad);
        AssertWhitespacePad(Required(document, "TestEntries"), entryPad);
        AssertWhitespacePad(Required(document, "Results"), slotCount * slotCapacity);
        Assert.IsTrue(StrictUtf8.GetString(harness.Bytes).EndsWith("</Results></TestRun>", StringComparison.Ordinal));
        Assert.AreSequenceEqual(PrototypeRootOrder, document.Root!.Elements().Select(element => element.Name.LocalName).ToArray());
    }

    [TestMethod]
    public void AppendCompleted_OneAndManyResults_PreservesCompletionOrderAndClosers()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        TrxTestResult[] results =
        [
            CreateResult(1, "first", TrxTestOutcome.Passed),
            CreateResult(2, "second", TrxTestOutcome.Skipped),
            CreateResult(3, "third", TrxTestOutcome.Timeout),
        ];
        Guid[] executions = [ExecutionId(1), ExecutionId(2), ExecutionId(3)];

        harness.Writer.AppendCompleted(results[0], executions[0]);
        AssertResultOrder(harness.Bytes, "first");
        Assert.IsTrue(StrictUtf8.GetString(harness.Bytes).EndsWith("</Results></TestRun>", StringComparison.Ordinal));

        harness.Writer.AppendCompleted(results[1], executions[1]);
        harness.Writer.AppendCompleted(results[2], executions[2]);

        AssertResultOrder(harness.Bytes, "first", "second", "third");
        Assert.IsTrue(StrictUtf8.GetString(harness.Bytes).EndsWith("</Results></TestRun>", StringComparison.Ordinal));
        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults(results, executions),
            [],
            StartTime,
            "Failed");
    }

    [TestMethod]
    public void AppendCompleted_DefinitionAndEntryUseReservedPadsAndLinkExecutionIds()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        TrxTestResult result = CreateResult(1, "linked", TrxTestOutcome.Passed);
        Guid executionId = ExecutionId(1);

        harness.Writer.AppendCompleted(result, executionId);

        XDocument document = Load(harness.Bytes);
        XElement definition = Required(document, "TestDefinitions").Elements(Ns + "UnitTest").Single();
        XElement entry = Required(document, "TestEntries").Elements(Ns + "TestEntry").Single();
        XElement completed = CompletedResults(document).Single();
        Assert.AreEqual(completed.Attribute("testId")!.Value, definition.Attribute("id")!.Value);
        Assert.AreEqual(executionId.ToString(), definition.Element(Ns + "Execution")!.Attribute("id")!.Value);
        Assert.AreEqual(completed.Attribute("testId")!.Value, entry.Attribute("testId")!.Value);
        Assert.AreEqual(executionId.ToString(), entry.Attribute("executionId")!.Value);
        Assert.IsTrue(Required(document, "TestDefinitions").Nodes().OfType<XText>().All(text => IsWhitespace(text.Value)));
        Assert.IsTrue(Required(document, "TestEntries").Nodes().OfType<XText>().All(text => IsWhitespace(text.Value)));
    }

    [TestMethod]
    public void AppendCompleted_RepeatedTestId_DeduplicatesDefinitionButCreatesEntries()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        TrxTestResult first = CreateResult(1, "repeat[1]", TrxTestOutcome.Passed, "Repeated.Definition");
        TrxTestResult second = CreateResult(1, "repeat[2]", TrxTestOutcome.Passed, "Repeated.Definition");
        Guid firstExecution = ExecutionId(1);
        Guid secondExecution = ExecutionId(2);

        harness.Writer.AppendCompleted(first, firstExecution);
        harness.Writer.AppendCompleted(second, secondExecution);

        XDocument document = Load(harness.Bytes);
        Assert.HasCount(1, Required(document, "TestDefinitions").Elements(Ns + "UnitTest"));
        Assert.HasCount(2, Required(document, "TestEntries").Elements(Ns + "TestEntry"));
        Assert.AreEqual(
            firstExecution.ToString(),
            Required(document, "TestDefinitions").Element(Ns + "UnitTest")!.Element(Ns + "Execution")!.Attribute("id")!.Value);
        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults([first, second], [firstExecution, secondExecution]),
            [],
            StartTime,
            "Failed");
    }

    [TestMethod]
    public void Counters_Transitions0To1_9To10_99To100AndLargeValuesRemainTruthful()
    {
        using var harness = new PrototypeHarness(entryPadBytes: 40_000);
        harness.Writer.Initialize();
        var results = new List<TrxTestResult>();
        var executions = new List<Guid>();

        for (int i = 1; i <= 100; i++)
        {
            TrxTestResult result = CreateResult(1, $"counter[{i}]", TrxTestOutcome.Passed, "Counter.Definition");
            Guid executionId = ExecutionId(i);
            results.Add(result);
            executions.Add(executionId);
            harness.Writer.AppendCompleted(result, executionId);

            if (i is 1 or 9 or 10 or 99 or 100)
            {
                XElement counters = Required(Load(harness.Bytes), "ResultSummary").Element(Ns + "Counters")!;
                Assert.AreEqual(i, int.Parse(counters.Attribute("total")!.Value, CultureInfo.InvariantCulture));
                Assert.AreEqual(i, int.Parse(counters.Attribute("executed")!.Value, CultureInfo.InvariantCulture));
                Assert.AreEqual(i, int.Parse(counters.Attribute("passed")!.Value, CultureInfo.InvariantCulture));
                Assert.AreEqual(CounterWidth, counters.Attribute("total")!.Value.Length);
            }
        }

        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults(results, executions),
            [],
            StartTime,
            "Failed");
    }

    [TestMethod]
    public void UpdateFinishTime_ControlledOffsetsAndRoundTripValues_RemainsTruthful()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        long initialLength = harness.Bytes.LongLength;
        DateTimeOffset first = new(2026, 7, 20, 18, 30, 45, TimeSpan.FromHours(5.5));
        DateTimeOffset second = new(2026, 7, 20, 15, 45, 30, TimeSpan.FromHours(-4));

        harness.Writer.UpdateFinishTime(first);
        Assert.AreEqual(first, ReadFinishTime(harness.Bytes));
        Assert.AreEqual(initialLength, harness.Bytes.LongLength);

        harness.Writer.UpdateFinishTime(second);
        Assert.AreEqual(second, ReadFinishTime(harness.Bytes));
        Assert.AreEqual(initialLength, harness.Bytes.LongLength);
        AssertTruthful(harness.Bytes, [], [], second, "Failed");
    }

    [TestMethod]
    public void Complete_FailedToCompleted_RewritesFixedOutcomeWithoutLengthShift()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        TrxTestResult result = CreateResult(1, "passing", TrxTestOutcome.Passed);
        Guid executionId = ExecutionId(1);
        harness.Writer.AppendCompleted(result, executionId);
        long paddedLength = harness.Bytes.LongLength;

        harness.Writer.Complete(new TrxPrototypeCompletion { FinishTime = FinishTime });

        Assert.AreEqual(paddedLength, harness.Bytes.LongLength);
        string xml = StrictUtf8.GetString(harness.Bytes);
        Assert.Contains("outcome=\"Completed\">", xml);
        Assert.DoesNotContain("outcome=\"Failed\"   >", xml);
        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults([result], [executionId]),
            [],
            FinishTime,
            "Completed");
    }

    [TestMethod]
    public void ClaimRunning_ClaimCompleteAndRelease_TracksExactRunningSet()
    {
        using var harness = new PrototypeHarness(runningSlotCount: 2);
        harness.Writer.Initialize();
        Guid firstExecution = ExecutionId(1);
        Guid secondExecution = ExecutionId(2);
        int firstSlot = harness.Writer.ClaimRunning(TestId(1), "running-one", firstExecution, StartTime);
        int secondSlot = harness.Writer.ClaimRunning(TestId(2), "running-two", secondExecution, StartTime.AddSeconds(1));

        AssertTruthful(
            harness.Bytes,
            [],
            [
                CreateExpectedRunning(1, firstExecution, "running-one"),
                CreateExpectedRunning(2, secondExecution, "running-two", StartTime.AddSeconds(1)),
            ],
            StartTime,
            "Failed");

        TrxTestResult second = CreateResult(2, "running-two", TrxTestOutcome.Passed);
        harness.Writer.AppendCompleted(second, secondExecution, secondSlot);
        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults([second], [secondExecution]),
            [CreateExpectedRunning(1, firstExecution, "running-one")],
            StartTime,
            "Failed");

        TrxTestResult first = CreateResult(1, "running-one", TrxTestOutcome.Passed);
        harness.Writer.AppendCompleted(first, firstExecution, firstSlot);
        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults([second, first], [secondExecution, firstExecution]),
            [],
            StartTime,
            "Failed");
    }

    [TestMethod]
    public void ClaimRunning_DuplicateStartOrCompletionWithoutClaim_IsDiagnosedBeforeMutation()
    {
        var operations = new TrxFaultInjectingFileOperations();
        using var harness = new PrototypeHarness(operations: operations, runningSlotCount: 2);
        harness.Writer.Initialize();
        Guid executionId = ExecutionId(1);
        _ = harness.Writer.ClaimRunning(TestId(1), "running", executionId, StartTime);
        int operationCount = operations.Operations.Count;
        byte[] before = harness.Bytes;

        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => harness.Writer.ClaimRunning(TestId(1), "duplicate", executionId, StartTime));
        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => harness.Writer.AppendCompleted(CreateResult(2, "not-claimed", TrxTestOutcome.Passed), ExecutionId(2), 1));

        Assert.HasCount(operationCount, operations.Operations);
        Assert.AreSequenceEqual(before, harness.Bytes);
    }

    [TestMethod]
    public void ClaimRunning_SlotNPlusOne_IsDiagnosedWithoutDamagingCompletedResults()
    {
        var operations = new TrxFaultInjectingFileOperations();
        using var harness = new PrototypeHarness(operations: operations, runningSlotCount: 1);
        harness.Writer.Initialize();
        TrxTestResult completed = CreateResult(1, "completed", TrxTestOutcome.Passed);
        Guid completedExecution = ExecutionId(1);
        harness.Writer.AppendCompleted(completed, completedExecution);
        Guid runningExecution = ExecutionId(2);
        _ = harness.Writer.ClaimRunning(TestId(2), "running", runningExecution, StartTime);
        int operationCount = operations.Operations.Count;
        byte[] before = harness.Bytes;

        _ = Assert.ThrowsExactly<InvalidOperationException>(
            () => harness.Writer.ClaimRunning(TestId(3), "overflow", ExecutionId(3), StartTime));

        Assert.HasCount(operationCount, operations.Operations);
        Assert.AreSequenceEqual(before, harness.Bytes);
        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults([completed], [completedExecution]),
            [CreateExpectedRunning(2, runningExecution, "running")],
            StartTime,
            "Failed");
    }

    [TestMethod]
    public void RunningSlot_LongUnicodeName_TruncatesAtScalarAndEncodedByteBoundary()
    {
        const int slotCapacity = 300;
        using var harness = new PrototypeHarness(runningSlotCount: 1, runningSlotByteCapacity: slotCapacity);
        harness.Writer.Initialize();
        string longName = string.Concat("prefix<&>é漢😀", new string('界', 100), "😀tail");
        Guid executionId = ExecutionId(1);

        _ = harness.Writer.ClaimRunning(TestId(1), longName, executionId, StartTime);

        XDocument document = Load(harness.Bytes);
        XElement running = Required(document, "Results").Elements(Ns + "UnitTestResult").Single();
        string truncated = running.Attribute("testName")!.Value;
        Assert.IsTrue(longName.StartsWith(truncated, StringComparison.Ordinal));
        Assert.IsLessThan(longName.Length, truncated.Length);
        Assert.AreNotEqual('\uFFFD', truncated[truncated.Length - 1]);
        Assert.IsFalse(char.IsHighSurrogate(truncated[truncated.Length - 1]));
        _ = StrictUtf8.GetString(harness.Bytes);
        AssertTruthful(
            harness.Bytes,
            [],
            [CreateExpectedRunning(1, executionId, truncated)],
            StartTime,
            "Failed");
    }

    [TestMethod]
    public void Unicode_EntitiesInvalidControlsAndLineBreaks_RoundTripWithCurrentSanitization()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        var result = new TrxTestResult
        {
            Uid = TestId(1),
            DisplayName = "name é漢😀 e\u0301 مرحبا <&\" control:\u0001",
            Outcome = TrxTestOutcome.Failed,
            StartTime = StartTime,
            EndTime = StartTime.AddSeconds(1),
            Duration = TimeSpan.FromSeconds(1),
            TrxTestDefinitionName = "definition é漢😀 <&\u0002",
            TrxFullyQualifiedTypeName = "Prototype.UnicodeTests",
            TestMethodIdentifier = new TrxTestMethodIdentifier
            {
                Namespace = "Prototype",
                TypeName = "UnicodeTests",
                MethodName = "RoundTrip",
            },
            ExceptionMessage = "message <&> 😀\u0003\r\nline",
            ExceptionStackTrace = "stack é漢\r\nline",
            Messages =
            [
                new TrxStreamMessage
                {
                    Kind = TrxStreamMessageKind.StandardOutput,
                    Message = "stdout <&> 😀\u0004\r\nline",
                },
            ],
            Metadata =
            [
                new TrxTestMetadata { Key = "Custom<&>", Value = "value é漢😀\u0005" },
            ],
        };

        harness.Writer.AppendCompleted(result, ExecutionId(1));

        XDocument document = Load(harness.Bytes);
        XElement completed = CompletedResults(document).Single();
        Assert.AreEqual("name é漢😀 e\u0301 مرحبا <&\" control:\\u0001", completed.Attribute("testName")!.Value);
        Assert.AreEqual(
            "stdout <&> 😀\\u0004\nline",
            completed.Element(Ns + "Output")!.Element(Ns + "StdOut")!.Value);
        Assert.AreEqual(
            "message <&> 😀\\u0003\nline",
            completed.Descendants(Ns + "Message").Single().Value);
        XElement property = Required(document, "TestDefinitions").Descendants(Ns + "Property").Single();
        Assert.AreEqual("Custom<&>", property.Element(Ns + "Key")!.Value);
        Assert.AreEqual("value é漢😀\\u0005", property.Element(Ns + "Value")!.Value);
    }

    [TestMethod]
    public void Metadata_OwnerDescriptionCategoriesAndCustomPropertiesOver500Bytes_UsesActualBytes()
    {
        string longValue = string.Concat("é漢😀<&>", new string('界', 260));
        TrxTestResult result = CreateResult(1, "metadata", TrxTestOutcome.Passed);
        result = CopyWithMetadata(
            result,
            ["fast", "unicode-漢"],
            [
                new TrxTestMetadata { Key = "Owner", Value = "Zoë <owner>" },
                new TrxTestMetadata { Key = "Description", Value = longValue },
                new TrxTestMetadata { Key = "Priority", Value = "7" },
                new TrxTestMetadata { Key = "Custom<&>", Value = longValue },
            ]);
        var renderer = new TrxPrototypeXmlRenderer(MachineName, TestModule, FrameworkUid, FrameworkVersion);
        byte[] definition = renderer.RenderDefinition(result, ExecutionId(1));
        Assert.IsGreaterThan(500, definition.Length);
        Assert.IsGreaterThan(longValue.Length, StrictUtf8.GetByteCount(longValue));

        using var harness = new PrototypeHarness(definitionPadBytes: definition.Length + 23);
        harness.Writer.Initialize();
        harness.Writer.AppendCompleted(result, ExecutionId(1));

        XDocument document = Load(harness.Bytes);
        XElement unitTest = Required(document, "TestDefinitions").Element(Ns + "UnitTest")!;
        Assert.AreEqual("Zoë <owner>", unitTest.Descendants(Ns + "Owner").Single().Attribute("name")!.Value);
        Assert.AreEqual(longValue, unitTest.Element(Ns + "Description")!.Value);
        Assert.AreSequenceEqual(
            ["fast", "unicode-漢"],
            unitTest.Descendants(Ns + "TestCategoryItem").Select(element => element.Attribute("TestCategory")!.Value).ToArray());
        Assert.AreEqual(longValue, unitTest.Descendants(Ns + "Value").Single().Value);
        Assert.AreEqual(23, unitTest.NodesAfterSelf().OfType<XText>().Sum(text => text.Value.Length));
    }

    [TestMethod]
    public void LargeOutput_OneLargeAndManyAggregateMessages_DoesNotConsumeDefinitionOrEntryPads()
    {
        string oneLargeMessage = new('x', (1024 * 1024) - 128);
        TrxTestResult first = CopyWithMessages(
            CreateResult(1, "one-large", TrxTestOutcome.Passed),
            [new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = oneLargeMessage }]);
        TrxStreamMessage[] manyMessages = Enumerable.Range(0, 80)
            .Select(index => new TrxStreamMessage
            {
                Kind = TrxStreamMessageKind.StandardError,
                Message = $"{index:D2}:{new string('y', 4_096)}",
            })
            .ToArray();
        TrxTestResult second = CopyWithMessages(CreateResult(2, "many-large", TrxTestOutcome.Passed), manyMessages);
        var renderer = new TrxPrototypeXmlRenderer(MachineName, TestModule, FrameworkUid, FrameworkVersion);
        int definitionBytes = renderer.RenderDefinition(first, ExecutionId(1)).Length
            + renderer.RenderDefinition(second, ExecutionId(2)).Length;
        int entryBytes = TrxPrototypeXmlRenderer.RenderEntry(first.Uid, ExecutionId(1)).Length
            + TrxPrototypeXmlRenderer.RenderEntry(second.Uid, ExecutionId(2)).Length;
        using var harness = new PrototypeHarness(
            definitionPadBytes: definitionBytes,
            entryPadBytes: entryBytes);
        harness.Writer.Initialize();

        harness.Writer.AppendCompleted(first, ExecutionId(1));
        harness.Writer.AppendCompleted(second, ExecutionId(2));

        XDocument document = Load(harness.Bytes);
        XElement[] completed = CompletedResults(document).ToArray();
        Assert.AreEqual(oneLargeMessage, completed[0].Descendants(Ns + "StdOut").Single().Value);
        Assert.AreEqual(
            string.Join("\n", manyMessages.Select(message => message.Message)),
            completed[1].Descendants(Ns + "StdErr").Single().Value);
        Assert.AreEqual(0, Required(document, "TestDefinitions").Nodes().OfType<XText>().Sum(text => text.Value.Length));
        Assert.AreEqual(0, Required(document, "TestEntries").Nodes().OfType<XText>().Sum(text => text.Value.Length));
    }

    [TestMethod]
    public void LargeOutput_PreservesStdOutStdErrDebugErrorStackAndResultFilesOrder()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        TrxTestResult result = CopyWithOutputAndFiles(CreateResult(1, "ordered-output", TrxTestOutcome.Failed));

        harness.Writer.AppendCompleted(result, ExecutionId(1));

        XElement completed = CompletedResults(Load(harness.Bytes)).Single();
        Assert.AreSequenceEqual(
            ["Output", "ResultFiles"],
            completed.Elements().Select(element => element.Name.LocalName).ToArray());
        XElement output = completed.Element(Ns + "Output")!;
        Assert.AreSequenceEqual(
            ["StdOut", "StdErr", "DebugTrace", "ErrorInfo"],
            output.Elements().Select(element => element.Name.LocalName).ToArray());
        Assert.AreEqual("stdout-1\nstdout-2", output.Element(Ns + "StdOut")!.Value);
        Assert.AreEqual("stderr", output.Element(Ns + "StdErr")!.Value);
        Assert.AreEqual("debug", output.Element(Ns + "DebugTrace")!.Value);
        Assert.AreEqual("exception", output.Descendants(Ns + "Message").Single().Value);
        Assert.AreEqual("stack", output.Descendants(Ns + "StackTrace").Single().Value);
        Assert.AreSequenceEqual(
            ["first/result.txt", "second/result.txt"],
            completed.Descendants(Ns + "ResultFile").Select(element => element.Attribute("path")!.Value).ToArray());
    }

    [TestMethod]
    public void Summary_CrashExitCodeRunInfosCollectorEntriesAndAttachmentWarnings_PreservesOrder()
    {
        using var harness = new PrototypeHarness();
        harness.Writer.Initialize();
        TrxTestResult result = CreateResult(1, "summary", TrxTestOutcome.Passed);
        Guid executionId = ExecutionId(1);
        harness.Writer.AppendCompleted(result, executionId);
        var completion = new TrxPrototypeCompletion
        {
            FinishTime = FinishTime,
            IsTestHostCrashed = true,
            ExitCode = 17,
            CrashText = "host crashed <&>",
            CollectorAttachmentHrefs = ["collector/first.bin", "collector/second.bin"],
            AttachmentWarnings = ["warning one", "warning two"],
        };

        harness.Writer.Complete(completion);

        XDocument document = Load(harness.Bytes);
        XElement summary = Required(document, "ResultSummary");
        Assert.AreSequenceEqual(
            ["Counters", "RunInfos", "CollectorDataEntries"],
            summary.Elements().Select(element => element.Name.LocalName).ToArray());
        Assert.AreSequenceEqual(
            ["collector/first.bin", "collector/second.bin"],
            summary.Descendants(Ns + "A").Select(element => element.Attribute("href")!.Value).ToArray());
        XElement[] runInfos = summary.Descendants(Ns + "RunInfo").ToArray();
        Assert.AreSequenceEqual(
            ["Error", "Warning", "Warning"],
            runInfos.Select(element => element.Attribute("outcome")!.Value).ToArray());
        Assert.AreSequenceEqual(
            ["host crashed <&>", "warning one", "warning two"],
            runInfos.Select(element => element.Element(Ns + "Text")!.Value).ToArray());
        AssertTruthful(
            harness.Bytes,
            CreateExpectedResults([result], [executionId]),
            [],
            FinishTime,
            "Failed");
    }

    [TestMethod]
    public async Task NormalPath_SemanticallyMatchesCurrentRendererAfterDynamicIdNormalization()
    {
        TrxPrototypeScenario scenario = TrxPrototypeScenarioFactory.CreateMixedResultsScenario();
        TrxRenderedScenario current = await TrxPrototypeScenarioFactory.RenderAsync(scenario);
        using var harness = new PrototypeHarness(
            runId: TrxPrototypeScenarioFactory.RunId,
            runName: TrxPrototypeScenarioFactory.RunName,
            machineName: TrxPrototypeScenarioFactory.MachineName,
            testModule: TrxPrototypeScenarioFactory.TestModule,
            frameworkUid: TrxPrototypeScenarioFactory.FrameworkUid,
            frameworkVersion: TrxPrototypeScenarioFactory.FrameworkVersion,
            startTime: TrxPrototypeScenarioFactory.StartTime);
        harness.Writer.Initialize();

        Guid[] executions = scenario.Expectation.CompletedResults
            .Select(result => Guid.Parse(result.ExecutionId))
            .ToArray();
        for (int i = 0; i < scenario.Results.Count; i++)
        {
            harness.Writer.AppendCompleted(scenario.Results[i], executions[i]);
        }

        var completion = new TrxPrototypeCompletion { FinishTime = TrxPrototypeScenarioFactory.FinishTime };
        harness.Writer.Complete(completion);
        XDocument prototype = Load(harness.Bytes);

        AssertTruthful(
            harness.Bytes,
            scenario.Expectation.CompletedResults,
            [],
            TrxPrototypeScenarioFactory.FinishTime,
            "Failed",
            TrxPrototypeScenarioFactory.RunId,
            TrxPrototypeScenarioFactory.RunName,
            TrxPrototypeScenarioFactory.StartTime);
        AssertSemanticSequenceEqual(
            CompletedResults(current.Document),
            CompletedResults(prototype),
            [
                "executionId",
                "testId",
                "testName",
                "computerName",
                "duration",
                "startTime",
                "endTime",
                "testType",
                "outcome",
                "testListId",
                "relativeResultsDirectory",
            ]);
        AssertSemanticSequenceEqual(
            Required(current.Document, "TestDefinitions").Elements(Ns + "UnitTest"),
            Required(prototype, "TestDefinitions").Elements(Ns + "UnitTest"),
            ["id", "name", "storage"]);
        AssertSemanticSequenceEqual(
            Required(current.Document, "TestEntries").Elements(Ns + "TestEntry"),
            Required(prototype, "TestEntries").Elements(Ns + "TestEntry"),
            ["testId", "executionId", "testListId"]);

        XElement currentCounters = Required(current.Document, "ResultSummary").Element(Ns + "Counters")!;
        XElement prototypeCounters = Required(prototype, "ResultSummary").Element(Ns + "Counters")!;
        foreach (XAttribute counter in currentCounters.Attributes())
        {
            Assert.AreEqual(
                int.Parse(counter.Value, CultureInfo.InvariantCulture),
                int.Parse(prototypeCounters.Attribute(counter.Name)!.Value, CultureInfo.InvariantCulture),
                counter.Name.LocalName);
        }

        harness.Writer.Compact(completion);
        TrxDocumentObservation compactObservation = TrxDocumentClassifier.Classify(harness.Bytes, scenario.Expectation);
        Assert.AreEqual(TrxDocumentClassification.Truthful, compactObservation.Classification, compactObservation.Diagnostic);
        XDocument compact = Load(harness.Bytes);
        AssertSemanticSequenceEqual(
            CompletedResults(current.Document),
            CompletedResults(compact),
            [
                "executionId",
                "testId",
                "testName",
                "computerName",
                "duration",
                "startTime",
                "endTime",
                "testType",
                "outcome",
                "testListId",
                "relativeResultsDirectory",
            ]);
    }

    private static void AssertTruthful(
        byte[] bytes,
        IReadOnlyList<TrxExpectedResult> completed,
        IReadOnlyList<TrxExpectedRunningTest> running,
        DateTimeOffset finishTime,
        string outcome,
        Guid? runId = null,
        string? runName = null,
        DateTimeOffset? startTime = null)
    {
        var expectation = new TrxDocumentExpectation
        {
            RunId = runId ?? RunId,
            RunName = runName ?? RunName,
            StartTime = startTime ?? StartTime,
            FinishTime = finishTime,
            SummaryOutcome = outcome,
            CompletedResults = completed,
            RunningTests = running,
            RootChildOrder = PrototypeRootOrder,
        };
        TrxDocumentObservation observation = TrxDocumentClassifier.Classify(bytes, expectation);
        Assert.AreEqual(TrxDocumentClassification.Truthful, observation.Classification, observation.Diagnostic);
    }

    private static IReadOnlyList<TrxExpectedResult> CreateExpectedResults(
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<Guid> executionIds)
    {
        var expected = new TrxExpectedResult[results.Count];
        for (int i = 0; i < results.Count; i++)
        {
            expected[i] = TrxExpectedResultFactory.Create(
                results[i],
                executionIds[i],
                MachineName,
                TestModule,
                FrameworkUid,
                FrameworkVersion,
                FinishTime);
        }

        return expected;
    }

    private static TrxExpectedRunningTest CreateExpectedRunning(
        int testNumber,
        Guid executionId,
        string displayName,
        DateTimeOffset? startTime = null)
        => new()
        {
            TestId = Guid.Parse(TestId(testNumber)).ToString(),
            ExecutionId = executionId.ToString(),
            TestName = displayName,
            ComputerName = MachineName,
            StartTime = startTime ?? StartTime,
        };

    private static TrxTestResult CreateResult(
        int testNumber,
        string displayName,
        TrxTestOutcome outcome,
        string? definitionName = null)
        => new()
        {
            Uid = TestId(testNumber),
            DisplayName = displayName,
            Outcome = outcome,
            StartTime = StartTime.AddSeconds(testNumber),
            EndTime = StartTime.AddSeconds(testNumber + 1),
            Duration = TimeSpan.FromSeconds(1),
            TrxTestDefinitionName = definitionName ?? displayName,
            TrxFullyQualifiedTypeName = "Prototype.ContractTests",
            TestMethodIdentifier = new TrxTestMethodIdentifier
            {
                Namespace = "Prototype",
                TypeName = "ContractTests",
                MethodName = definitionName ?? displayName,
            },
        };

    private static TrxTestResult CopyWithMetadata(
        TrxTestResult source,
        IReadOnlyList<string> categories,
        IReadOnlyList<TrxTestMetadata> metadata)
        => new()
        {
            Uid = source.Uid,
            DisplayName = source.DisplayName,
            Outcome = source.Outcome,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            Duration = source.Duration,
            TrxTestDefinitionName = source.TrxTestDefinitionName,
            TrxFullyQualifiedTypeName = source.TrxFullyQualifiedTypeName,
            TestMethodIdentifier = source.TestMethodIdentifier,
            Categories = categories,
            Metadata = metadata,
        };

    private static TrxTestResult CopyWithMessages(
        TrxTestResult source,
        IReadOnlyList<TrxStreamMessage> messages)
        => new()
        {
            Uid = source.Uid,
            DisplayName = source.DisplayName,
            Outcome = source.Outcome,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            Duration = source.Duration,
            TrxTestDefinitionName = source.TrxTestDefinitionName,
            TrxFullyQualifiedTypeName = source.TrxFullyQualifiedTypeName,
            TestMethodIdentifier = source.TestMethodIdentifier,
            Messages = messages,
        };

    private static TrxTestResult CopyWithOutputAndFiles(TrxTestResult source)
        => new()
        {
            Uid = source.Uid,
            DisplayName = source.DisplayName,
            Outcome = source.Outcome,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            Duration = source.Duration,
            TrxTestDefinitionName = source.TrxTestDefinitionName,
            TrxFullyQualifiedTypeName = source.TrxFullyQualifiedTypeName,
            TestMethodIdentifier = source.TestMethodIdentifier,
            ExceptionMessage = "exception",
            ExceptionStackTrace = "stack",
            Messages =
            [
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = "stdout-1" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = "stdout-2" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardError, Message = "stderr" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.DebugOrTrace, Message = "debug" },
            ],
            FileArtifacts =
            [
                new TrxTestFileArtifact { FullPath = "first/result.txt" },
                new TrxTestFileArtifact { FullPath = "second/result.txt" },
            ],
        };

    private static void AssertResultOrder(byte[] bytes, params string[] expectedNames)
        => Assert.AreSequenceEqual(
            expectedNames,
            CompletedResults(Load(bytes)).Select(element => element.Attribute("testName")!.Value).ToArray());

    private static void AssertWhitespacePad(XElement element, int expectedLength)
    {
        Assert.IsEmpty(element.Elements());
        string value = string.Concat(element.Nodes().OfType<XText>().Select(text => text.Value));
        Assert.AreEqual(expectedLength, value.Length);
        Assert.IsTrue(IsWhitespace(value));
    }

    private static bool IsWhitespace(string value)
        => value.All(character => character is ' ' or '\t' or '\r' or '\n');

    private static DateTimeOffset ReadFinishTime(byte[] bytes)
        => DateTimeOffset.Parse(
            Required(Load(bytes), "Times").Attribute("finish")!.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    private static XDocument Load(byte[] bytes)
        => XDocument.Parse(StrictUtf8.GetString(bytes), LoadOptions.PreserveWhitespace);

    private static XElement Required(XDocument document, string localName)
        => document.Root!.Element(Ns + localName)!;

    private static IEnumerable<XElement> CompletedResults(XDocument document)
        => Required(document, "Results")
            .Elements(Ns + "UnitTestResult")
            .Where(element => element.Attribute("outcome")?.Value != "InProgress");

    private static void AssertSemanticSequenceEqual(
        IEnumerable<XElement> expected,
        IEnumerable<XElement> actual,
        IReadOnlyList<string> attributeNames)
    {
        XElement[] expectedArray = expected.ToArray();
        XElement[] actualArray = actual.ToArray();
        Assert.HasCount(expectedArray.Length, actualArray);
        for (int i = 0; i < expectedArray.Length; i++)
        {
            foreach (string attributeName in attributeNames)
            {
                Assert.AreEqual(
                    expectedArray[i].Attribute(attributeName)!.Value,
                    actualArray[i].Attribute(attributeName)!.Value,
                    $"Element {i}, attribute {attributeName}");
            }
        }
    }

    private static string TestId(int value) => $"20000000-0000-0000-0000-{value:D12}";

    private static Guid ExecutionId(int value) => new($"30000000-0000-0000-0000-{value:D12}");

    private sealed class PrototypeHarness : IDisposable
    {
        private readonly string? _temporaryDirectory;
        private readonly TrxFaultInjectingFileOperations? _faultOperations;

        public PrototypeHarness(
            ITrxPrototypeFileOperations? operations = null,
            Guid? runId = null,
            string runName = RunName,
            string machineName = MachineName,
            string testModule = TestModule,
            string frameworkUid = FrameworkUid,
            string frameworkVersion = FrameworkVersion,
            DateTimeOffset? startTime = null,
            int definitionPadBytes = 65_536,
            int entryPadBytes = 32_768,
            int summaryPadBytes = 32_768,
            int counterWidth = CounterWidth,
            int runningSlotCount = 4,
            int runningSlotByteCapacity = 512)
        {
            if (operations is null)
            {
                _temporaryDirectory = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"trx-incremental-prototype-{Guid.NewGuid():N}");
                Directory.CreateDirectory(_temporaryDirectory);
                operations = new TrxPrototypeFileOperations();
                Path = System.IO.Path.Combine(_temporaryDirectory, "results.trx");
            }
            else
            {
                Path = "results.trx";
                _faultOperations = operations as TrxFaultInjectingFileOperations;
            }

            Writer = new TrxIncrementalWriterPrototype(
                operations,
                Path,
                runId ?? RunId,
                runName,
                machineName,
                testModule,
                frameworkUid,
                frameworkVersion,
                startTime ?? StartTime,
                definitionPadBytes,
                entryPadBytes,
                summaryPadBytes,
                counterWidth,
                runningSlotCount,
                runningSlotByteCapacity);
        }

        public string Path { get; }

        public TrxIncrementalWriterPrototype Writer { get; }

        public byte[] Bytes => _faultOperations?.GetFileBytes(Path) ?? File.ReadAllBytes(Path);

        public void Dispose()
        {
            if (_temporaryDirectory is not null)
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
    }
}
