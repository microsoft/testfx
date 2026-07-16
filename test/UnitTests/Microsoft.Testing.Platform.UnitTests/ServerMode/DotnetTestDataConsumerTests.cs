// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class DotnetTestDataConsumerTests
{
    [TestMethod]
    [DynamicData(nameof(DuplicateSingleOrDefaultProperties))]
    public void GetTestNodeDetails_WithDuplicateSingleOrDefaultProperty_ThrowsInvalidOperationException(Type propertyType, IProperty firstProperty, IProperty secondProperty)
    {
        TestNodeUpdateMessage testNodeUpdateMessage = new(new SessionUid("session"), new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "DisplayName",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, firstProperty, secondProperty),
        });

        MethodInfo getTestNodeDetails = typeof(DotnetTestDataConsumer).GetMethod("GetTestNodeDetails", BindingFlags.Static | BindingFlags.NonPublic)!;
        TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(() => getTestNodeDetails.Invoke(null, [testNodeUpdateMessage]));

        InvalidOperationException invalidOperationException = Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual($"Found multiple properties of type '{propertyType}'.", invalidOperationException.Message);
    }

    public static IEnumerable<object[]> DuplicateSingleOrDefaultProperties()
    {
        yield return [typeof(TimingProperty), CreateTimingProperty(), CreateTimingProperty()];
        yield return [typeof(StandardOutputProperty), new StandardOutputProperty("first"), new StandardOutputProperty("second")];
        yield return [typeof(StandardErrorProperty), new StandardErrorProperty("first"), new StandardErrorProperty("second")];
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenFailedWithAssertData_PopulatesExpectedAndActual()
    {
        // Exercises the production extraction path (Exception.Data["assert.expected"/"assert.actual"]) that the
        // serializer round-trip tests bypass by constructing FailedTestResultMessage directly. Without this a
        // regression in the producer would leave every forwarded pipe payload null while those tests still pass.
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "the expected";
        exception.Data["assert.actual"] = "the actual";

        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(new FailedTestNodeStateProperty(exception));

        Assert.AreEqual(TestStates.Failed, details.State);
        Assert.AreEqual("the expected", details.Expected);
        Assert.AreEqual("the actual", details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenPassed_DoesNotPopulateExpectedOrActual()
    {
        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(PassedTestNodeStateProperty.CachedInstance);

        Assert.AreEqual(TestStates.Passed, details.State);
        Assert.IsNull(details.Expected);
        Assert.IsNull(details.Actual);
    }

    [TestMethod]
    public void GetTestNodeDetails_WhenErroredWithAssertData_DoesNotPopulateExpectedOrActual()
    {
        // Only failed tests carry the assertion diff, mirroring TerminalOutputDevice's single-assembly behavior;
        // error/timeout/cancelled states must not surface expected/actual even when their exception carries the data.
        var exception = new InvalidOperationException("boom");
        exception.Data["assert.expected"] = "the expected";
        exception.Data["assert.actual"] = "the actual";

        DotnetTestDataConsumer.TestNodeDetails details = InvokeGetTestNodeDetails(new ErrorTestNodeStateProperty(exception));

        Assert.AreEqual(TestStates.Error, details.State);
        Assert.IsNull(details.Expected);
        Assert.IsNull(details.Actual);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromSessionFileArtifact_PreservesKind()
    {
        // Exercises the producer-to-wire mapping the serializer round-trip tests bypass (they start from
        // an already-populated FileArtifactMessage). Without this a regression that dropped the Kind here
        // would break post-processing grouping while every serializer test stayed green.
        var artifact = new SessionFileArtifact(new SessionUid("session"), new FileInfo("/path/a.trx"), "a.trx", "desc", "microsoft.testing.trx");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.AreEqual("exec-1", messages.ExecutionId);
        Assert.HasCount(1, messages.FileArtifacts);
        Assert.AreEqual("microsoft.testing.trx", messages.FileArtifacts[0].Kind);
        Assert.AreEqual("session", messages.FileArtifacts[0].SessionUid);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromSessionFileArtifactWithoutKind_SendsNullKind()
    {
        var artifact = new SessionFileArtifact(new SessionUid("session"), new FileInfo("/path/a.txt"), "a.txt");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.IsNull(messages.FileArtifacts[0].Kind);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromFileArtifact_PreservesKind()
    {
        var artifact = new FileArtifact(new FileInfo("/path/a.trx"), "a.trx", "desc", "microsoft.testing.trx");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.HasCount(1, messages.FileArtifacts);
        Assert.AreEqual("microsoft.testing.trx", messages.FileArtifacts[0].Kind);
    }

    [TestMethod]
    public void CreateFileArtifactMessages_FromFileArtifactWithoutKind_SendsNullKind()
    {
        var artifact = new FileArtifact(new FileInfo("/path/a.txt"), "a.txt");

        FileArtifactMessages messages = DotnetTestDataConsumer.CreateFileArtifactMessages("exec-1", artifact);

        Assert.IsNull(messages.FileArtifacts[0].Kind);
    }

    private static DotnetTestDataConsumer.TestNodeDetails InvokeGetTestNodeDetails(IProperty stateProperty)
    {
        TestNodeUpdateMessage testNodeUpdateMessage = new(new SessionUid("session"), new TestNode
        {
            Uid = new TestNodeUid("uid"),
            DisplayName = "DisplayName",
            Properties = new PropertyBag(stateProperty),
        });

        MethodInfo getTestNodeDetails = typeof(DotnetTestDataConsumer).GetMethod("GetTestNodeDetails", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsInstanceOfType<DotnetTestDataConsumer.TestNodeDetails>(getTestNodeDetails.Invoke(null, [testNodeUpdateMessage]));
    }

    private static TimingProperty CreateTimingProperty()
        => new(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)));
}
