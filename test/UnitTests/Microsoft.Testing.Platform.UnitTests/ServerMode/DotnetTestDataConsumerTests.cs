// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.IPC;
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
