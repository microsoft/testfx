// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestExecutionActivityBrokerCompletionTests
{
    [TestMethod]
    public void CompleteWithoutResult_BeforeExecutionEnd_FinalizesWhenExecutionEndIsRecorded()
    {
        Mock<IPlatformOpenTelemetryServiceWithTestExecutionActivities> service = new();
        Mock<IPlatformTestExecutionActivity> activity = new();
        activity.Setup(a => a.SetTag(It.IsAny<string>(), It.IsAny<object?>())).Returns(activity.Object);
        activity.Setup(a => a.SetStatus(It.IsAny<PlatformActivityStatusCode>(), It.IsAny<string?>())).Returns(activity.Object);
        service.Setup(s => s.StartTestExecutionActivity(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTimeOffset>())).Returns(activity.Object);
        using TestExecutionActivityBroker broker = new(service.Object);
        TestNode node = new() { Uid = "no-result", DisplayName = "no-result" };
        TestExecutionActivityReservation reservation = broker.Reserve(node)!;
        reservation.Activate();

        reservation.CompleteWithoutResult();
        activity.Verify(a => a.Stop(It.IsAny<DateTimeOffset>()), Times.Never);

        DateTimeOffset end = new(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);
        reservation.RecordExecutionEnd(end);

        activity.Verify(a => a.Stop(end), Times.Once);
    }
}
