// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class LoggerTests : TestContainer
{
    public void LogMessageWhenFormatIsNullShouldThrow()
    {
        Logger.OnLogMessage += message => { };
        Exception ex = VerifyThrows(() => Logger.LogMessage(null!, "arg1"));
        Verify(ex is ArgumentNullException && ex.Message.Contains("format"));
    }

    public void LogMessageWhenArgsIsNullShouldThrow()
    {
        Logger.OnLogMessage += message => { };
        Exception ex = VerifyThrows(() => Logger.LogMessage("foo", null!));
        Verify(ex is ArgumentNullException && ex.Message.Contains("args"));
    }

    public void LogMessageWhenFormatIsSimpleMessageAndNoArgsShouldCallEvent()
    {
        string? calledWith = null;
        Logger.OnLogMessage += message => calledWith = message;
        Logger.LogMessage("message");
        Verify(calledWith == "message");
    }

    public void LogMessageWhenFormatIsFormateMessageWithArgsShouldCallEvent()
    {
        string? calledWith = null;
        Logger.OnLogMessage += message => calledWith = message;
        Logger.LogMessage("message {0}", 1);
        Verify(calledWith == "message 1");
    }

    public void LogMessageWhenFormatContainsCurlyBrace()
    {
        string? calledWith = null;
        Logger.OnLogMessage += message => calledWith = message;
        Logger.LogMessage("{ A");
        Verify(calledWith == "{ A");
    }
}
