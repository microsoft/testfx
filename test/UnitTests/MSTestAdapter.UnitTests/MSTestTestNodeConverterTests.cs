// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

/// <summary>
/// Unit tests for <see cref="MSTestTestNodeConverter"/> and the Microsoft.Testing.Platform-native
/// discovery/result seams (<see cref="MtpUnitTestElementSink"/>, <see cref="MtpTestResultRecorder"/>).
/// </summary>
public sealed class MSTestTestNodeConverterTests : TestContainer
{
    private static UnitTestElement CreateElement(string? managedMethodName = "MyMethod", string fullClassName = "MyNamespace.MyClass", string name = "MyMethod")
    {
        var testMethod = new TestMethod(managedMethodName, hierarchyValues: null, name, fullClassName, "MyAssembly.dll", displayName: null, parameterTypes: null);
        return new UnitTestElement(testMethod);
    }

    // --- Discovery node ---------------------------------------------------------------------------------------
    public void ToDiscoveredTestNode_SetsUidDisplayNameAndDiscoveredState()
    {
        UnitTestElement element = CreateElement();

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        node.Uid.Value.Should().Be(element.GetTestId().ToString());
        node.DisplayName.Should().Be("MyMethod");
        node.Properties.Any<DiscoveredTestNodeStateProperty>().Should().BeTrue();
        node.Properties.Any<InProgressTestNodeStateProperty>().Should().BeFalse();
    }

    public void ToDiscoveredTestNode_UsesExplicitDisplayName_WhenProvided()
    {
        var testMethod = new TestMethod("MyMethod", hierarchyValues: null, "MyMethod", "MyNamespace.MyClass", "MyAssembly.dll", displayName: "Friendly name", parameterTypes: null);
        var element = new UnitTestElement(testMethod);

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        node.DisplayName.Should().Be("Friendly name");
    }

    public void ToInProgressTestNode_AddsInProgressState()
    {
        TestNode node = MSTestTestNodeConverter.ToInProgressTestNode(CreateElement(), isTrxEnabled: false);

        node.Properties.Any<InProgressTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToDiscoveredTestNode_AddsTestMethodIdentifier_FromManagedNames()
    {
        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(CreateElement(), isTrxEnabled: false);

        TestMethodIdentifierProperty? identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        identifier.Should().NotBeNull();
        identifier!.Namespace.Should().Be("MyNamespace");
        identifier.TypeName.Should().Be("MyClass");
        identifier.MethodName.Should().Be("MyMethod");
        identifier.AssemblyFullName.Should().BeEmpty();
        identifier.ReturnTypeFullName.Should().BeEmpty();
    }

    public void ToDiscoveredTestNode_DoesNotAddTestMethodIdentifier_WhenNoManagedMethodName()
    {
        UnitTestElement element = CreateElement(managedMethodName: null);

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        node.Properties.Any<TestMethodIdentifierProperty>().Should().BeFalse();
    }

    public void ToDiscoveredTestNode_AddsFileLocation_WhenDeclaringFileKnown()
    {
        UnitTestElement element = CreateElement();
        element.DeclaringFilePath = "C:\\src\\MyClass.cs";
        element.DeclaringLineNumber = 42;

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        TestFileLocationProperty? location = node.Properties.SingleOrDefault<TestFileLocationProperty>();
        location.Should().NotBeNull();
        location!.FilePath.Should().Be("C:\\src\\MyClass.cs");
        location.LineSpan.Start.Line.Should().Be(42);
    }

    public void ToDiscoveredTestNode_DoesNotAddFileLocation_WhenDeclaringFileUnknown()
    {
        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(CreateElement(), isTrxEnabled: false);

        node.Properties.Any<TestFileLocationProperty>().Should().BeFalse();
    }

    public void ToDiscoveredTestNode_AddsCategoriesAndTraitsAsMetadata()
    {
        UnitTestElement element = CreateElement();
        element.TestCategory = ["CategoryA", "CategoryB"];
        element.Traits = [new TestTrait("Owner", "Alice")];

        TestNode node = MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false);

        TestMetadataProperty[] metadata = node.Properties.OfType<TestMetadataProperty>();
        metadata.Should().Contain(p => p.Key == "CategoryA" && p.Value == string.Empty);
        metadata.Should().Contain(p => p.Key == "CategoryB" && p.Value == string.Empty);
        metadata.Should().Contain(p => p.Key == "Owner" && p.Value == "Alice");
    }

    public void ToDiscoveredTestNode_AddsTrxCategories_OnlyWhenTrxEnabled()
    {
        UnitTestElement element = CreateElement();
        element.TestCategory = ["CategoryA"];

        MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: false)
            .Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty>().Should().BeFalse();

