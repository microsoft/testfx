// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxPropertyProjectionTests
{
    private const int CapturedFieldLimit = 1024 * 1024;

    [TestMethod]
    [DataRow(null, null, "TrxExceptionProperty { Message = StackTrace =  }")]
    [DataRow("failure <&>", "stack: α", "TrxExceptionProperty { Message = failure <&>StackTrace = stack: α }")]
    public void ExceptionProperty_ToString_PreservesNullableDiagnosticText(string? message, string? stack, string expected)
        => Assert.AreEqual(expected, new TrxExceptionProperty(message, stack).ToString());

    [TestMethod]
    public void IdentityProperties_ToString_DistinguishClassAndDefinitionNames()
    {
        Assert.AreEqual(
            "TrxFullyQualifiedTypeNameProperty { FullyQualifiedTypeName = Suite.Nested+Type }",
            new TrxFullyQualifiedTypeNameProperty("Suite.Nested+Type").ToString());
        Assert.AreEqual(
            "TrxTestDefinitionName { TestDefinitionName = Parameterized(α, <&>) }",
            new TrxTestDefinitionName("Parameterized(α, <&>)").ToString());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("line <&> α")]
    public void MessageProperties_ToString_IncludeChannelAndNullableText(string? text)
    {
        Assert.AreEqual($"StandardOutputTrxMessage {{ Message = {text} }}", new StandardOutputTrxMessage(text).ToString());
        Assert.AreEqual($"StandardErrorTrxMessage {{ Message = {text} }}", new StandardErrorTrxMessage(text).ToString());
        Assert.AreEqual($"DebugOrTraceTrxMessage {{ Message = {text} }}", new DebugOrTraceTrxMessage(text).ToString());
    }

    [TestMethod]
    public void CollectionProperties_ToString_EmptyAndSingletonHaveNoTrailingDelimiter()
    {
        Assert.AreEqual("TrxMessagesProperty { Messages = [] }", new TrxMessagesProperty([]).ToString());
        Assert.AreEqual(
            "TrxMessagesProperty { Messages = [DebugOrTraceTrxMessage { Message = trace }] }",
            new TrxMessagesProperty([new DebugOrTraceTrxMessage("trace")]).ToString());
        Assert.AreEqual("TrxCategoriesProperty { Categories = [] }", new TrxCategoriesProperty([]).ToString());
        Assert.AreEqual("TrxCategoriesProperty { Categories = [unit] }", new TrxCategoriesProperty(["unit"]).ToString());
        Assert.AreEqual("TrxWorkItemsProperty { WorkItemIds = [] }", new TrxWorkItemsProperty([]).ToString());
        Assert.AreEqual("TrxWorkItemsProperty { WorkItemIds = [123] }", new TrxWorkItemsProperty(["123"]).ToString());
    }

    [TestMethod]
    public void Extract_ProducerMutatesArraysAfterProjection_ResultRetainsOriginalValues()
    {
        string[] categories = ["unit", "fast"];
        string[] workItems = ["123", "456"];
        TrxMessage[] messages = [new StandardOutputTrxMessage("original output")];
        var properties = new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new TrxCategoriesProperty(categories),
            new TrxWorkItemsProperty(workItems),
            new TrxMessagesProperty(messages));

        (TrxTestResult result, bool truncated) = Extract(properties);
        categories[0] = "changed";
        workItems[1] = "999";
        messages[0] = new StandardErrorTrxMessage("changed output");
        properties.Add(new TestMetadataProperty("late", "mutation"));

        Assert.IsFalse(truncated);
        Assert.AreSequenceEqual(new[] { "unit", "fast" }, result.Categories!);
        Assert.AreSequenceEqual(new[] { "123", "456" }, result.WorkItemIds!);
        TrxStreamMessage message = Assert.ContainsSingle(result.Messages!);
        Assert.AreEqual(TrxStreamMessageKind.StandardOutput, message.Kind);
        Assert.AreEqual("original output", message.Message);
        Assert.IsNull(result.Metadata);
    }

    [TestMethod]
    [DataRow(nameof(TimingProperty))]
    [DataRow(nameof(TrxTestDefinitionName))]
    [DataRow(nameof(TrxFullyQualifiedTypeNameProperty))]
    [DataRow(nameof(TestMethodIdentifierProperty))]
    [DataRow(nameof(TrxExceptionProperty))]
    [DataRow(nameof(TrxMessagesProperty))]
    [DataRow(nameof(TrxCategoriesProperty))]
    [DataRow(nameof(TrxWorkItemsProperty))]
    public void Extract_DuplicateSingleton_RejectsAmbiguousReportMetadata(string propertyName)
    {
        IProperty CreateProperty() => propertyName switch
        {
            nameof(TimingProperty) => new TimingProperty(new TimingInfo(
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeSpan.Zero)),
            nameof(TrxTestDefinitionName) => new TrxTestDefinitionName("definition"),
            nameof(TrxFullyQualifiedTypeNameProperty) => new TrxFullyQualifiedTypeNameProperty("Suite.Type"),
            nameof(TestMethodIdentifierProperty) => new TestMethodIdentifierProperty("tests.dll", "Suite", "Type", "Method", 0, [], "System.Void"),
            nameof(TrxExceptionProperty) => new TrxExceptionProperty("failure", "stack"),
            nameof(TrxMessagesProperty) => new TrxMessagesProperty([]),
            nameof(TrxCategoriesProperty) => new TrxCategoriesProperty([]),
            nameof(TrxWorkItemsProperty) => new TrxWorkItemsProperty([]),
            _ => throw new InvalidOperationException(propertyName),
        };

        IProperty property = CreateProperty();
        var properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance, property, CreateProperty());

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => Extract(properties));

        Assert.AreEqual($"Found multiple properties of type '{property.GetType()}'.", exception.Message);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    public void Extract_DiagnosticFieldsAtCaptureBoundary_TruncatesOnlyOversizedFields(int delta)
    {
        string diagnostic = new('x', CapturedFieldLimit + delta);

        (TrxTestResult result, bool truncated) = Extract(new PropertyBag(
            new FailedTestNodeStateProperty("failure"),
            new TrxExceptionProperty(diagnostic, diagnostic),
            new TrxMessagesProperty([new StandardErrorTrxMessage(diagnostic)])));

        string expected = delta > 0
            ? $"""
                {new string('x', CapturedFieldLimit)}
                ... [truncated by TRX streaming store]
                """.Replace("\r\n", "\n")
            : diagnostic;
        Assert.AreEqual(delta > 0, truncated);
        Assert.AreEqual(expected, result.ExceptionMessage);
        Assert.AreEqual(expected, result.ExceptionStackTrace);
        Assert.AreEqual(expected, Assert.ContainsSingle(result.Messages!).Message);
    }

    [TestMethod]
    public void Extract_EmptyCollectionsAndNullableMessages_PreserveAbsenceWithoutTruncation()
    {
        (TrxTestResult empty, bool emptyTruncated) = Extract(new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new TrxMessagesProperty([]),
            new TrxCategoriesProperty([]),
            new TrxWorkItemsProperty([])));
        Assert.IsFalse(emptyTruncated);
        Assert.IsNull(empty.Messages);
        Assert.IsNull(empty.Categories);
        Assert.IsNull(empty.WorkItemIds);

        (TrxTestResult result, bool truncated) = Extract(new PropertyBag(
            PassedTestNodeStateProperty.CachedInstance,
            new TrxMessagesProperty([new StandardOutputTrxMessage(null), new CustomTrxMessage("custom")]),
            new TrxExceptionProperty(null, string.Empty)));
        Assert.IsFalse(truncated);
        Assert.IsNull(result.ExceptionMessage);
        Assert.AreEqual(string.Empty, result.ExceptionStackTrace);
        Assert.IsNull(result.Messages![0].Message);
        Assert.AreEqual(TrxStreamMessageKind.StandardOutput, result.Messages[1].Kind);
        Assert.AreEqual("custom", result.Messages[1].Message);
    }

    private static (TrxTestResult Result, bool WasTruncated) Extract(PropertyBag properties)
        => TrxTestResultExtractor.Extract(new TestNodeUpdateMessage(
            new SessionUid("projection-session"),
            new TestNode { Uid = "projection-test", DisplayName = "Projection test", Properties = properties }));

    private sealed class CustomTrxMessage(string message) : TrxMessage(message);
}
