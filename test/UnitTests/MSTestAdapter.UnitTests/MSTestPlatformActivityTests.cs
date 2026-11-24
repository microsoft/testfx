// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.Telemetry;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestPlatformActivityTests : TestContainer
{
    public void FailureSignalsAndDisposalReachThePlatformActivity()
    {
        var activity = new Mock<IPlatformActivity>(MockBehavior.Strict);
        var failure = new InvalidOperationException("fixture failed");
        activity.Setup(value => value.SetTag("row", 7)).Returns(activity.Object);
        activity.Setup(value => value.RecordException(failure, null)).Returns(activity.Object);
        activity.Setup(value => value.SetStatus(PlatformActivityStatusCode.Error, "cleanup failed")).Returns(activity.Object);
        activity.Setup(value => value.Dispose());

        using (var bridge = new MSTestPlatformActivity(activity.Object))
        {
            bridge.SetTag("row", 7);
            bridge.RecordException(failure);
            bridge.SetFailed("cleanup failed");
        }

        activity.Verify(value => value.SetTag("row", 7), Times.Once);
        activity.Verify(value => value.RecordException(failure, null), Times.Once);
        activity.Verify(value => value.SetStatus(PlatformActivityStatusCode.Error, "cleanup failed"), Times.Once);
        activity.Verify(value => value.Dispose(), Times.Once);
        activity.VerifyNoOtherCalls();
    }

    public void FactoryUsesNonAmbientSpansAndSelectsTheCorrectParent()
    {
        var rootActivity = new Mock<IPlatformActivity>();
        rootActivity.SetupGet(value => value.Id).Returns("framework-parent");
        var activity = new Mock<IPlatformActivity>();
        var telemetry = new Mock<IPlatformOpenTelemetryService>();
        telemetry.SetupGet(value => value.TestFrameworkActivity).Returns(rootActivity.Object);
        telemetry.Setup(value => value.StartNonAmbientActivity("fixture", It.IsAny<IEnumerable<KeyValuePair<string, object?>>?>(), It.IsAny<string?>()))
            .Returns(activity.Object);
        var services = new Mock<IServiceProvider>();
        services.Setup(value => value.GetService(typeof(IPlatformOpenTelemetryService))).Returns(telemetry.Object);
        KeyValuePair<string, object?>[] tags = [new("row", 7)];

        try
        {
            MSTestPlatformActivity.TryEnable(services.Object);
            using (IMSTestActivity? first = MSTestInstrumentation.StartActivity("fixture", tags))
            {
                first.Should().NotBeNull();
                first!.SetTag("result", "first");
            }

            telemetry.SetupGet(value => value.HasCurrentActivity).Returns(true);
            using (IMSTestActivity? second = MSTestInstrumentation.StartActivity("fixture", tags))
            {
                second.Should().NotBeNull();
                second!.SetFailed("second");
            }

            telemetry.Verify(value => value.StartNonAmbientActivity("fixture", tags, "framework-parent"), Times.Once);
            telemetry.Verify(value => value.StartNonAmbientActivity("fixture", tags, null), Times.Once);
            activity.Verify(value => value.SetTag("result", "first"), Times.Once);
            activity.Verify(value => value.SetStatus(PlatformActivityStatusCode.Error, "second"), Times.Once);
            activity.Verify(value => value.Dispose(), Times.Exactly(2));

            telemetry.Setup(value => value.StartNonAmbientActivity("fixture", tags, null)).Returns((IPlatformActivity?)null);
            MSTestInstrumentation.StartActivity("fixture", tags).Should().BeNull();
        }
        finally
        {
            MSTestInstrumentation.SetActivityFactory(null);
        }
    }

    public void MissingServiceDisablesPreviouslyConfiguredTracing()
    {
        bool factoryCalled = false;
        MSTestInstrumentation.SetActivityFactory((_, _) =>
        {
            factoryCalled = true;
            return null;
        });

        try
        {
            MSTestPlatformActivity.TryEnable(Mock.Of<IServiceProvider>());

            MSTestInstrumentation.IsEnabled.Should().BeFalse();
            MSTestInstrumentation.StartActivity("not started").Should().BeNull();
            factoryCalled.Should().BeFalse();
        }
        finally
        {
            MSTestInstrumentation.SetActivityFactory(null);
        }
    }
}
