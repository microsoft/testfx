// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages.UnitTests;

[TestGroup]
public sealed class TestNodePropertiesTests
{
    public void KeyValuePairStringProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "KeyValuePairStringProperty { Key = key, Value = value }",
            new KeyValuePairStringProperty("key", "value").ToString());

    public void DiscoveredTestNodeStateProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "DiscoveredTestNodeStateProperty { Explanation = some explanation }",
            new DiscoveredTestNodeStateProperty("some explanation").ToString());

    public void DiscoveredTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "DiscoveredTestNodeStateProperty { Explanation =  }",
            new DiscoveredTestNodeStateProperty().ToString());

    public void InProgressTestNodeStateProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "InProgressTestNodeStateProperty { Explanation = some explanation }",
            new InProgressTestNodeStateProperty("some explanation").ToString());

    public void InProgressTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "InProgressTestNodeStateProperty { Explanation =  }",
            new InProgressTestNodeStateProperty().ToString());

    public void PassedTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "PassedTestNodeStateProperty { Explanation = some explanation }",
        new PassedTestNodeStateProperty("some explanation").ToString());

    public void PassedTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "PassedTestNodeStateProperty { Explanation =  }",
            new PassedTestNodeStateProperty().ToString());

    public void SkippedTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "SkippedTestNodeStateProperty { Explanation = some explanation }",
        new SkippedTestNodeStateProperty("some explanation").ToString());

    public void SkippedTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "SkippedTestNodeStateProperty { Explanation =  }",
            new SkippedTestNodeStateProperty().ToString());

    public void FailedTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "FailedTestNodeStateProperty { Explanation = some explanation, Exception =  }",
        new FailedTestNodeStateProperty("some explanation").ToString());

    public void FailedTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "FailedTestNodeStateProperty { Explanation = , Exception =  }",
            new FailedTestNodeStateProperty().ToString());

    public void FailedTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "FailedTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message }",
            new FailedTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    public void FailedTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "FailedTestNodeStateProperty { Explanation = some message, Exception = System.Exception: some message }",
            new FailedTestNodeStateProperty(new Exception("some message")).ToString());

    public void ErrorTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "ErrorTestNodeStateProperty { Explanation = some explanation, Exception =  }",
        new ErrorTestNodeStateProperty("some explanation").ToString());

    public void ErrorTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "ErrorTestNodeStateProperty { Explanation = , Exception =  }",
            new ErrorTestNodeStateProperty().ToString());

    public void ErrorTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "ErrorTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message }",
            new ErrorTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    public void ErrorTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "ErrorTestNodeStateProperty { Explanation = some message, Exception = System.Exception: some message }",
            new ErrorTestNodeStateProperty(new Exception("some message")).ToString());

    public void TimeoutTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "TimeoutTestNodeStateProperty { Explanation = some explanation, Exception = , Timeout =  }",
        new TimeoutTestNodeStateProperty("some explanation").ToString());

    public void TimeoutTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "TimeoutTestNodeStateProperty { Explanation = , Exception = , Timeout =  }",
            new TimeoutTestNodeStateProperty().ToString());

    public void TimeoutTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "TimeoutTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message, Timeout =  }",
            new TimeoutTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    public void TimeoutTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "TimeoutTestNodeStateProperty { Explanation = some message, Exception = System.Exception: some message, Timeout =  }",
            new TimeoutTestNodeStateProperty(new Exception("some message")).ToString());

    public void CancelledTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "CancelledTestNodeStateProperty { Explanation = some explanation, Exception =  }",
        new CancelledTestNodeStateProperty("some explanation").ToString());

    public void CancelledTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "CancelledTestNodeStateProperty { Explanation = , Exception =  }",
            new CancelledTestNodeStateProperty().ToString());

    public void CancelledTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "CancelledTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message }",
            new CancelledTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    public void CancelledTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "CancelledTestNodeStateProperty { Explanation = some message, Exception = System.Exception: some message }",
            new CancelledTestNodeStateProperty(new Exception("some message")).ToString());

    public void TimingProperty_ToStringIsCorrect()
    {
        DateTimeOffset startTime = new(2021, 9, 1, 0, 0, 0, default);
        DateTimeOffset endTime = new(2021, 9, 1, 1, 2, 3, default);
        TimeSpan duration = endTime - startTime;
        Assert.AreEqual(
                "TimingProperty { GlobalTiming = TimingInfo { StartTime = 01/09/2021 00:00:00 +00:00, EndTime = 01/09/2021 01:02:03 +00:00, Duration = 01:02:03 }, StepTimings = [] }",
                new TimingProperty(new(startTime, endTime, duration)).ToString());
    }

    public void TimingProperty_WithStepTimings_ToStringIsCorrect()
    {
        DateTimeOffset startTime = new(2021, 9, 1, 0, 0, 0, default);
        DateTimeOffset endTime = new(2021, 9, 1, 1, 2, 3, default);
        TimeSpan duration = endTime - startTime;
        Assert.AreEqual(
                "TimingProperty { GlobalTiming = TimingInfo { StartTime = 01/09/2021 00:00:00 +00:00, EndTime = 01/09/2021 01:02:03 +00:00, Duration = 01:02:03 }, StepTimings = [StepTimingInfo { Id = run, Description = some description, Timing = TimingInfo { StartTime = 01/09/2021 00:00:00 +00:00, EndTime = 01/09/2021 01:02:03 +00:00, Duration = 01:02:03 } }] }",
                new TimingProperty(new(startTime, endTime, duration), [new("run", "some description", new(startTime, endTime, duration))]).ToString());
    }

    public void TestFileLocationProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TestFileLocationProperty { FilePath = some path, LineSpan = LinePositionSpan { Start = LinePosition { Line = 42, Column = 1 }, End = LinePosition { Line = 43, Column = 10 } } }",
            new TestFileLocationProperty("some path", new(new(42, 1), new(43, 10))).ToString());

    public void TestMethodIdentifierProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TestMethodIdentifierProperty { AssemblyFullName = assembly, Namespace = namespace, TypeName = type, MethodName = method, ParameterTypeFullNames = [string], ReturnTypeFullName = bool }",
            new TestMethodIdentifierProperty("assembly", "namespace", "type", "method", ["string"], "bool").ToString());

    public void SerializableNamedKeyValuePairsStringProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "SerializableNamedKeyValuePairsStringProperty { Name = some name, Pairs = [[key, value]] }",
            new SerializableNamedKeyValuePairsStringProperty("some name", [new("key", "value")]).ToString());

    public void SerializableNamedArrayStringProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "SerializableNamedArrayStringProperty { Name = some name, Values = [value] }",
            new SerializableNamedArrayStringProperty("some name", ["value"]).ToString());
}
