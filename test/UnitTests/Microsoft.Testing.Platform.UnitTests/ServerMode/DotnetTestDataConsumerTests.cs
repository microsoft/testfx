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

    private static TimingProperty CreateTimingProperty()
        => new(new TimingInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)));
}
