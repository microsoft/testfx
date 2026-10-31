// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete

using Microsoft.Testing.Platform.Messages;

namespace Microsoft.Testing.Platform.Extensions.Messages.UnitTests;

[TestClass]
public sealed class TestNodePropertiesTests
{
    [TestMethod]
    public void DiscoveredTestNodeStateProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "DiscoveredTestNodeStateProperty { Explanation = some explanation }",
            new DiscoveredTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void DiscoveredTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "DiscoveredTestNodeStateProperty { Explanation =  }",
            new DiscoveredTestNodeStateProperty().ToString());

    [TestMethod]
    public void InProgressTestNodeStateProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "InProgressTestNodeStateProperty { Explanation = some explanation }",
            new InProgressTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void InProgressTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "InProgressTestNodeStateProperty { Explanation =  }",
            new InProgressTestNodeStateProperty().ToString());

    [TestMethod]
    public void PassedTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "PassedTestNodeStateProperty { Explanation = some explanation }",
        new PassedTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void PassedTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "PassedTestNodeStateProperty { Explanation =  }",
            new PassedTestNodeStateProperty().ToString());

    [TestMethod]
    public void SkippedTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "SkippedTestNodeStateProperty { Explanation = some explanation }",
        new SkippedTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void SkippedTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "SkippedTestNodeStateProperty { Explanation =  }",
            new SkippedTestNodeStateProperty().ToString());

    [TestMethod]
    public void FailedTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "FailedTestNodeStateProperty { Explanation = some explanation, Exception =  }",
        new FailedTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void FailedTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "FailedTestNodeStateProperty { Explanation = , Exception =  }",
            new FailedTestNodeStateProperty().ToString());

    [TestMethod]
    public void FailedTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "FailedTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message }",
            new FailedTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    [TestMethod]
    public void FailedTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "FailedTestNodeStateProperty { Explanation = , Exception = System.Exception: some message }",
            new FailedTestNodeStateProperty(new Exception("some message")).ToString());

    [TestMethod]
    public void ErrorTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "ErrorTestNodeStateProperty { Explanation = some explanation, Exception =  }",
        new ErrorTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void ErrorTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "ErrorTestNodeStateProperty { Explanation = , Exception =  }",
            new ErrorTestNodeStateProperty().ToString());

    [TestMethod]
    public void ErrorTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "ErrorTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message }",
            new ErrorTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    [TestMethod]
    public void ErrorTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "ErrorTestNodeStateProperty { Explanation = , Exception = System.Exception: some message }",
            new ErrorTestNodeStateProperty(new Exception("some message")).ToString());

    [TestMethod]
    public void TimeoutTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "TimeoutTestNodeStateProperty { Explanation = some explanation, Exception = , Timeout =  }",
        new TimeoutTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void TimeoutTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "TimeoutTestNodeStateProperty { Explanation = , Exception = , Timeout =  }",
            new TimeoutTestNodeStateProperty().ToString());

    [TestMethod]
    public void TimeoutTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "TimeoutTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message, Timeout =  }",
            new TimeoutTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    [TestMethod]
    public void TimeoutTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "TimeoutTestNodeStateProperty { Explanation = , Exception = System.Exception: some message, Timeout =  }",
            new TimeoutTestNodeStateProperty(new Exception("some message")).ToString());

    [TestMethod]
    public void CancelledTestNodeStateProperty_ToStringIsCorrect()
    => Assert.AreEqual(
        "CancelledTestNodeStateProperty { Explanation = some explanation, Exception =  }",
        new CancelledTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void CancelledTestNodeStateProperty_WhenNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "CancelledTestNodeStateProperty { Explanation = , Exception =  }",
            new CancelledTestNodeStateProperty().ToString());

    [TestMethod]
    public void CancelledTestNodeStateProperty_WhenExceptionAndExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "CancelledTestNodeStateProperty { Explanation = some explanation, Exception = System.Exception: some message }",
            new CancelledTestNodeStateProperty(new Exception("some message"), "some explanation").ToString());

    [TestMethod]
    public void CancelledTestNodeStateProperty_WhenExceptionAndNoExplanation_ToStringIsCorrect()
        => Assert.AreEqual(
            "CancelledTestNodeStateProperty { Explanation = , Exception = System.Exception: some message }",
            new CancelledTestNodeStateProperty(new Exception("some message")).ToString());

    [TestMethod]
    public void CustomTestNodeStateProperty_ToStringUsesRuntimeTypeName()
        => Assert.AreEqual(
            "CustomTestNodeStateProperty { Explanation = some explanation }",
            new CustomTestNodeStateProperty("some explanation").ToString());

    [TestMethod]
    public void TimingProperty_ToStringIsCorrect()
    {
        DateTimeOffset startTime = new(2021, 9, 1, 0, 0, 0, default);
        DateTimeOffset endTime = new(2021, 9, 1, 1, 2, 3, default);
        TimeSpan duration = endTime - startTime;
        Assert.AreEqual(
                "TimingProperty { GlobalTiming = TimingInfo { StartTime = 09/01/2021 00:00:00 +00:00, EndTime = 09/01/2021 01:02:03 +00:00, Duration = 01:02:03 }, StepTimings = [] }",
                new TimingProperty(new(startTime, endTime, duration)).ToString());
    }

    [TestMethod]
    public void TimingProperty_WithStepTimings_ToStringIsCorrect()
    {
        DateTimeOffset startTime = new(2021, 9, 1, 0, 0, 0, default);
        DateTimeOffset endTime = new(2021, 9, 1, 1, 2, 3, default);
        TimeSpan duration = endTime - startTime;
        Assert.AreEqual(
                "TimingProperty { GlobalTiming = TimingInfo { StartTime = 09/01/2021 00:00:00 +00:00, EndTime = 09/01/2021 01:02:03 +00:00, Duration = 01:02:03 }, StepTimings = [StepTimingInfo { Id = run, Description = some description, Timing = TimingInfo { StartTime = 09/01/2021 00:00:00 +00:00, EndTime = 09/01/2021 01:02:03 +00:00, Duration = 01:02:03 } }] }",
                new TimingProperty(new(startTime, endTime, duration), [new("run", "some description", new(startTime, endTime, duration))]).ToString());
    }

    [TestMethod]
    public void TestFileLocationProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TestFileLocationProperty { FilePath = some path, LineSpan = LinePositionSpan { Start = LinePosition { Line = 42, Column = 1 }, End = LinePosition { Line = 43, Column = 10 } } }",
            new TestFileLocationProperty("some path", new(new(42, 1), new(43, 10))).ToString());

    [TestMethod]
    public void TestMethodIdentifierProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TestMethodIdentifierProperty { AssemblyFullName = assembly, Namespace = namespace, TypeName = type, MethodName = method, MethodArity = 0, ParameterTypeFullNames = [string], ReturnTypeFullName = bool }",
            new TestMethodIdentifierProperty("assembly", "namespace", "type", "method", 0, ["string"], "bool").ToString());

    [TestMethod]
    public void TestMetadataProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TestMetadataProperty { Key = some name, Value = some value }",
            new TestMetadataProperty("some name", "some value").ToString());

    [TestMethod]
    [DataRow(0, true)]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(3, false)]
    public void TimingInfo_Equality_UsesEveryComponent(int changedComponent, bool expectedEqual)
    {
        DateTimeOffset start = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        DateTimeOffset end = start.AddSeconds(5);
        TimingInfo value = new(start, end, TimeSpan.FromSeconds(5));
        TimingInfo other = new(
            changedComponent == 1 ? start.AddTicks(1) : start,
            changedComponent == 2 ? end.AddTicks(1) : end,
            changedComponent == 3 ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(5));

        Assert.AreEqual(expectedEqual, value.Equals(other));
        Assert.AreEqual(expectedEqual, value.Equals((object)other));
        Assert.AreEqual(expectedEqual, value == other);
        Assert.AreEqual(!expectedEqual, value != other);
        Assert.IsFalse(value.Equals(null));
        Assert.IsFalse(value.Equals("not a timing"));
        if (expectedEqual)
        {
            Assert.AreEqual(value.GetHashCode(), other.GetHashCode());
        }
    }

    [TestMethod]
    public void StepTimingInfo_Equality_UsesEveryComponent()
    {
        DateTimeOffset start = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        TimingInfo timing = new(start, start.AddSeconds(5), TimeSpan.FromSeconds(5));
        TimingInfo changedTiming = new(start, start.AddSeconds(5), TimeSpan.FromSeconds(6));

        AssertReferenceValueEquality(
            new StepTimingInfo("run", "Execute tests", timing),
            new StepTimingInfo("run", "Execute tests", timing),
            new StepTimingInfo("setup", "Execute tests", timing),
            new StepTimingInfo("run", "Initialize tests", timing),
            new StepTimingInfo("run", "Execute tests", changedTiming));
    }

    [TestMethod]
    public void TimingProperty_Equality_UsesStructuralOrderedSteps()
    {
        DateTimeOffset start = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        TimingInfo timing = new(start, start.AddSeconds(5), TimeSpan.FromSeconds(5));
        TimingInfo changedTiming = new(start, start.AddSeconds(5), TimeSpan.FromSeconds(6));
        StepTimingInfo[] steps = [new("setup", "Initialize", timing), new("run", "Execute", timing)];

        AssertReferenceValueEquality(
            new TimingProperty(timing, steps),
            new TimingProperty(timing, [new("setup", "Initialize", timing), new("run", "Execute", timing)]),
            new TimingProperty(changedTiming, steps),
            new TimingProperty(timing, [new("setup", "Initialize", timing)]),
            new TimingProperty(timing, [new("run", "Execute", timing), new("setup", "Initialize", timing)]),
            new TimingProperty(timing, [new("setup", "Changed", timing), new("run", "Execute", timing)]),
            new TimingProperty(timing, [new("setup", "Initialize", changedTiming), new("run", "Execute", timing)]));
    }

    [TestMethod]
    public void TimingProperty_MultipleSteps_PreservesDiagnosticOrder()
    {
        DateTimeOffset start = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        TimingInfo timing = new(start, start.AddSeconds(5), TimeSpan.FromSeconds(5));
        TimingProperty property = new(timing, [new("setup", "Initialize", timing), new("run", "Execute", timing)]);

        Assert.AreEqual(
            "TimingProperty { GlobalTiming = TimingInfo { StartTime = 01/02/2024 03:04:05 +00:00, EndTime = 01/02/2024 03:04:10 +00:00, Duration = 00:00:05 }, StepTimings = [StepTimingInfo { Id = setup, Description = Initialize, Timing = TimingInfo { StartTime = 01/02/2024 03:04:05 +00:00, EndTime = 01/02/2024 03:04:10 +00:00, Duration = 00:00:05 } }, StepTimingInfo { Id = run, Description = Execute, Timing = TimingInfo { StartTime = 01/02/2024 03:04:05 +00:00, EndTime = 01/02/2024 03:04:10 +00:00, Duration = 00:00:05 } }] }",
            property.ToString());
    }

    [TestMethod]
    [DataRow(42, 1, true)]
    [DataRow(43, 1, false)]
    [DataRow(42, 2, false)]
    public void LinePosition_Equality_UsesLineAndColumn(int line, int column, bool expectedEqual)
    {
        LinePosition value = new(42, 1);
        LinePosition other = new(line, column);

        Assert.AreEqual(expectedEqual, value.Equals(other));
        Assert.AreEqual(expectedEqual, value.Equals((object)other));
        Assert.AreEqual(expectedEqual, value == other);
        Assert.AreEqual(!expectedEqual, value != other);
        Assert.IsFalse(value.Equals(null));
        Assert.IsFalse(value.Equals("42:1"));
        if (expectedEqual)
        {
            Assert.AreEqual(value.GetHashCode(), other.GetHashCode());
        }
    }

    [TestMethod]
    [DataRow(42, 1, 43, 10, true)]
    [DataRow(41, 1, 43, 10, false)]
    [DataRow(42, 2, 43, 10, false)]
    [DataRow(42, 1, 44, 10, false)]
    [DataRow(42, 1, 43, 11, false)]
    public void LinePositionSpan_Equality_UsesBothEndpoints(int startLine, int startColumn, int endLine, int endColumn, bool expectedEqual)
    {
        LinePositionSpan value = new(new(42, 1), new(43, 10));
        LinePositionSpan other = new(new(startLine, startColumn), new(endLine, endColumn));

        Assert.AreEqual(expectedEqual, value.Equals(other));
        Assert.AreEqual(expectedEqual, value.Equals((object)other));
        Assert.AreEqual(expectedEqual, value == other);
        Assert.AreEqual(!expectedEqual, value != other);
        Assert.IsFalse(value.Equals(null));
        Assert.IsFalse(value.Equals("not a span"));
        if (expectedEqual)
        {
            Assert.AreEqual(value.GetHashCode(), other.GetHashCode());
        }
    }

    [TestMethod]
    public void FileLocation_Equality_UsesPathAndSpan()
    {
        LinePositionSpan span = new(new(42, 1), new(43, 10));
        LinePositionSpan changedSpan = new(new(42, 1), new(44, 10));

        AssertReferenceValueEquality<FileLocationProperty>(
            new CustomFileLocationProperty("tests.cs", span),
            new CustomFileLocationProperty("tests.cs", span),
            new CustomFileLocationProperty("other.cs", span),
            new CustomFileLocationProperty("tests.cs", changedSpan));
        AssertReferenceValueEquality(
            new TestFileLocationProperty("tests.cs", span),
            new TestFileLocationProperty("tests.cs", span),
            new TestFileLocationProperty("other.cs", span),
            new TestFileLocationProperty("tests.cs", changedSpan));
        Assert.AreEqual(
            "FileLocationProperty { FilePath = tests.cs, LineSpan = LinePositionSpan { Start = LinePosition { Line = 42, Column = 1 }, End = LinePosition { Line = 43, Column = 10 } } }",
            new CustomFileLocationProperty("tests.cs", span).ToString());
    }

    [TestMethod]
    public void StateProperties_Equality_PreservesExplanationsAndExceptionIdentity()
    {
        InvalidOperationException exception = new("failure");
        InvalidOperationException differentException = new("failure");
        AssertReferenceValueEquality<TestNodeStateProperty>(
            new CustomTestNodeStateProperty("reason"),
            new CustomTestNodeStateProperty("reason"),
            new CustomTestNodeStateProperty("different"));
        AssertReferenceValueEquality(new DiscoveredTestNodeStateProperty("reason"), new DiscoveredTestNodeStateProperty("reason"), new DiscoveredTestNodeStateProperty("different"));
        AssertReferenceValueEquality(new InProgressTestNodeStateProperty("reason"), new InProgressTestNodeStateProperty("reason"), new InProgressTestNodeStateProperty("different"));
        AssertReferenceValueEquality(new PassedTestNodeStateProperty("reason"), new PassedTestNodeStateProperty("reason"), new PassedTestNodeStateProperty("different"));
        AssertReferenceValueEquality(new SkippedTestNodeStateProperty("reason"), new SkippedTestNodeStateProperty("reason"), new SkippedTestNodeStateProperty("different"));
        AssertReferenceValueEquality(
            new FailedTestNodeStateProperty(exception, "reason"),
            new FailedTestNodeStateProperty(exception, "reason"),
            new FailedTestNodeStateProperty(exception, "different"),
            new FailedTestNodeStateProperty(differentException, "reason"));
        AssertReferenceValueEquality(
            new ErrorTestNodeStateProperty(exception, "reason"),
            new ErrorTestNodeStateProperty(exception, "reason"),
            new ErrorTestNodeStateProperty(exception, "different"),
            new ErrorTestNodeStateProperty(differentException, "reason"));
        AssertReferenceValueEquality(
            new CancelledTestNodeStateProperty(exception, "reason"),
            new CancelledTestNodeStateProperty(exception, "reason"),
            new CancelledTestNodeStateProperty(exception, "different"),
            new CancelledTestNodeStateProperty(differentException, "reason"));
        AssertReferenceValueEquality(
            new TimeoutTestNodeStateProperty(exception, "reason") { Timeout = TimeSpan.FromSeconds(5) },
            new TimeoutTestNodeStateProperty(exception, "reason") { Timeout = TimeSpan.FromSeconds(5) },
            new TimeoutTestNodeStateProperty(exception, "different") { Timeout = TimeSpan.FromSeconds(5) },
            new TimeoutTestNodeStateProperty(differentException, "reason") { Timeout = TimeSpan.FromSeconds(5) },
            new TimeoutTestNodeStateProperty(exception, "reason") { Timeout = TimeSpan.FromSeconds(6) },
            new TimeoutTestNodeStateProperty(exception, "reason"));
    }

    [TestMethod]
    public void StateProperties_Equality_NullExplanationsAreValueEqual()
    {
        AssertReferenceValueEquality<TestNodeStateProperty>(new CustomTestNodeStateProperty(null), new CustomTestNodeStateProperty(null));
        AssertReferenceValueEquality(new DiscoveredTestNodeStateProperty(), DiscoveredTestNodeStateProperty.CachedInstance);
        AssertReferenceValueEquality(new InProgressTestNodeStateProperty(), InProgressTestNodeStateProperty.CachedInstance);
        AssertReferenceValueEquality(new PassedTestNodeStateProperty(), PassedTestNodeStateProperty.CachedInstance);
        AssertReferenceValueEquality(new SkippedTestNodeStateProperty(), SkippedTestNodeStateProperty.CachedInstance);
    }

    [TestMethod]
    public void StateCategories_SeparateDiscoveryOutcomesAndFailures()
    {
        Type[] discovered = [typeof(DiscoveredTestNodeStateProperty)];
        Type[] outcomes = [typeof(PassedTestNodeStateProperty), typeof(FailedTestNodeStateProperty), typeof(ErrorTestNodeStateProperty), typeof(TimeoutTestNodeStateProperty), typeof(CancelledTestNodeStateProperty)];
        Type[] failed = [typeof(FailedTestNodeStateProperty), typeof(ErrorTestNodeStateProperty), typeof(TimeoutTestNodeStateProperty), typeof(CancelledTestNodeStateProperty)];

        Assert.AreSequenceEqual(
            discovered,
            TestNodePropertiesCategories.WellKnownTestNodeDiscoveredProperties,
            SequenceOrder.InAnyOrder);
        Assert.AreSequenceEqual(
            outcomes,
            TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeProperties,
            SequenceOrder.InAnyOrder);
        Assert.AreSequenceEqual(
            failed,
            TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties,
            SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void MetadataProperties_Equality_UsesBothKeyAndValue()
    {
        AssertReferenceValueEquality(
            new TestMetadataProperty("category", "fast"),
            new TestMetadataProperty("category", "fast"),
            new TestMetadataProperty("priority", "fast"),
            new TestMetadataProperty("category", "slow"));
        AssertReferenceValueEquality(
            new SerializableKeyValuePairStringProperty("category", "fast"),
            new SerializableKeyValuePairStringProperty("category", "fast"),
            new SerializableKeyValuePairStringProperty("priority", "fast"),
            new SerializableKeyValuePairStringProperty("category", "slow"));
        Assert.AreEqual(string.Empty, new TestMetadataProperty("category").Value);
        Assert.AreEqual(
            "SerializableKeyValuePairStringProperty { Key = category, Value = fast }",
            new SerializableKeyValuePairStringProperty("category", "fast").ToString());
    }

    private static void AssertReferenceValueEquality<T>(T value, T equal, params T[] different)
        where T : class, IEquatable<T>
    {
        Assert.IsTrue(value.Equals(equal));
        Assert.IsTrue(value.Equals((object)equal));
        Assert.AreEqual(value.GetHashCode(), equal.GetHashCode());
        Assert.IsFalse(value.Equals(null));
        Assert.IsFalse(value.Equals(new object()));
        foreach (T other in different)
        {
            Assert.IsFalse(value.Equals(other));
            Assert.IsFalse(value.Equals((object)other));
        }
    }

    private sealed class CustomFileLocationProperty(string filePath, LinePositionSpan lineSpan) : FileLocationProperty(filePath, lineSpan);

    private sealed class CustomTestNodeStateProperty : TestNodeStateProperty
    {
        public CustomTestNodeStateProperty(string? explanation)
            : base(explanation)
        {
        }
    }
}
