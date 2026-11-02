// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public sealed class TestCaseDiscoverySinkAdapterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SendTestCase_ForwardsFixedUpCaseAndPublishesDiscoveryWithAdditionalProperties(bool useFullyQualifiedNameAsUid)
    {
        var fixture = new BridgeTestFixture(useFullyQualifiedNameAsUid);
        var sink = new Mock<ITestCaseDiscoverySink>(MockBehavior.Strict);
        var testCase = new TestCase("Sample.Tests.Discovered", new Uri("executor://original"), "tests.dll");
        sink.Setup(x => x.SendTestCase(testCase)).Callback(() =>
        {
            Assert.AreEqual(Constants.ExecutorUri, testCase.ExecutorUri.AbsoluteUri);
            Assert.IsEmpty(fixture.PublishedMessages);
        });

        fixture.CreateDiscoverySink(sink.Object, TestContext.CancellationToken).SendTestCase(testCase);

        Assert.HasCount(1, fixture.PublishedMessages);
        var message = (TestNodeUpdateMessage)fixture.PublishedMessages.Single().Data;
        Assert.AreSame(fixture.Framework, fixture.PublishedMessages.Single().Producer);
        Assert.AreEqual(fixture.Session.SessionUid, message.SessionUid);
        Assert.AreEqual(useFullyQualifiedNameAsUid ? testCase.FullyQualifiedName : testCase.Id.ToString(), message.TestNode.Uid.Value);
        Assert.IsTrue(message.TestNode.Properties.Any<DiscoveredTestNodeStateProperty>());
        Assert.IsFalse(message.TestNode.Properties.Any<InProgressTestNodeStateProperty>());
        Assert.AreEqual(testCase.FullyQualifiedName, message.TestNode.Properties.Single<SerializableKeyValuePairStringProperty>().Value);
        sink.VerifyAll();
    }

    [TestMethod]
    public void SendTestCase_WhenCanceled_DoesNotMutateForwardOrPublish()
    {
        var fixture = new BridgeTestFixture();
        var sink = new Mock<ITestCaseDiscoverySink>(MockBehavior.Strict);
        var testCase = new TestCase("Sample.Tests.Discovered", new Uri("executor://original"), "tests.dll");
        Uri originalExecutor = testCase.ExecutorUri;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.ThrowsExactly<OperationCanceledException>(() =>
            fixture.CreateDiscoverySink(sink.Object, cancellation.Token).SendTestCase(testCase));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreSame(originalExecutor, testCase.ExecutorUri);
        Assert.IsEmpty(fixture.PublishedMessages);
        sink.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void SendTestCase_WhenLegacySinkThrows_DoesNotPublish()
    {
        var fixture = new BridgeTestFixture();
        var sink = new Mock<ITestCaseDiscoverySink>();
        var testCase = new TestCase("Sample.Tests.Discovered", new Uri("executor://original"), "tests.dll");
        var failure = new InvalidOperationException("discovery sink failure");
        sink.Setup(x => x.SendTestCase(testCase)).Throws(failure);

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => fixture.CreateDiscoverySink(sink.Object, TestContext.CancellationToken).SendTestCase(testCase));

        Assert.AreSame(failure, actual);
        Assert.IsEmpty(fixture.PublishedMessages);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Constructor_InvalidAssemblySet_RejectsEmptyOrMissingApplication(bool empty)
    {
        var fixture = new BridgeTestFixture();
        string[] paths = empty ? [] : ["first.dll", "second.dll"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => fixture.CreateDiscoverySink(cancellationToken: TestContext.CancellationToken, assemblyPaths: paths));

        Assert.AreEqual(empty ? "testAssemblyPaths should contain at least one test assembly." : "None of the test assemblies are the test application.", exception.Message);
    }

    [TestMethod]
    public void SendTestCase_WithoutLegacySinkAndWithMultipleAssemblies_PublishesDiscovery()
    {
        var fixture = new BridgeTestFixture();
        var testCase = new TestCase("Sample.Tests.Discovered", new Uri("executor://original"), "other.dll");

        fixture.CreateDiscoverySink(cancellationToken: TestContext.CancellationToken, assemblyPaths: ["other.dll", "tests.dll"]).SendTestCase(testCase);

        Assert.HasCount(1, fixture.PublishedMessages);
        fixture.ModuleInfo.Verify(x => x.GetCurrentTestApplicationFullPath(), Times.Once);
    }
}
