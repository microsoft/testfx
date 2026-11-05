// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias serverclient;

using System.Collections;

using ClientLogLevel = serverclient::Microsoft.Testing.Platform.Logging.LogLevel;
using ClientMessages = serverclient::Microsoft.Testing.Platform.Extensions.Messages;
using ClientServerLogMessage = serverclient::Microsoft.Testing.Platform.Logging.ServerLogMessage;
using SessionUid = serverclient::Microsoft.Testing.Platform.TestHost.SessionUid;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

[TestClass]
public sealed class MessageModelMutationTests
{
    private const string TimingText = "TimingInfo { StartTime = 02/03/2025 04:05:06 +02:00, EndTime = 02/03/2025 04:05:09 +02:00, Duration = 00:00:02.5000000 }";

    [TestMethod]
    [DataRow("output")]
    [DataRow("error")]
    [DataRow("metadata")]
    [DataRow("serializable")]
    [DataRow("assertion")]
    [DataRow("artifact")]
    [DataRow("retry")]
    public void Properties_EqualityAndDiagnostics_DistinguishEveryValueField(string property)
    {
        switch (property)
        {
            case "output":
                AssertValueSemantics(
                    new ClientMessages.StandardOutputProperty("output"),
                    new ClientMessages.StandardOutputProperty("output"),
                    "StandardOutputProperty { StandardOutput = output }",
                    new ClientMessages.StandardOutputProperty("other"));
                Assert.AreEqual("output", new ClientMessages.StandardOutputProperty("output").StandardOutput);
                break;
            case "error":
                AssertValueSemantics(
                    new ClientMessages.StandardErrorProperty("error"),
                    new ClientMessages.StandardErrorProperty("error"),
                    "StandardErrorProperty { StandardError = error }",
                    new ClientMessages.StandardErrorProperty("other"));
                Assert.AreEqual("error", new ClientMessages.StandardErrorProperty("error").StandardError);
                break;
            case "metadata":
                AssertValueSemantics(
                    new ClientMessages.TestMetadataProperty("category", "fast"),
                    new ClientMessages.TestMetadataProperty("category", "fast"),
                    "TestMetadataProperty { Key = category, Value = fast }",
                    new ClientMessages.TestMetadataProperty("owner", "fast"),
                    new ClientMessages.TestMetadataProperty("category", "slow"));
                Assert.AreEqual("category", new ClientMessages.TestMetadataProperty("category").Key);
                Assert.AreEqual(string.Empty, new ClientMessages.TestMetadataProperty("category").Value);
                break;
            case "serializable":
                AssertValueSemantics(
                    new ClientMessages.SerializableKeyValuePairStringProperty("custom", "value"),
                    new ClientMessages.SerializableKeyValuePairStringProperty("custom", "value"),
                    "SerializableKeyValuePairStringProperty { Key = custom, Value = value }",
                    new ClientMessages.SerializableKeyValuePairStringProperty("other", "value"),
                    new ClientMessages.SerializableKeyValuePairStringProperty("custom", "other"));
                break;
            case "assertion":
                AssertValueSemantics(
                    new ClientMessages.AssertionFailureProperty("expected", "actual"),
                    new ClientMessages.AssertionFailureProperty("expected", "actual"),
                    "AssertionFailureProperty { Expected = expected, Actual = actual }",
                    new ClientMessages.AssertionFailureProperty(null, "actual"),
                    new ClientMessages.AssertionFailureProperty("expected", null),
                    new ClientMessages.AssertionFailureProperty("other", "actual"),
                    new ClientMessages.AssertionFailureProperty("expected", "other"));
                ArgumentException invalid = Assert.ThrowsExactly<ArgumentException>(() => new ClientMessages.AssertionFailureProperty(null, null));
                Assert.AreEqual("expected", invalid.ParamName);
                Assert.StartsWith("At least one of 'expected' or 'actual' must be non-null.", invalid.Message);
                break;
            case "artifact":
                var file = new FileInfo("attachment.trx");
                AssertValueSemantics(
                    new ClientMessages.FileArtifactProperty(file, "report", "details"),
                    new ClientMessages.FileArtifactProperty(file, "report", "details"),
                    "FileArtifactProperty { FileInfo = attachment.trx, DisplayName = report, Description = details }",
                    new ClientMessages.FileArtifactProperty(new FileInfo("other.trx"), "report", "details"),
                    new ClientMessages.FileArtifactProperty(file, "other", "details"),
                    new ClientMessages.FileArtifactProperty(file, "report", null));
                break;
            case "retry":
                AssertValueSemantics(
                    new ClientMessages.RetryAttemptProperty(1, true),
                    new ClientMessages.RetryAttemptProperty(1, true),
                    "RetryAttemptProperty { AttemptNumber = 1, IsSuperseded = True }",
                    new ClientMessages.RetryAttemptProperty(2, true),
                    new ClientMessages.RetryAttemptProperty(1, false));
                Assert.AreEqual("RetryAttemptProperty { AttemptNumber = 2, IsSuperseded = False }", new ClientMessages.RetryAttemptProperty(2, false).ToString());
                break;
            default:
                Assert.Fail($"Unexpected property case '{property}'.");
                break;
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void RetryAttempt_RejectsNonPositiveAttempts(int attempt)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ClientMessages.RetryAttemptProperty(attempt, false));
        Assert.AreEqual("attemptNumber", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void RetryAttempt_OnlySupersededUpdatesAreNonFinal(bool? superseded, bool expected)
    {
        var node = new ClientMessages.TestNode { Uid = "test", DisplayName = "Test" };
        if (superseded is { } value)
        {
            node.Properties.Add(new ClientMessages.RetryAttemptProperty(2, value));
        }

        Assert.AreEqual(expected, ClientMessages.RetryAttemptPropertyExtensions.IsSupersededRetryAttempt(node));
    }

    [TestMethod]
    public void FileLocations_EqualityOperatorsAndDiagnostics_PreserveBothPositionsAndPath()
    {
        var start = new ClientMessages.LinePosition(2, 3);
        var end = new ClientMessages.LinePosition(5, 7);
        var span = new ClientMessages.LinePositionSpan(start, end);
        const string startText = "LinePosition { Line = 2, Column = 3 }";
        const string spanText = "LinePositionSpan { Start = LinePosition { Line = 2, Column = 3 }, End = LinePosition { Line = 5, Column = 7 } }";
        AssertValueSemantics(start, new ClientMessages.LinePosition(2, 3), startText, new ClientMessages.LinePosition(4, 3), new ClientMessages.LinePosition(2, 4));
        AssertValueSemantics(span, new ClientMessages.LinePositionSpan(start, end), spanText, new ClientMessages.LinePositionSpan(end, end), new ClientMessages.LinePositionSpan(start, start));
        bool equalPositionsCompareEqual = start == new ClientMessages.LinePosition(2, 3);
        bool equalPositionsCompareUnequal = start != new ClientMessages.LinePosition(2, 3);
        bool differentPositionsCompareEqual = start == end;
        bool differentPositionsCompareUnequal = start != end;
        bool equalSpansCompareEqual = span == new ClientMessages.LinePositionSpan(start, end);
        bool equalSpansCompareUnequal = span != new ClientMessages.LinePositionSpan(start, end);
        bool differentSpansCompareEqual = span == new ClientMessages.LinePositionSpan(end, start);
        bool differentSpansCompareUnequal = span != new ClientMessages.LinePositionSpan(end, start);
        Assert.IsTrue(equalPositionsCompareEqual);
        Assert.IsFalse(equalPositionsCompareUnequal);
        Assert.IsFalse(differentPositionsCompareEqual);
        Assert.IsTrue(differentPositionsCompareUnequal);
        Assert.IsTrue(equalSpansCompareEqual);
        Assert.IsFalse(equalSpansCompareUnequal);
        Assert.IsFalse(differentSpansCompareEqual);
        Assert.IsTrue(differentSpansCompareUnequal);

        var location = new ClientMessages.TestFileLocationProperty("source.cs", span);
        var equal = new ClientMessages.TestFileLocationProperty("source.cs", span);
        var otherPath = new ClientMessages.TestFileLocationProperty("other.cs", span);
        var otherSpan = new ClientMessages.TestFileLocationProperty("source.cs", new ClientMessages.LinePositionSpan(end, start));
        AssertValueSemantics(location, equal, $"TestFileLocationProperty {{ FilePath = source.cs, LineSpan = {spanText} }}", otherPath, otherSpan);
        Assert.AreEqual("source.cs", location.FilePath);
        Assert.AreEqual(span, location.LineSpan);
        Assert.IsTrue(((ClientMessages.FileLocationProperty)location).Equals(equal));
        Assert.IsFalse(((ClientMessages.FileLocationProperty)location).Equals(otherPath));
        Assert.IsFalse(((ClientMessages.FileLocationProperty)location).Equals(otherSpan));
        Assert.IsFalse(((ClientMessages.FileLocationProperty)location).Equals(null));

        var baseLocation = new DiagnosticLocation("source.cs", span);
        AssertValueSemantics<ClientMessages.FileLocationProperty>(
            baseLocation,
            new DiagnosticLocation("source.cs", span),
            $"FileLocationProperty {{ FilePath = source.cs, LineSpan = {spanText} }}",
            new DiagnosticLocation("other.cs", span),
            new DiagnosticLocation("source.cs", new ClientMessages.LinePositionSpan(end, start)));
    }

    [TestMethod]
    [DataRow("discovered")]
    [DataRow("in-progress")]
    [DataRow("passed")]
    [DataRow("skipped")]
    public void SimpleStates_EqualityAndDiagnostics_PreserveExplanation(string state)
    {
        switch (state)
        {
            case "discovered":
                AssertValueSemantics(new ClientMessages.DiscoveredTestNodeStateProperty("reason"), new ClientMessages.DiscoveredTestNodeStateProperty("reason"),
                    "DiscoveredTestNodeStateProperty { Explanation = reason }", new ClientMessages.DiscoveredTestNodeStateProperty("other"));
                Assert.IsNull(ClientMessages.DiscoveredTestNodeStateProperty.CachedInstance.Explanation);
                break;
            case "in-progress":
                AssertValueSemantics(new ClientMessages.InProgressTestNodeStateProperty("reason"), new ClientMessages.InProgressTestNodeStateProperty("reason"),
                    "InProgressTestNodeStateProperty { Explanation = reason }", new ClientMessages.InProgressTestNodeStateProperty("other"));
                Assert.IsNull(ClientMessages.InProgressTestNodeStateProperty.CachedInstance.Explanation);
                break;
            case "passed":
                AssertValueSemantics(new ClientMessages.PassedTestNodeStateProperty("reason"), new ClientMessages.PassedTestNodeStateProperty("reason"),
                    "PassedTestNodeStateProperty { Explanation = reason }", new ClientMessages.PassedTestNodeStateProperty("other"));
                Assert.IsNull(ClientMessages.PassedTestNodeStateProperty.CachedInstance.Explanation);
                break;
            case "skipped":
                AssertValueSemantics(new ClientMessages.SkippedTestNodeStateProperty("reason"), new ClientMessages.SkippedTestNodeStateProperty("reason"),
                    "SkippedTestNodeStateProperty { Explanation = reason }", new ClientMessages.SkippedTestNodeStateProperty("other"));
                Assert.IsNull(ClientMessages.SkippedTestNodeStateProperty.CachedInstance.Explanation);
                break;
            default:
                Assert.Fail($"Unexpected state case '{state}'.");
                break;
        }

        ClientMessages.TestNodeStateProperty baseState = new DiagnosticState("reason");
        AssertValueSemantics(baseState, new DiagnosticState("reason"), "DiagnosticState { Explanation = reason }", new DiagnosticState("other"));
        Assert.AreEqual("reason", baseState.Explanation);
    }

    [TestMethod]
    public void FailureStates_EqualityAndDiagnostics_DistinguishExplanationExceptionAndTimeout()
    {
        var exception = new InvalidOperationException("failure");
        var otherException = new InvalidOperationException("different");
        AssertValueSemantics(
            new ClientMessages.FailedTestNodeStateProperty(exception, "reason"),
            new ClientMessages.FailedTestNodeStateProperty(exception, "reason"),
            "FailedTestNodeStateProperty { Explanation = reason, Exception = System.InvalidOperationException: failure }",
            new ClientMessages.FailedTestNodeStateProperty(exception, "other"),
            new ClientMessages.FailedTestNodeStateProperty(otherException, "reason"));
        AssertValueSemantics(
            new ClientMessages.ErrorTestNodeStateProperty(exception, "reason"),
            new ClientMessages.ErrorTestNodeStateProperty(exception, "reason"),
            "ErrorTestNodeStateProperty { Explanation = reason, Exception = System.InvalidOperationException: failure }",
            new ClientMessages.ErrorTestNodeStateProperty(exception, "other"),
            new ClientMessages.ErrorTestNodeStateProperty(otherException, "reason"));
        AssertValueSemantics(
            new ClientMessages.TimeoutTestNodeStateProperty(exception, "reason") { Timeout = TimeSpan.FromSeconds(2) },
            new ClientMessages.TimeoutTestNodeStateProperty(exception, "reason") { Timeout = TimeSpan.FromSeconds(2) },
            "TimeoutTestNodeStateProperty { Explanation = reason, Exception = System.InvalidOperationException: failure, Timeout = 00:00:02 }",
            new ClientMessages.TimeoutTestNodeStateProperty(exception, "other") { Timeout = TimeSpan.FromSeconds(2) },
            new ClientMessages.TimeoutTestNodeStateProperty(otherException, "reason") { Timeout = TimeSpan.FromSeconds(2) },
            new ClientMessages.TimeoutTestNodeStateProperty(exception, "reason") { Timeout = TimeSpan.FromSeconds(3) });
#pragma warning disable CS0618, MTP0001 // The shipped legacy state still has equality and serialization contracts.
        AssertValueSemantics(
            new ClientMessages.CancelledTestNodeStateProperty(exception, "reason"),
            new ClientMessages.CancelledTestNodeStateProperty(exception, "reason"),
            "CancelledTestNodeStateProperty { Explanation = reason, Exception = System.InvalidOperationException: failure }",
            new ClientMessages.CancelledTestNodeStateProperty(exception, "other"),
            new ClientMessages.CancelledTestNodeStateProperty(otherException, "reason"));
        Assert.IsNull(new ClientMessages.CancelledTestNodeStateProperty().Exception);
        Assert.AreEqual("reason", new ClientMessages.CancelledTestNodeStateProperty("reason").Explanation);
#pragma warning restore CS0618, MTP0001
        Assert.IsNull(new ClientMessages.FailedTestNodeStateProperty().Exception);
        Assert.IsNull(new ClientMessages.ErrorTestNodeStateProperty().Exception);
        Assert.IsNull(new ClientMessages.TimeoutTestNodeStateProperty().Exception);
        Assert.AreEqual("reason", new ClientMessages.FailedTestNodeStateProperty("reason").Explanation);
        Assert.AreEqual("reason", new ClientMessages.ErrorTestNodeStateProperty("reason").Explanation);
        Assert.AreEqual("reason", new ClientMessages.TimeoutTestNodeStateProperty("reason").Explanation);
    }

    [TestMethod]
    [DataRow(0, "")]
    [DataRow(1, "System.String")]
    [DataRow(2, "System.String, System.Int32")]
    public void MethodIdentifier_EqualityAndDiagnostics_PreserveEveryMetadataComponent(int parameterCount, string parametersText)
    {
        string[] parameters = parameterCount switch { 0 => [], 1 => ["System.String"], _ => ["System.String", "System.Int32"] };
        var identifier = new ClientMessages.TestMethodIdentifierProperty("assembly", "ns", "Type`1+Nested", "Method", 2, parameters, "System.Void");
        AssertValueSemantics(
            identifier,
            new ClientMessages.TestMethodIdentifierProperty("assembly", "ns", "Type`1+Nested", "Method", 2, [.. parameters], "System.Void"),
            $"TestMethodIdentifierProperty {{ AssemblyFullName = assembly, Namespace = ns, TypeName = Type`1+Nested, MethodName = Method, MethodArity = 2, ParameterTypeFullNames = [{parametersText}], ReturnTypeFullName = System.Void }}",
            new ClientMessages.TestMethodIdentifierProperty("other", "ns", "Type`1+Nested", "Method", 2, parameters, "System.Void"),
            new ClientMessages.TestMethodIdentifierProperty("assembly", "other", "Type`1+Nested", "Method", 2, parameters, "System.Void"),
            new ClientMessages.TestMethodIdentifierProperty("assembly", "ns", "Other", "Method", 2, parameters, "System.Void"),
            new ClientMessages.TestMethodIdentifierProperty("assembly", "ns", "Type`1+Nested", "Other", 2, parameters, "System.Void"),
            new ClientMessages.TestMethodIdentifierProperty("assembly", "ns", "Type`1+Nested", "Method", 3, parameters, "System.Void"),
            new ClientMessages.TestMethodIdentifierProperty("assembly", "ns", "Type`1+Nested", "Method", 2, ["Other"], "System.Void"),
            new ClientMessages.TestMethodIdentifierProperty("assembly", "ns", "Type`1+Nested", "Method", 2, parameters, "Other"));
        Assert.AreEqual("assembly", identifier.AssemblyFullName);
        Assert.AreEqual("ns", identifier.Namespace);
        Assert.AreEqual("Type`1+Nested", identifier.TypeName);
        Assert.AreEqual("Method", identifier.MethodName);
        Assert.AreEqual(2, identifier.MethodArity);
        Assert.AreSequenceEqual(parameters, identifier.ParameterTypeFullNames);
        Assert.AreEqual("System.Void", identifier.ReturnTypeFullName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void Timing_EqualityAndDiagnostics_PreserveEndpointsDurationAndOrderedSteps(int stepCount)
    {
        var start = new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.FromHours(2));
        DateTimeOffset end = start.AddSeconds(3);
        var duration = TimeSpan.FromMilliseconds(2500);
        var timing = new ClientMessages.TimingInfo(start, end, duration);
        AssertValueSemantics(timing, new ClientMessages.TimingInfo(start, end, duration), TimingText,
            new ClientMessages.TimingInfo(start.AddSeconds(1), end, duration),
            new ClientMessages.TimingInfo(start, end.AddSeconds(1), duration),
            new ClientMessages.TimingInfo(start, end, duration.Add(TimeSpan.FromSeconds(1))));
        bool equalTimingsCompareEqual = timing == new ClientMessages.TimingInfo(start, end, duration);
        bool equalTimingsCompareUnequal = timing != new ClientMessages.TimingInfo(start, end, duration);
        bool differentTimingsCompareEqual = timing == default;
        bool differentTimingsCompareUnequal = timing != default;
        Assert.IsTrue(equalTimingsCompareEqual);
        Assert.IsFalse(equalTimingsCompareUnequal);
        Assert.IsFalse(differentTimingsCompareEqual);
        Assert.IsTrue(differentTimingsCompareUnequal);
        var step = new ClientMessages.StepTimingInfo("setup", "prepare", timing);
        AssertValueSemantics(step, new ClientMessages.StepTimingInfo("setup", "prepare", timing),
            $"StepTimingInfo {{ Id = setup, Description = prepare, Timing = {TimingText} }}",
            new ClientMessages.StepTimingInfo("other", "prepare", timing),
            new ClientMessages.StepTimingInfo("setup", "other", timing),
            new ClientMessages.StepTimingInfo("setup", "prepare", default));
        var secondStep = new ClientMessages.StepTimingInfo("run", "execute", timing);
        ClientMessages.StepTimingInfo[] steps = stepCount switch { 0 => [], 1 => [step], _ => [step, secondStep] };
        string stepText = stepCount switch
        {
            0 => string.Empty,
            1 => $"StepTimingInfo {{ Id = setup, Description = prepare, Timing = {TimingText} }}",
            _ => $"StepTimingInfo {{ Id = setup, Description = prepare, Timing = {TimingText} }}, StepTimingInfo {{ Id = run, Description = execute, Timing = {TimingText} }}",
        };
        AssertValueSemantics(new ClientMessages.TimingProperty(timing, steps), new ClientMessages.TimingProperty(timing, [.. steps]),
            $"TimingProperty {{ GlobalTiming = {TimingText}, StepTimings = [{stepText}] }}",
            new ClientMessages.TimingProperty(default, steps),
            new ClientMessages.TimingProperty(timing, [new ClientMessages.StepTimingInfo("different", "step", timing)]));
        Assert.IsEmpty(new ClientMessages.TimingProperty(timing).StepTimings);
        Assert.AreEqual(start, timing.StartTime);
        Assert.AreEqual(end, timing.EndTime);
        Assert.AreEqual(duration, timing.Duration);
    }

    [TestMethod]
    [DataRow("array")]
    [DataRow("enumerable")]
    [DataRow("add")]
    public void PropertyBag_ConstructionAndQueries_PreserveMultiplicityAndRejectDuplicates(string construction)
    {
        var first = new ClientMessages.TestMetadataProperty("first", "one");
        var second = new ClientMessages.TestMetadataProperty("second", "two");
        var output = new ClientMessages.StandardOutputProperty("output");
        ClientMessages.PassedTestNodeStateProperty state = ClientMessages.PassedTestNodeStateProperty.CachedInstance;
        ClientMessages.IProperty[] properties = [first, output, second, state];
        ClientMessages.PropertyBag bag = construction switch
        {
            "array" => new ClientMessages.PropertyBag(properties),
            "enumerable" => new ClientMessages.PropertyBag((IEnumerable<ClientMessages.IProperty>)properties),
            _ => new ClientMessages.PropertyBag(),
        };
        if (construction == "add")
        {
            foreach (ClientMessages.IProperty property in properties)
            {
                bag.Add(property);
            }
        }

        Assert.AreEqual(4, bag.Count);
        Assert.IsTrue(bag.Any<ClientMessages.TestNodeStateProperty>());
        Assert.IsTrue(bag.Any<ClientMessages.TestMetadataProperty>());
        Assert.IsTrue(bag.Any<ClientMessages.StandardOutputProperty>());
        Assert.IsFalse(bag.Any<ClientMessages.StandardErrorProperty>());
        Assert.IsFalse(bag.Any<ClientMessages.FailedTestNodeStateProperty>());
        Assert.AreSame(second, bag.FirstOrDefault<ClientMessages.TestMetadataProperty>());
        Assert.AreSame(output, bag.Single<ClientMessages.StandardOutputProperty>());
        Assert.AreSame(state, bag.Single<ClientMessages.TestNodeStateProperty>());
        Assert.AreSame(state, bag.FirstOrDefault<ClientMessages.TestNodeStateProperty>());
        Assert.IsNull(bag.FirstOrDefault<ClientMessages.StandardErrorProperty>());
        Assert.IsNull(bag.FirstOrDefault<ClientMessages.FailedTestNodeStateProperty>());
        Assert.IsNull(bag.SingleOrDefault<ClientMessages.FailedTestNodeStateProperty>());
        Assert.IsEmpty(bag.OfType<ClientMessages.FailedTestNodeStateProperty>());
        Assert.IsEmpty(bag.OfType<ClientMessages.StandardErrorProperty>());
        Assert.AreSequenceEqual([second, first], bag.OfType<ClientMessages.TestMetadataProperty>());
        Assert.AreSequenceEqual([output], bag.OfType<ClientMessages.StandardOutputProperty>());
        Assert.AreSequenceEqual([state], bag.OfType<ClientMessages.TestNodeStateProperty>());
        Assert.AreSequenceEqual<ClientMessages.IProperty>([second, output, first, state], bag.AsEnumerable());
        Assert.ThrowsExactly<InvalidOperationException>(() => bag.Single<ClientMessages.TestMetadataProperty>());
        Assert.ThrowsExactly<InvalidOperationException>(() => bag.Single<ClientMessages.FailedTestNodeStateProperty>());
        Assert.ThrowsExactly<InvalidOperationException>(() => bag.SingleOrDefault<ClientMessages.TestMetadataProperty>());
        Assert.ThrowsExactly<InvalidOperationException>(() => bag.Add(first));
        Assert.ThrowsExactly<InvalidOperationException>(() => bag.Add(new ClientMessages.FailedTestNodeStateProperty()));
        Assert.AreEqual(4, bag.Count);

        Assert.ThrowsExactly<InvalidOperationException>(() => new ClientMessages.PropertyBag(first, first));
        Assert.ThrowsExactly<InvalidOperationException>(() => new ClientMessages.PropertyBag(Enumerable.Repeat<ClientMessages.IProperty>(first, 2)));
        Assert.ThrowsExactly<InvalidOperationException>(() => new ClientMessages.PropertyBag(state, new ClientMessages.FailedTestNodeStateProperty()));
        IEnumerable<ClientMessages.IProperty> duplicateStates = [state, new ClientMessages.FailedTestNodeStateProperty()];
        Assert.ThrowsExactly<InvalidOperationException>(() => new ClientMessages.PropertyBag(duplicateStates));
        Assert.AreEqual("property", Assert.ThrowsExactly<ArgumentNullException>(() => bag.Add(null!)).ParamName);
        Assert.AreEqual("properties", Assert.ThrowsExactly<ArgumentNullException>(() => new ClientMessages.PropertyBag(null!)).ParamName);
        Assert.AreEqual("properties", Assert.ThrowsExactly<ArgumentNullException>(() => new ClientMessages.PropertyBag((IEnumerable<ClientMessages.IProperty>)null!)).ParamName);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PropertyBag_EmptyAndStateOnlyQueries_ReturnNoRegularProperties(bool stateOnly)
    {
        ClientMessages.PropertyBag bag = stateOnly
            ? new(ClientMessages.PassedTestNodeStateProperty.CachedInstance)
            : new();
        Assert.AreEqual(stateOnly ? 1 : 0, bag.Count);
        Assert.AreEqual(stateOnly, bag.Any<ClientMessages.PassedTestNodeStateProperty>());
        Assert.IsFalse(bag.Any<ClientMessages.TestMetadataProperty>());
        Assert.IsNull(bag.FirstOrDefault<ClientMessages.TestMetadataProperty>());
        Assert.IsNull(bag.SingleOrDefault<ClientMessages.TestMetadataProperty>());
        Assert.IsEmpty(bag.OfType<ClientMessages.TestMetadataProperty>());
        Assert.ThrowsExactly<InvalidOperationException>(() => bag.Single<ClientMessages.TestMetadataProperty>());
    }

    [TestMethod]
    public void PropertyBag_ThreeMatchesWithoutState_PreserveCountOrderingAndDiagnosticContracts()
    {
        var first = new ClientMessages.TestMetadataProperty("first", "one");
        var second = new ClientMessages.TestMetadataProperty("second", "two");
        var third = new ClientMessages.TestMetadataProperty("third", "three");
        var bag = new ClientMessages.PropertyBag(first, new ClientMessages.StandardOutputProperty("output"), second, third);

        Assert.AreEqual(4, bag.Count);
        Assert.AreSequenceEqual([third, second, first], bag.OfType<ClientMessages.TestMetadataProperty>());
        Assert.AreEqual(
            $"Found multiple properties of type '{typeof(ClientMessages.TestMetadataProperty)}'.",
            Assert.ThrowsExactly<InvalidOperationException>(() => bag.Single<ClientMessages.TestMetadataProperty>()).Message);
        Assert.AreEqual(
            $"Could not find a property of type '{typeof(ClientMessages.FailedTestNodeStateProperty)}'.",
            Assert.ThrowsExactly<InvalidOperationException>(() => bag.Single<ClientMessages.FailedTestNodeStateProperty>()).Message);
        Assert.AreEqual(
            "Duplicated property instance of type 'TestMetadataProperty { Key = first, Value = one }'",
            Assert.ThrowsExactly<InvalidOperationException>(() => bag.Add(first)).Message);

        bag.Add(ClientMessages.PassedTestNodeStateProperty.CachedInstance);
        Assert.AreEqual(5, bag.Count);
        Assert.AreEqual(
            $"Could not find a property of type '{typeof(ClientMessages.FailedTestNodeStateProperty)}'.",
            Assert.ThrowsExactly<InvalidOperationException>(() => bag.Single<ClientMessages.FailedTestNodeStateProperty>()).Message);
        Assert.AreEqual(
            "Duplicated property of type 'FailedTestNodeStateProperty { Explanation = , Exception =  }'",
            Assert.ThrowsExactly<InvalidOperationException>(() => bag.Add(new ClientMessages.FailedTestNodeStateProperty())).Message);
    }

    [TestMethod]
    public void PropertyBag_LinkedListAndEnumerators_PreserveOrderAndResetCurrent()
    {
        var first = new ClientMessages.TestMetadataProperty("first", "one");
        var output = new ClientMessages.StandardOutputProperty("output");
        var second = new ClientMessages.TestMetadataProperty("second", "two");
        var list = new ClientMessages.PropertyBag.Property(second, new ClientMessages.PropertyBag.Property(output, new ClientMessages.PropertyBag.Property(first)));
        int count = list.Count;
        bool containsSecond = list.Contains(second);
        bool containsFirst = list.Contains(first);
        bool containsDifferent = list.Contains(new ClientMessages.TestMetadataProperty("first", "one"));
        Assert.AreEqual(3, count);
        Assert.IsTrue(containsSecond);
        Assert.IsTrue(containsFirst);
        Assert.IsFalse(containsDifferent);
        Assert.IsTrue(list.Any<ClientMessages.TestMetadataProperty>());
        Assert.IsTrue(list.Any<ClientMessages.StandardOutputProperty>());
        Assert.IsFalse(list.Any<ClientMessages.StandardErrorProperty>());
        Assert.AreSequenceEqual([second, first], list.OfType<ClientMessages.TestMetadataProperty>());
        Assert.IsEmpty(list.OfType<ClientMessages.StandardErrorProperty>());
        Assert.AreEqual("property", Assert.ThrowsExactly<ArgumentNullException>(() => new ClientMessages.PropertyBag.PropertyDebugView(null!)).ParamName);

        IEnumerator<ClientMessages.IProperty>[] enumerators =
        [
            list.GetEnumerator(),
            (IEnumerator<ClientMessages.IProperty>)((IEnumerable)list).GetEnumerator(),
            new ClientMessages.PropertyBag(first, output, second).GetEnumerator(),
            new ClientMessages.PropertyBag(first, output, second).AsEnumerable().GetEnumerator(),
            (IEnumerator<ClientMessages.IProperty>)((IEnumerable)new ClientMessages.PropertyBag(first, output, second).AsEnumerable()).GetEnumerator(),
        ];
        foreach (IEnumerator<ClientMessages.IProperty> enumerator in enumerators)
        {
            using (enumerator)
            {
                Assert.AreEqual(
                    "Invalid Current state, possible wrong usage.",
                    Assert.ThrowsExactly<InvalidOperationException>(() => _ = enumerator.Current).Message);
                Assert.AreEqual(
                    "Invalid Current state, possible wrong usage.",
                    Assert.ThrowsExactly<InvalidOperationException>(() => _ = ((IEnumerator)enumerator).Current).Message);
                foreach (ClientMessages.IProperty expected in new ClientMessages.IProperty[] { second, output, first })
                {
                    Assert.IsTrue(enumerator.MoveNext());
                    Assert.AreSame(expected, enumerator.Current);
                    Assert.AreSame(expected, ((IEnumerator)enumerator).Current);
                }

                Assert.IsFalse(enumerator.MoveNext());
                enumerator.Reset();
                Assert.ThrowsExactly<InvalidOperationException>(() => _ = enumerator.Current);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreSame(second, enumerator.Current);
                enumerator.Dispose();
                Assert.ThrowsExactly<InvalidOperationException>(() => _ = enumerator.Current);
                Assert.ThrowsExactly<InvalidOperationException>(() => _ = ((IEnumerator)enumerator).Current);
            }
        }

        Assert.AreSequenceEqual<ClientMessages.IProperty>([second, output, first], new ClientMessages.PropertyBag.PropertyDebugView(list).Items);
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, null)]
    [DataRow(2, "parent")]
    public void DataMessages_Diagnostics_PreserveIdentityAndPropertyList(int propertyCount, string? parent)
    {
        var properties = new ClientMessages.PropertyBag();
        if (propertyCount > 0)
        {
            properties.Add(new ClientMessages.TestMetadataProperty("first", "one"));
        }

        if (propertyCount > 1)
        {
            properties.Add(new ClientMessages.TestMetadataProperty("second", "two"));
        }

        string propertiesText = propertyCount switch
        {
            0 => string.Empty,
            1 => " TestMetadataProperty { Key = first, Value = one } ",
            _ => " TestMetadataProperty { Key = second, Value = two }, TestMetadataProperty { Key = first, Value = one } ",
        };
        var node = new ClientMessages.TestNode { Uid = "node", DisplayName = "Node", Properties = properties };
        string nodeText = $"TestNode {{ Uid = TestNodeUid {{ Value = node }}, DisplayName = Node, Properties = [{propertiesText}] }}";
        Assert.AreEqual(nodeText, node.ToString());
        Assert.AreEqual("SessionUid { Value = session }", new SessionUid("session").ToString());
        Assert.AreEqual("TestNodeUid { Value = node }", new ClientMessages.TestNodeUid("node").ToString());
        Assert.AreEqual("value", ((ClientMessages.TestNodeUid)"value").Value);
        AssertValueSemantics(
            new ClientMessages.TestNodeUid("node"), new ClientMessages.TestNodeUid("node"),
            "TestNodeUid { Value = node }", new ClientMessages.TestNodeUid("other"));
        bool differentUidsCompareUnequal = new ClientMessages.TestNodeUid("node") != new ClientMessages.TestNodeUid("other");
        bool equalUidsCompareUnequal = new ClientMessages.TestNodeUid("node") != new ClientMessages.TestNodeUid("node");
        Assert.IsTrue(differentUidsCompareUnequal);
        Assert.IsFalse(equalUidsCompareUnequal);
        ClientMessages.TestNodeUid? absent = null;
        Assert.IsFalse(new ClientMessages.TestNodeUid("node").Equals(absent));
        Assert.AreEqual("value", Assert.ThrowsExactly<ArgumentNullException>(() => new ClientMessages.TestNodeUid(null!)).ParamName);
        Assert.ThrowsExactly<ArgumentException>(() => new ClientMessages.TestNodeUid(string.Empty));

        var data = new DiagnosticData();
        var sessionData = new DiagnosticSessionData();
        var update = new ClientMessages.TestNodeUpdateMessage(new SessionUid("session"), node, parent is null ? null : new ClientMessages.TestNodeUid(parent));
        foreach (ClientMessages.IProperty property in properties)
        {
            data.Properties.Add(property);
            sessionData.Properties.Add(property);
            update.Properties.Add(property);
        }

        string dataPropertiesText = propertyCount == 2
            ? " TestMetadataProperty { Key = first, Value = one }, TestMetadataProperty { Key = second, Value = two } "
            : propertiesText;
        Assert.AreEqual($"PropertyBagData {{ DisplayName = data, Description = details, Properties = [{dataPropertiesText}] }}", data.ToString());
        Assert.AreEqual($"DataWithSessionUid {{ DisplayName = session-data, Description = session-details, Properties = [{dataPropertiesText}] }}", sessionData.ToString());
        Assert.AreEqual(new SessionUid("session"), sessionData.SessionUid);
        Assert.AreEqual(
            $"TestNodeUpdateMessage {{ DisplayName = TestNode update, Description = This data is used to report a TestNode state change., Properties = [{dataPropertiesText}], ParentTestNodeUid = {(parent is null ? "<null>" : "TestNodeUid { Value = parent }")}, TestNode = {nodeText} }}",
            update.ToString());
        Assert.AreSame(node, update.TestNode);
        Assert.AreEqual(parent, update.ParentTestNodeUid?.Value);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void FileArtifacts_Diagnostics_PreserveFileSessionAndPropertyList(int propertyCount)
    {
        string path = Path.GetFullPath("report.trx");
        var file = new FileInfo(path);
        var artifact = new ClientMessages.FileArtifact(file, "report", "details");
        var sessionArtifact = new ClientMessages.SessionFileArtifact(new SessionUid("session"), file, "report", "details");
        for (int i = 0; i < propertyCount; i++)
        {
            artifact.Properties.Add(new ClientMessages.TestMetadataProperty(i == 0 ? "first" : "second", i == 0 ? "one" : "two"));
            sessionArtifact.Properties.Add(new ClientMessages.TestMetadataProperty(i == 0 ? "first" : "second", i == 0 ? "one" : "two"));
        }

        string properties = propertyCount switch
        {
            0 => string.Empty,
            1 => " TestMetadataProperty { Key = first, Value = one } ",
            _ => " TestMetadataProperty { Key = second, Value = two }, TestMetadataProperty { Key = first, Value = one } ",
        };
        Assert.AreEqual($"FileArtifact {{ DisplayName = report, Description = details, FilePath = {path}, Properties = [{properties}] }}", artifact.ToString());
        Assert.AreEqual(
            $"SessionFileArtifact {{ DisplayName = report, Description = details, FilePath = {path}, Properties = [{properties}], SessionUid =session, FilePath = {path} }}",
            sessionArtifact.ToString());
        Assert.AreSame(file, artifact.FileInfo);
        Assert.AreSame(file, sessionArtifact.FileInfo);
        Assert.AreEqual("session", sessionArtifact.SessionUid.Value);
        Assert.IsNull(artifact.Kind);
        Assert.IsNull(sessionArtifact.Kind);
        Assert.AreEqual("custom.report", new ClientMessages.FileArtifact(file, "report", "details", "custom.report").Kind);
        Assert.AreEqual("custom.report", new ClientMessages.SessionFileArtifact(new SessionUid("session"), file, "report", "details", "custom.report").Kind);
    }

    [TestMethod]
    public void ServerLog_DiagnosticContainsLevelAndMessageWithFinalNewline()
    {
        var message = new ClientServerLogMessage(ClientLogLevel.Warning, "diagnostic");
        const string expected = """
            Server log message:
            Level: Warning
            Message: diagnostic

            """;

        Assert.AreEqual(expected.Replace("\r\n", "\n"), message.ToString().Replace("\r\n", "\n"));
        Assert.AreEqual(ClientLogLevel.Warning, message.Level);
        Assert.AreEqual("diagnostic", message.Message);
        Assert.AreEqual("Server log message", message.DisplayName);
        Assert.AreEqual("This data represents a server log message", message.Description);
    }

    [TestMethod]
    public async Task NopLogger_DisablesAllLevelsWithoutInvokingFormatters()
    {
        var logger = new serverclient::Microsoft.Testing.Platform.Logging.NopLogger();
        int formatCalls = 0;
        string Format(string value, Exception? exception)
        {
            formatCalls++;
            return value;
        }

        Assert.IsFalse(logger.IsEnabled(ClientLogLevel.Trace));
        Assert.IsFalse(logger.IsEnabled(ClientLogLevel.Error));
        logger.Log(ClientLogLevel.Warning, "message", null, Format);
        await logger.LogAsync(ClientLogLevel.Warning, "message", null, Format);
        Assert.AreEqual(0, formatCalls);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void RpcDiagnostics_CollectionFormatting_DistinguishesNullEmptyAndMultipleMembers(int count)
    {
        var runId = new Guid("11111111-2222-3333-4444-555555555555");
        ClientMessages.TestNode[] nodes = count switch
        {
            0 => [],
            1 => [new ClientMessages.TestNode { Uid = "one", DisplayName = "First" }],
            _ =>
            [
                new ClientMessages.TestNode { Uid = "one", DisplayName = "First" },
                new ClientMessages.TestNode { Uid = "two", DisplayName = "Second" },
            ],
        };
        const string firstNode = "TestNode { Uid = TestNodeUid { Value = one }, DisplayName = First, Properties = [] }";
        const string secondNode = "TestNode { Uid = TestNodeUid { Value = two }, DisplayName = Second, Properties = [] }";
        string nodeList = count switch { 0 => "[]", 1 => $"[{firstNode}]", _ => $"[{firstNode}, {secondNode}]" };
        Assert.AreEqual(
            $"DiscoverRequestArgs {{ RunId = 11111111-2222-3333-4444-555555555555, TestNodes = {nodeList}, GraphFilter = filter }}",
            new DiscoverRequestArgs(runId, nodes, "filter").ToString());
        Assert.AreEqual(
            $"RunRequestArgs {{ RunId = 11111111-2222-3333-4444-555555555555, TestNodes = {nodeList}, GraphFilter = <null> }}",
            new RunRequestArgs(runId, nodes, null).ToString());
        Assert.AreEqual(
            "RunRequestArgs { RunId = 11111111-2222-3333-4444-555555555555, TestNodes = <null>, GraphFilter =  }",
            new RunRequestArgs(runId, null, string.Empty).ToString());

        Artifact[] artifacts = count switch
        {
            0 => [],
            1 => [new Artifact("first", "collector", "type", "First")],
            _ => [new Artifact("first", "collector", "type", "First"), new Artifact("second", "collector", "type", "Second", "details")],
        };
        const string firstArtifact = "Artifact { Uri = first, Producer = collector, Type = type, DisplayName = First, Description =  }";
        const string secondArtifact = "Artifact { Uri = second, Producer = collector, Type = type, DisplayName = Second, Description = details }";
        string artifactList = count switch { 0 => "[]", 1 => $"[{firstArtifact}]", _ => $"[{firstArtifact}, {secondArtifact}]" };
        Assert.AreEqual($"RunResponseArgs {{ Artifacts = {artifactList} }}", new RunResponseArgs(artifacts).ToString());

        var environment = new Dictionary<string, string?>();
        var metrics = new Dictionary<string, object>();
        if (count > 0)
        {
            environment.Add("first", "one");
            metrics.Add("first", 1);
        }

        if (count > 1)
        {
            environment.Add("second", null);
            metrics.Add("second", 2);
        }

        string environmentText = count switch { 0 => "[]", 1 => "[first = one]", _ => "[first = one, second = <null>]" };
        string metricsText = count switch { 0 => "[]", 1 => "[first = 1]", _ => "[first = 1, second = 2]" };
        Assert.AreEqual(
            $"ProcessInfoArgs {{ Program = runner, Args = --run, WorkingDirectory = <null>, EnvironmentVariables = {environmentText} }}",
            new ProcessInfoArgs("runner", "--run", null, environment).ToString());
        Assert.AreEqual(
            "ProcessInfoArgs { Program = runner, Args = <null>, WorkingDirectory = work, EnvironmentVariables = <null> }",
            new ProcessInfoArgs("runner", null, "work", null).ToString());
        Assert.AreEqual($"TelemetryEventArgs {{ EventName = event, Metrics = {metricsText} }}", new TelemetryEventArgs("event", metrics).ToString());
    }

    [TestMethod]
    public void RpcDiagnostics_UpdatesAndAttachments_KeepEachMemberAssociatedWithItsIdentity()
    {
        var runId = new Guid("11111111-2222-3333-4444-555555555555");
        var node = new ClientMessages.TestNode { Uid = "node", DisplayName = "Node" };
        var update = new ClientMessages.TestNodeUpdateMessage(new SessionUid("session"), node, "parent");
        const string updateText = "TestNodeUpdateMessage { DisplayName = TestNode update, Description = This data is used to report a TestNode state change., Properties = [], ParentTestNodeUid = TestNodeUid { Value = parent }, TestNode = TestNode { Uid = TestNodeUid { Value = node }, DisplayName = Node, Properties = [] } }";
        Assert.AreEqual(
            $"TestNodeStateChangedEventArgs {{ RunId = 11111111-2222-3333-4444-555555555555, Changes = [{updateText}] }}",
            new TestNodeStateChangedEventArgs(runId, [update]).ToString());
        Assert.AreEqual(
            "TestNodeStateChangedEventArgs { RunId = 11111111-2222-3333-4444-555555555555, Changes = <null> }",
            new TestNodeStateChangedEventArgs(runId, null).ToString());
        Assert.AreEqual("TestsAttachments { Attachments = [] }", new TestsAttachments([]).ToString());
        Assert.AreEqual(
            "TestsAttachments { Attachments = [RunTestAttachment { Uri = first, Producer = producer, Type = type, DisplayName = First, Description = details }, RunTestAttachment { Uri = second, Producer = , Type = , DisplayName = Second, Description =  }] }",
            new TestsAttachments(
            [
                new RunTestAttachment("first", "producer", "type", "First", "details"),
                new RunTestAttachment("second", null, null, "Second", null),
            ]).ToString());
    }

    private static void AssertValueSemantics<T>(T value, T equal, string expectedText, params T[] different)
        where T : notnull, IEquatable<T>
    {
        Assert.IsTrue(value.Equals(equal));
        object boxedValue = value;
        Assert.IsTrue(boxedValue.Equals(equal));
        Assert.IsFalse(boxedValue.Equals(null));
        Assert.IsFalse(boxedValue.Equals(new object()));
        Assert.AreEqual(value.GetHashCode(), equal.GetHashCode());
        Assert.AreEqual(expectedText, value.ToString());
        var set = new HashSet<T> { value, equal };
        Assert.HasCount(1, set);
        foreach (T other in different)
        {
            Assert.IsFalse(value.Equals(other));
            Assert.IsFalse(boxedValue.Equals(other));
            Assert.IsFalse(other.Equals(value));
            bool containsDifferent = set.Contains(other);
            Assert.IsFalse(containsDifferent);
        }
    }

    private sealed class DiagnosticLocation(string path, ClientMessages.LinePositionSpan span) : ClientMessages.FileLocationProperty(path, span);

    private sealed class DiagnosticState(string? explanation) : ClientMessages.TestNodeStateProperty(explanation);

    private sealed class DiagnosticData() : ClientMessages.PropertyBagData("data", "details");

    private sealed class DiagnosticSessionData() : ClientMessages.DataWithSessionUid("session-data", "session-details", new SessionUid("session"));
}
