// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Requests;

[TestClass]
public sealed class VSTestExecutionRequestFactoryTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CreateRequest_PreservesSessionAssembliesAndFilter_AndProducesUsableAdapter(bool discovery)
    {
        var fixture = new BridgeTestFixture(useFullyQualifiedNameAsUid: true);
        var filter = new TestNodeUidListFilter([new TestNodeUid("Sample.Tests.Selected")]);
        string[] paths = ["tests.dll", "other.dll"];
        var testCase = new TestCase("Sample.Tests.Selected", new Uri("executor://original"), "tests.dll");
        TestExecutionRequest converted;
        ContextAdapterBase context;
        if (discovery)
        {
            VSTestDiscoverTestExecutionRequest request = VSTestDiscoverTestExecutionRequestFactory.CreateRequest(
                new DiscoverTestExecutionRequest(fixture.Session, filter), fixture.Framework, paths, CancellationToken.None);
            converted = request;
            context = (DiscoveryContextAdapter)request.DiscoveryContext;
            Assert.AreSequenceEqual(paths, request.AssemblyPaths);
            request.DiscoverySink.SendTestCase(testCase);
        }
        else
        {
            VSTestRunTestExecutionRequest request = VSTestRunTestExecutionRequestFactory.CreateRequest(
                new RunTestExecutionRequest(fixture.Session, filter), fixture.Framework, paths, CancellationToken.None);
            converted = request;
            context = (RunContextAdapter)request.RunContext;
            Assert.AreSequenceEqual(paths, request.AssemblyPaths);
            request.FrameworkHandle.RecordStart(testCase);
        }

        Assert.AreSame(fixture.Session, converted.Session);
        Assert.AreSame(filter, converted.Filter);
        ITestCaseFilterExpression? expression = context.GetTestCaseFilter(null, static _ => null);
        Assert.IsNotNull(expression);
        Assert.IsTrue(expression.MatchTestCase(testCase, static name => name == "FullyQualifiedName" ? "Sample.Tests.Selected" : null));
        Assert.IsFalse(expression.MatchTestCase(testCase, static name => name == "FullyQualifiedName" ? "Sample.Tests.Other" : null));
        Assert.HasCount(1, fixture.PublishedMessages);
        var message = (TestNodeUpdateMessage)fixture.PublishedMessages.Single().Data;
        Assert.AreEqual("Sample.Tests.Selected", message.TestNode.Uid.Value);
        Assert.AreEqual(fixture.Session.SessionUid, message.SessionUid);
        Assert.AreSame(fixture.Framework, fixture.PublishedMessages.Single().Producer);
        fixture.FileSystem.Verify(x => x.ReadAllText("tests.runsettings"), Times.Once);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CreateRequest_CancellationToken_IsHonoredByReturnedAdapter(bool discovery)
    {
        var fixture = new BridgeTestFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var testCase = new TestCase("Sample.Tests.Test", new Uri("executor://original"), "tests.dll");
        Uri originalExecutor = testCase.ExecutorUri;

        OperationCanceledException exception = Assert.ThrowsExactly<OperationCanceledException>(() =>
        {
            if (discovery)
            {
                VSTestDiscoverTestExecutionRequest request = VSTestDiscoverTestExecutionRequestFactory.CreateRequest(
                    new DiscoverTestExecutionRequest(fixture.Session), fixture.Framework, ["tests.dll"], cancellation.Token);
                request.DiscoverySink.SendTestCase(testCase);
            }
            else
            {
                VSTestRunTestExecutionRequest request = VSTestRunTestExecutionRequestFactory.CreateRequest(
                    new RunTestExecutionRequest(fixture.Session), fixture.Framework, ["tests.dll"], cancellation.Token);
                request.FrameworkHandle.RecordStart(testCase);
            }
        });

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreSame(originalExecutor, testCase.ExecutorUri);
        Assert.IsEmpty(fixture.PublishedMessages);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CreateRequest_NamedFeatureCapability_EnablesOriginalExecutorProperties(bool discovery)
    {
        var capability = new VSTestBridgeExtensionBaseCapabilities();
        var fixture = new BridgeTestFixture(false, capability);
        fixture.CommandLineOptions.Setup(x => x.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey)).Returns(true);
        var testCase = new TestCase("Sample.Tests.Test", new Uri("executor://original"), "tests.dll");
        if (discovery)
        {
            VSTestDiscoverTestExecutionRequestFactory.CreateRequest(
                new DiscoverTestExecutionRequest(fixture.Session), fixture.Framework, ["tests.dll"], CancellationToken.None)
                .DiscoverySink.SendTestCase(testCase);
        }
        else
        {
            VSTestRunTestExecutionRequestFactory.CreateRequest(
                new RunTestExecutionRequest(fixture.Session), fixture.Framework, ["tests.dll"], CancellationToken.None)
                .FrameworkHandle.RecordStart(testCase);
        }

        var message = (TestNodeUpdateMessage)fixture.PublishedMessages.Single().Data;
        Dictionary<string, string> properties = message.TestNode.Properties.OfType<SerializableKeyValuePairStringProperty>().ToDictionary(x => x.Key, x => x.Value);
        Assert.AreEqual(new Uri("executor://original").AbsoluteUri, properties["vstest.original-executor-uri"]);
        Assert.AreEqual(testCase.Id.ToString(), properties["vstest.TestCase.Id"]);
        Assert.AreEqual(testCase.FullyQualifiedName, properties["vstest.TestCase.FullyQualifiedName"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CreateRequest_InvalidAssemblySet_RejectsBeforeReportingAnyTests(bool discovery)
    {
        var fixture = new BridgeTestFixture();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
        {
            if (discovery)
            {
                _ = VSTestDiscoverTestExecutionRequestFactory.CreateRequest(
                    new DiscoverTestExecutionRequest(fixture.Session), fixture.Framework, [], CancellationToken.None);
            }
            else
            {
                _ = VSTestRunTestExecutionRequestFactory.CreateRequest(
                    new RunTestExecutionRequest(fixture.Session), fixture.Framework, [], CancellationToken.None);
            }
        });

        Assert.AreEqual("testAssemblyPaths should contain at least one test assembly.", exception.Message);
        Assert.IsEmpty(fixture.PublishedMessages);
    }
}
