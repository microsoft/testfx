// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestFrameworkHandleTests : TestContainer
{
    public void MessagesPreserveSeverityTextAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        List<(IOutputDeviceDataProducer Producer, IOutputDeviceData Data, CancellationToken Token)> messages = [];
        var output = new Mock<IOutputDevice>(MockBehavior.Strict);
        output.Setup(device => device.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((producer, data, token) => messages.Add((producer, data, token)))
            .Returns(Task.CompletedTask);
        var handle = new MSTestFrameworkHandle(output.Object, new MSTestExtension(), cancellation.Token);

        handle.SendMessage(TestMessageLevel.Informational, "information");
        handle.SendMessage(TestMessageLevel.Warning, "warning");
        handle.SendMessage(TestMessageLevel.Error, "error");

        messages.Count.Should().Be(3);
        messages[0].Data.Should().BeOfType<TextOutputDeviceData>().Which.Text.Should().Be("information");
        messages[1].Data.Should().BeOfType<WarningMessageOutputDeviceData>().Which.Message.Should().Be("warning");
        messages[2].Data.Should().BeOfType<ErrorMessageOutputDeviceData>().Which.Message.Should().Be("error");
        messages.Select(message => message.Producer).Should().OnlyContain(producer => ReferenceEquals(producer, handle));
        messages.Select(message => message.Token).Should().OnlyContain(token => token == cancellation.Token);
    }

    public void UnsupportedSeverityFailsWithoutPublishing()
    {
        var output = new Mock<IOutputDevice>(MockBehavior.Strict);
        var handle = new MSTestFrameworkHandle(output.Object, new MSTestExtension(), CancellationToken.None);

        Action send = () => handle.SendMessage((TestMessageLevel)42, "not published");

        send.Should().ThrowExactly<NotSupportedException>().WithMessage("Unsupported logging level '42'.");
        output.VerifyNoOtherCalls();
    }

    public void VSTestRecordingDoesNotPublishNativeResults()
    {
        var output = new Mock<IOutputDevice>(MockBehavior.Strict);
        var handle = new MSTestFrameworkHandle(output.Object, new MSTestExtension(), CancellationToken.None);
        var testCase = new TestCase("Namespace.Class.Method", new Uri("executor://unit"), "source.dll");

        handle.RecordStart(testCase);
        handle.RecordResult(new ObjectModel.TestResult(testCase) { Outcome = TestOutcome.Failed });
        handle.RecordEnd(testCase, TestOutcome.Failed);
        handle.RecordAttachments([new AttachmentSet(new Uri("attachment://unit"), "attachment")]);

        handle.LaunchProcessWithDebuggerAttached("not-launched.exe", null, null, null).Should().Be(-1);
        output.VerifyNoOtherCalls();
    }

    public async Task EnablementDelegatesToRegisteredExtension()
    {
        var extension = new Mock<IExtension>(MockBehavior.Strict);
        extension.SetupGet(value => value.Uid).Returns("extension-id");
        extension.SetupGet(value => value.Version).Returns("1.2.3");
        extension.SetupGet(value => value.DisplayName).Returns("custom extension");
        extension.SetupGet(value => value.Description).Returns("custom description");
        extension.SetupSequence(value => value.IsEnabledAsync()).ReturnsAsync(false).ReturnsAsync(true);
        IExtension handle = new MSTestFrameworkHandle(Mock.Of<IOutputDevice>(), extension.Object, CancellationToken.None);

        (await handle.IsEnabledAsync()).Should().BeFalse();
        (await handle.IsEnabledAsync()).Should().BeTrue();
        handle.Uid.Should().Be("extension-id");
        handle.Version.Should().Be("1.2.3");
        handle.DisplayName.Should().Be("custom extension");
        handle.Description.Should().Be("custom description");
        extension.Verify(value => value.IsEnabledAsync(), Times.Exactly(2));
        (await new MSTestExtension().IsEnabledAsync()).Should().BeTrue();
    }

    public void AdapterSeverityRejectsUnknownValues()
    {
        MessageLevel.Informational.ToTestMessageLevel().Should().Be(TestMessageLevel.Informational);
        MessageLevel.Warning.ToTestMessageLevel().Should().Be(TestMessageLevel.Warning);
        MessageLevel.Error.ToTestMessageLevel().Should().Be(TestMessageLevel.Error);
        Action convert = () => ((MessageLevel)42).ToTestMessageLevel();

        convert.Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName("messageLevel");
    }
}
