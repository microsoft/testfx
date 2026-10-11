// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public sealed class FrameworkHandlerAdapterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RecordStart_ThenResult_PublishesOrderedStatesAndForwardsFixedUpTestCase(bool useFullyQualifiedNameAsUid)
    {
        var fixture = new BridgeTestFixture(useFullyQualifiedNameAsUid);
        var handle = new Mock<IFrameworkHandle>(MockBehavior.Strict);
        TestCase testCase = CreateTestCase();
        var result = new TestResult(testCase) { Outcome = TestOutcome.Passed, DisplayName = "Result name" };
        var order = new List<string>();
        handle.Setup(x => x.RecordStart(testCase)).Callback(() =>
        {
            Assert.AreEqual(Constants.ExecutorUri, testCase.ExecutorUri.AbsoluteUri);
            order.Add("start");
        });
        handle.Setup(x => x.RecordResult(result)).Callback(() => order.Add("result"));
        fixture.MessageBus.Setup(x => x.PublishAsync(fixture.Framework, It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((producer, data) =>
            {
                order.Add("publish");
                fixture.PublishedMessages.Add((producer, data));
            })
            .Returns(Task.CompletedTask);
        FrameworkHandlerAdapter adapter = fixture.CreateFrameworkHandler(handle.Object, TestContext.CancellationToken);

        adapter.RecordStart(testCase);
        adapter.RecordResult(result);

        Assert.AreSequenceEqual(new[] { "start", "publish", "result", "publish" }, order);
        Assert.HasCount(2, fixture.PublishedMessages);
        var start = (TestNodeUpdateMessage)fixture.PublishedMessages[0].Data;
        var end = (TestNodeUpdateMessage)fixture.PublishedMessages[1].Data;
        Assert.AreEqual(fixture.Session.SessionUid, start.SessionUid);
        Assert.AreEqual(fixture.Session.SessionUid, end.SessionUid);
        Assert.AreEqual(useFullyQualifiedNameAsUid ? testCase.FullyQualifiedName : testCase.Id.ToString(), start.TestNode.Uid.Value);
        Assert.AreEqual(start.TestNode.Uid, end.TestNode.Uid);
        Assert.IsTrue(start.TestNode.Properties.Any<InProgressTestNodeStateProperty>());
        Assert.IsTrue(end.TestNode.Properties.Any<PassedTestNodeStateProperty>());
        Assert.IsFalse(end.TestNode.Properties.Any<InProgressTestNodeStateProperty>());
        Assert.AreEqual("Result name", end.TestNode.DisplayName);
        Assert.AreEqual(testCase.FullyQualifiedName, end.TestNode.Properties.Single<SerializableKeyValuePairStringProperty>().Value);
        handle.VerifyAll();
    }

    [TestMethod]
    [DataRow(TestOutcome.Failed, typeof(FailedTestNodeStateProperty))]
    [DataRow(TestOutcome.NotFound, typeof(ErrorTestNodeStateProperty))]
    [DataRow(TestOutcome.Skipped, typeof(SkippedTestNodeStateProperty))]
    public void RecordResult_NonPassingOutcome_PublishesMatchingState(TestOutcome outcome, Type expectedStateType)
    {
        var fixture = new BridgeTestFixture();
        var result = new TestResult(CreateTestCase()) { Outcome = outcome, ErrorMessage = "adapter detail" };

        fixture.CreateFrameworkHandler(cancellationToken: TestContext.CancellationToken).RecordResult(result);

        Assert.HasCount(1, fixture.PublishedMessages);
        var message = (TestNodeUpdateMessage)fixture.PublishedMessages.Single().Data;
        Assert.AreEqual(expectedStateType, message.TestNode.Properties.Single<TestNodeStateProperty>().GetType());
        Assert.AreSame(fixture.Framework, fixture.PublishedMessages.Single().Producer);
    }

    [TestMethod]
    [DataRow("start")]
    [DataRow("result")]
    [DataRow("end")]
    public void Recording_WhenCanceled_DoesNotMutateForwardOrPublish(string operation)
    {
        var fixture = new BridgeTestFixture();
        var handle = new Mock<IFrameworkHandle>(MockBehavior.Strict);
        TestCase testCase = CreateTestCase();
        Uri originalExecutor = testCase.ExecutorUri;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        FrameworkHandlerAdapter adapter = fixture.CreateFrameworkHandler(handle.Object, cancellation.Token);

        OperationCanceledException exception = Assert.ThrowsExactly<OperationCanceledException>(() =>
        {
            switch (operation)
            {
                case "start":
                    adapter.RecordStart(testCase);
                    break;
                case "result":
                    adapter.RecordResult(new TestResult(testCase) { Outcome = TestOutcome.Passed });
                    break;
                default:
                    adapter.RecordEnd(testCase, TestOutcome.Passed);
                    break;
            }
        });

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreSame(originalExecutor, testCase.ExecutorUri);
        Assert.IsEmpty(fixture.PublishedMessages);
        handle.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void RecordEnd_ForwardsOutcomeWithoutPublishingAnExtraNode()
    {
        var fixture = new BridgeTestFixture();
        var handle = new Mock<IFrameworkHandle>();
        TestCase testCase = CreateTestCase();
        handle.Setup(x => x.RecordEnd(testCase, TestOutcome.Failed)).Callback(() =>
            Assert.AreEqual(Constants.ExecutorUri, testCase.ExecutorUri.AbsoluteUri));

        fixture.CreateFrameworkHandler(handle.Object, TestContext.CancellationToken).RecordEnd(testCase, TestOutcome.Failed);

        handle.Verify(x => x.RecordEnd(testCase, TestOutcome.Failed), Times.Once);
        Assert.IsEmpty(fixture.PublishedMessages);
    }

    [TestMethod]
    public void RecordResult_WhenPublishingFails_PropagatesOriginalFailureAfterForwarding()
    {
        var fixture = new BridgeTestFixture();
        var handle = new Mock<IFrameworkHandle>();
        var result = new TestResult(CreateTestCase()) { Outcome = TestOutcome.Passed };
        var failure = new InvalidOperationException("message bus failure");
        fixture.MessageBus.Setup(x => x.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Returns(Task.FromException(failure));

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => fixture.CreateFrameworkHandler(handle.Object, TestContext.CancellationToken).RecordResult(result));

        Assert.AreSame(failure, actual);
        handle.Verify(x => x.RecordResult(result), Times.Once);
    }

    [TestMethod]
    public void RecordAttachments_PublishesEveryFileWithSessionAndAttachmentMetadata()
    {
        var fixture = new BridgeTestFixture();
        var handle = new Mock<IFrameworkHandle>();
        var firstSet = new AttachmentSet(new Uri("collector://first"), "first collection");
        var secondSet = new AttachmentSet(new Uri("collector://second"), "second collection");
        var first = new UriDataAttachment(new Uri("file:///C:/bridge/first.txt"), "first description");
        var second = new UriDataAttachment(new Uri("file:///C:/bridge/second.txt"), "second description");
        firstSet.Attachments.Add(first);
        secondSet.Attachments.Add(second);
        IList<AttachmentSet> sets = new[] { firstSet, secondSet };

        fixture.CreateFrameworkHandler(handle.Object, TestContext.CancellationToken).RecordAttachments(sets);

        handle.Verify(x => x.RecordAttachments(sets), Times.Once);
        Assert.HasCount(2, fixture.PublishedMessages);
        for (int i = 0; i < sets.Count; i++)
        {
            var artifact = (SessionFileArtifact)fixture.PublishedMessages[i].Data;
            Assert.AreEqual(fixture.Session.SessionUid, artifact.SessionUid);
            Assert.AreEqual(sets[i].DisplayName, artifact.DisplayName);
            Assert.AreEqual(sets[i].Attachments[0].Description, artifact.Description);
            Assert.AreEqual(new FileInfo(sets[i].Attachments[0].Uri.LocalPath).FullName, artifact.FileInfo.FullName);
            Assert.AreSame(fixture.Framework, fixture.PublishedMessages[i].Producer);
        }
    }

    [TestMethod]
    public void RecordAttachments_NonFileUri_RejectsAttachmentWithoutPublishing()
    {
        var fixture = new BridgeTestFixture();
        var handle = new Mock<IFrameworkHandle>();
        var set = new AttachmentSet(new Uri("collector://test"), "collection");
        set.Attachments.Add(new UriDataAttachment(new Uri("https://invalid.example/result.txt"), "not a file"));
        IList<AttachmentSet> sets = new[] { set };

        FormatException exception = Assert.ThrowsExactly<FormatException>(() => fixture.CreateFrameworkHandler(handle.Object, TestContext.CancellationToken).RecordAttachments(sets));

        Assert.AreEqual("Test adapter Bridge tests only supports file attachments.", exception.Message);
        Assert.IsEmpty(fixture.PublishedMessages);
        handle.Verify(x => x.RecordAttachments(sets), Times.Once);
    }

    [TestMethod]
    public void LaunchProcessWithDebuggerAttached_DelegatesToHandleWithoutStartingAProcess()
    {
        var fixture = new BridgeTestFixture();
        var handle = new Mock<IFrameworkHandle>(MockBehavior.Strict);
        var environment = new Dictionary<string, string?> { ["name"] = "value" };
        handle.Setup(x => x.LaunchProcessWithDebuggerAttached("program", "directory", "arguments", environment)).Returns(123);

        int result = fixture.CreateFrameworkHandler(handle.Object, TestContext.CancellationToken)
            .LaunchProcessWithDebuggerAttached("program", "directory", "arguments", environment);

        Assert.AreEqual(123, result);
        handle.VerifyAll();
        Assert.AreEqual(-1, fixture.CreateFrameworkHandler(cancellationToken: TestContext.CancellationToken).LaunchProcessWithDebuggerAttached("program", null, null, null));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Constructor_InvalidAssemblySet_RejectsEmptyOrMissingApplication(bool empty)
    {
        var fixture = new BridgeTestFixture();
        string[] paths = empty ? [] : ["first.dll", "second.dll"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => fixture.CreateFrameworkHandler(cancellationToken: TestContext.CancellationToken, assemblyPaths: paths));

        Assert.AreEqual(empty ? "testAssemblyPaths should contain at least one test assembly." : "None of the test assemblies are the test application.", exception.Message);
    }

    [TestMethod]
    public void Constructor_MultipleAssembliesIncludingApplication_AllowsRecording()
    {
        var fixture = new BridgeTestFixture();

        fixture.CreateFrameworkHandler(cancellationToken: TestContext.CancellationToken, assemblyPaths: ["other.dll", "tests.dll"]).RecordStart(CreateTestCase());

        Assert.HasCount(1, fixture.PublishedMessages);
        fixture.ModuleInfo.Verify(x => x.GetCurrentTestApplicationFullPath(), Times.Once);
    }

    private static TestCase CreateTestCase() => new("Sample.Tests.Test", new Uri("executor://original"), "tests.dll")
    {
        Id = new Guid("f0718900-f19b-4676-9da2-895df5b6d105"),
        DisplayName = "Discovery name",
    };
}
