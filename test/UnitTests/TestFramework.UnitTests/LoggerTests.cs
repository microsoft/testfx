// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class LoggerTests : TestContainer
{
    public void LogMessageWhenFormatIsNullShouldThrow()
    {
        Logger.OnLogMessage += message => { };
        Action act = () => Logger.LogMessage(null!, "arg1");
        act.Should().Throw<ArgumentNullException>().WithMessage("*format*");
    }

    public void LogMessageWhenArgsIsNullShouldThrow()
    {
        Logger.OnLogMessage += message => { };
        Action act = () => Logger.LogMessage("foo", null!);
        act.Should().Throw<ArgumentNullException>().WithMessage("*args*");
    }

    public void LogMessageWhenFormatIsSimpleMessageAndNoArgsShouldCallEvent()
    {
        string? calledWith = null;
        Logger.OnLogMessage += message => calledWith = message;
        Logger.LogMessage("message");
        calledWith.Should().Be("message");
    }

    public void LogMessageWhenFormatIsFormateMessageWithArgsShouldCallEvent()
    {
        string? calledWith = null;
        Logger.OnLogMessage += message => calledWith = message;
        Logger.LogMessage("message {0}", 1);
        calledWith.Should().Be("message 1");
    }

    public void LogMessageWhenFormatContainsCurlyBrace()
    {
        string? calledWith = null;
        Logger.OnLogMessage += message => calledWith = message;
        Logger.LogMessage("{ A");
        calledWith.Should().Be("{ A");
    }

    public void LogMessage_ThrowingSubscriber_DoesNotPreventLaterSynchronousDelivery()
    {
        var calls = new List<string>();
        int callerThread = Environment.CurrentManagedThreadId;
        int deliveredThread = 0;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        Logger.LogMessageHandler first = message =>
        {
            calls.Add($"first:{message}");
            throw new InvalidOperationException("subscriber failure");
        };
        Logger.LogMessageHandler second = message =>
        {
            deliveredThread = Environment.CurrentManagedThreadId;
            calls.Add($"second:{message}");
        };
        Logger.LogMessageHandler third = message => calls.Add($"third:{message}");
        Logger.OnLogMessage += first;
        Logger.OnLogMessage += second;
        Logger.OnLogMessage += third;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Action action = () => Logger.LogMessage("value {0:F1}", 1.5);
            action.Should().NotThrow();
            calls.Should().Equal("first:value 1.5", "second:value 1.5", "third:value 1.5");
            deliveredThread.Should().Be(callerThread);
        }
        finally
        {
            Logger.OnLogMessage -= first;
            Logger.OnLogMessage -= second;
            Logger.OnLogMessage -= third;
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