        MSTestTestNodeConverter.ToDiscoveredTestNode(element, isTrxEnabled: true)
            .Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxCategoriesProperty>().Should().BeTrue();
    }

    // --- Result node: outcomes --------------------------------------------------------------------------------
    public void ToResultTestNode_MapsPassedOutcome()
    {
        TestNode node = ResultNode(UnitTestOutcome.Passed);

        node.Properties.Any<PassedTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToResultTestNode_MapsFailedOutcome_WithMessageAndStackTrace()
    {
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "boom", ExceptionStackTrace = "at Some.Method()" };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        FailedTestNodeStateProperty? failed = node.Properties.SingleOrDefault<FailedTestNodeStateProperty>();
        failed.Should().NotBeNull();
        failed!.Exception!.Message.Should().Be("boom");
        failed.Exception.StackTrace.Should().Be("at Some.Method()");
    }

    public void ToResultTestNode_MapsIgnoredOutcomeToSkipped()
    {
        TestNode node = ResultNode(UnitTestOutcome.Ignored);

        node.Properties.Any<SkippedTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToResultTestNode_MapsNotFoundOutcomeToError()
    {
        TestNode node = ResultNode(UnitTestOutcome.NotFound);

        node.Properties.Any<ErrorTestNodeStateProperty>().Should().BeTrue();
    }

    public void ToResultTestNode_MapsInconclusiveToSkipped_ByDefault()
    {
        // Default MSTestSettings has MapInconclusiveToFailed = false.
        TestNode node = ResultNode(UnitTestOutcome.Inconclusive);

        node.Properties.Any<SkippedTestNodeStateProperty>().Should().BeTrue();
    }

    // --- Result node: timing, output, attachments -------------------------------------------------------------
    public void ToResultTestNode_AddsTimingProperty()
    {
        var start = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        DateTimeOffset end = start.AddSeconds(2);
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Passed, Duration = TimeSpan.FromSeconds(2) };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, start, end, isTrxEnabled: false, new MSTestSettings());

        TimingProperty? timing = node.Properties.SingleOrDefault<TimingProperty>();
        timing.Should().NotBeNull();
        timing!.GlobalTiming.Duration.Should().Be(TimeSpan.FromSeconds(2));
    }

    public void ToResultTestNode_MapsLogOutputAndLogErrorToStandardStreams()
    {
        var result = new FrameworkTestResult
        {
            Outcome = UnitTestOutcome.Passed,
            LogOutput = "hello out",
            LogError = "hello err",
        };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        node.Properties.SingleOrDefault<StandardOutputProperty>()!.StandardOutput.Should().Contain("hello out");
        node.Properties.SingleOrDefault<StandardErrorProperty>()!.StandardError.Should().Contain("hello err");
    }

    public void ToResultTestNode_IncludesDebugTraceAndTestContextBannersInStandardOutput()
    {
        var result = new FrameworkTestResult
        {
            Outcome = UnitTestOutcome.Passed,
            DebugTrace = "some trace",
            TestContextMessages = "some context",
        };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

        string standardOutput = node.Properties.SingleOrDefault<StandardOutputProperty>()!.StandardOutput;
        standardOutput.Should().Contain("Debug Trace:").And.Contain("some trace");
        standardOutput.Should().Contain("TestContext Messages:").And.Contain("some context");
    }

    // --- Result node: TRX properties --------------------------------------------------------------------------
    public void ToResultTestNode_AddsTrxProperties_WhenTrxEnabled()
    {
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "boom", ExceptionStackTrace = "at X()" };

        TestNode node = MSTestTestNodeConverter.ToResultTestNode(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: true, new MSTestSettings());

        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxExceptionProperty>().Should().BeTrue();
        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxMessagesProperty>().Should().BeTrue();
        Testing.Extensions.TrxReport.Abstractions.TrxFullyQualifiedTypeNameProperty? typeName =
            node.Properties.SingleOrDefault<Testing.Extensions.TrxReport.Abstractions.TrxFullyQualifiedTypeNameProperty>();
        typeName.Should().NotBeNull();
        typeName!.FullyQualifiedTypeName.Should().Be("MyNamespace.MyClass");
    }

    public void ToResultTestNode_DoesNotAddTrxProperties_WhenTrxDisabled()
    {
        TestNode node = ResultNode(UnitTestOutcome.Passed);

        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxExceptionProperty>().Should().BeFalse();
        node.Properties.Any<Testing.Extensions.TrxReport.Abstractions.TrxMessagesProperty>().Should().BeFalse();
    }

    // --- GetTestId caching ------------------------------------------------------------------------------------
    public void GetTestId_CachesComputedId_AndReturnsSameValueOnSubsequentCalls()
    {
        UnitTestElement element = CreateElement();

        element.CachedTestNodeUid.Should().BeNull();

        Guid first = element.GetTestId();

        element.CachedTestNodeUid.Should().Be(first);

        Guid second = element.GetTestId();

        second.Should().Be(first);
    }

    public void CloneWithSource_InvalidatesCachedTestId()
    {
        UnitTestElement element = CreateElement();
        Guid original = element.GetTestId();
        element.CachedTestNodeUid.Should().Be(original);

        UnitTestElement clone = element.CloneWithSource("OtherAssembly.dll");

        // The clone must start with a cleared cache and recompute a distinct id for the new source.
        clone.CachedTestNodeUid.Should().BeNull();
        clone.GetTestId().Should().NotBe(original);
        // The original element keeps its cached id untouched.
        element.CachedTestNodeUid.Should().Be(original);
    }

    public void WithUpdatedSource_InvalidatesCachedTestId_WhenSourceChanges()
    {
        UnitTestElement element = CreateElement();
        Guid original = element.GetTestId();

        UnitTestElement result = element.WithUpdatedSource("OtherAssembly.dll");

        result.Should().NotBeSameAs(element);
        result.CachedTestNodeUid.Should().BeNull();
        result.GetTestId().Should().NotBe(original);
    }

    public void CloneWithUpdatedSource_InvalidatesCachedTestId()
    {
        UnitTestElement element = CreateElement();
        element.GetTestId();
        element.CachedTestNodeUid.Should().NotBeNull();

        UnitTestElement clone = element.CloneWithUpdatedSource("OtherAssembly.dll");

        clone.CachedTestNodeUid.Should().BeNull();
    }

    public void Clone_PreservesCachedTestId()
    {
        UnitTestElement element = CreateElement();
        Guid original = element.GetTestId();

        UnitTestElement clone = element.Clone();

        // Clone() does not touch any hash input, so the cached id must be preserved.
        clone.CachedTestNodeUid.Should().Be(original);
        clone.GetTestId().Should().Be(original);
    }

    // --- Seams ------------------------------------------------------------------------------------------------
    public async Task MtpUnitTestElementSink_PublishesDiscoveredTestNode()
    {
        var messageBus = new CapturingMessageBus();
        var sessionUid = new SessionUid("session-1");
        var sink = new MtpUnitTestElementSink(messageBus, new StubDataProducer(), sessionUid, isTrxEnabled: false);

        await sink.SendTestElementAsync(CreateElement());

        messageBus.Published.Should().ContainSingle();
        var message = (TestNodeUpdateMessage)messageBus.Published[0];
        message.SessionUid.Value.Should().Be("session-1");
        message.TestNode.Properties.Any<DiscoveredTestNodeStateProperty>().Should().BeTrue();
    }

    public async Task MtpTestResultRecorder_RecordStart_PublishesInProgressNode()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());

        await recorder.RecordStartAsync(CreateElement());

        var message = (TestNodeUpdateMessage)messageBus.Published.Single();
        message.TestNode.Properties.Any<InProgressTestNodeStateProperty>().Should().BeTrue();
    }

    public async Task MtpTestResultRecorder_RecordEmptyResult_PublishesNothing()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());

        await recorder.RecordEmptyResultAsync(CreateElement());

        messageBus.Published.Should().BeEmpty();
    }

    public async Task MtpTestResultRecorder_RecordResult_PublishesResultNodeAndReturnsFailedFlag()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());
        var result = new FrameworkTestResult { Outcome = UnitTestOutcome.Failed, ExceptionMessage = "nope" };

        bool isFailed = await recorder.RecordResultAsync(CreateElement(), result, DateTimeOffset.Now, DateTimeOffset.Now);

        isFailed.Should().BeTrue();
        var message = (TestNodeUpdateMessage)messageBus.Published.Single();
        message.TestNode.Properties.Any<FailedTestNodeStateProperty>().Should().BeTrue();
    }

    public async Task MtpTestResultRecorder_RecordResult_ReturnsFalse_ForPassedTest()
    {
        var messageBus = new CapturingMessageBus();
        var recorder = new MtpTestResultRecorder(messageBus, new StubDataProducer(), new SessionUid("s"), isTrxEnabled: false, new MSTestSettings());

        bool isFailed = await recorder.RecordResultAsync(CreateElement(), new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }, DateTimeOffset.Now, DateTimeOffset.Now);

        isFailed.Should().BeFalse();
    }

    private static TestNode ResultNode(UnitTestOutcome outcome)
        => MSTestTestNodeConverter.ToResultTestNode(CreateElement(), new FrameworkTestResult { Outcome = outcome }, DateTimeOffset.Now, DateTimeOffset.Now, isTrxEnabled: false, new MSTestSettings());

    private sealed class CapturingMessageBus : IMessageBus
    {
        public List<IData> Published { get; } = [];

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            Published.Add(data);
            return Task.CompletedTask;
        }
    }

    private sealed class StubDataProducer : IDataProducer
    {
        public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

        public string Uid => "stub";

        public string Version => "1.0.0";

        public string DisplayName => "Stub";

        public string Description => "Stub data producer for tests.";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
