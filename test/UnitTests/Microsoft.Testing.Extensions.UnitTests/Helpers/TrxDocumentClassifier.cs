// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal enum TrxDocumentClassification
{
    Malformed,
    ParseableInconsistent,
    Repairable,
    Truthful,
}

internal enum TrxSchemaStatus
{
    NotChecked,
    Valid,
    Invalid,
}

internal sealed class TrxExpectedResult
{
    public required string TestId { get; init; }

    public required string ExecutionId { get; init; }

    public required string TestName { get; init; }

    public required TrxTestOutcome Outcome { get; init; }

    public required string Duration { get; init; }

    public required DateTimeOffset StartTime { get; init; }

    public required DateTimeOffset EndTime { get; init; }

    public required string ComputerName { get; init; }

    public required string TestTypeId { get; init; }

    public required string TestListId { get; init; }

    public required string RelativeResultsDirectory { get; init; }

    public required TrxExpectedDefinition Definition { get; init; }
}

internal sealed class TrxExpectedDefinition
{
    public required string Name { get; init; }

    public required string Storage { get; init; }

    public required string ClassName { get; init; }

    public required string MethodName { get; init; }

    public required string CodeBase { get; init; }

    public required string AdapterTypeName { get; init; }

    public string? Priority { get; init; }

    public string? Owner { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = [];

    public IReadOnlyList<TrxExpectedProperty> Properties { get; init; } = [];
}

internal sealed class TrxExpectedProperty
{
    public required string Key { get; init; }

    public required string Value { get; init; }
}

internal sealed class TrxExpectedRunningTest
{
    public required string TestId { get; init; }

    public required string ExecutionId { get; init; }

    public required string TestName { get; init; }

    public required string ComputerName { get; init; }

    public required DateTimeOffset StartTime { get; init; }
}

internal sealed class TrxDocumentExpectation
{
    public required Guid RunId { get; init; }

    public required string RunName { get; init; }

    public required DateTimeOffset StartTime { get; init; }

    public required DateTimeOffset FinishTime { get; init; }

    public required string SummaryOutcome { get; init; }

    public required IReadOnlyList<TrxExpectedResult> CompletedResults { get; init; }

    public IReadOnlyList<TrxExpectedRunningTest> RunningTests { get; init; } = [];

    public IReadOnlyDictionary<string, int>? Counters { get; init; }

    public IReadOnlyList<string> RootChildOrder { get; init; } =
        ["Times", "TestSettings", "Results", "TestDefinitions", "TestEntries", "TestLists", "ResultSummary"];

    public IReadOnlyList<string> SummaryChildOrder { get; init; } =
        ["Counters", "RunInfos", "CollectorDataEntries"];

    public string UncategorizedTestListId { get; init; } = "8C84FA94-04C1-424b-9868-57A2D4851A1D";

    public string AllLoadedResultsTestListId { get; init; } = "19431567-8539-422a-85D7-44EE4E166BDA";
}

internal static class TrxExpectedResultFactory
{
    internal const string UnitTestTypeId = "13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B";
    internal const string UncategorizedTestListId = "8C84FA94-04C1-424b-9868-57A2D4851A1D";

    public static TrxExpectedResult Create(
        TrxTestResult result,
        Guid executionId,
        string computerName,
        string testModule,
        string frameworkUid,
        string frameworkVersion,
        DateTimeOffset fallbackTime)
    {
        string executionIdText = executionId.ToString();
        string displayName = Sanitize(result.DisplayName);
        TrxTestMethodIdentifier? identifier = result.TestMethodIdentifier;
        string className = result.TrxFullyQualifiedTypeName
            ?? (identifier is null
                ? string.Empty
                : string.IsNullOrEmpty(identifier.Namespace)
                    ? identifier.TypeName
                    : $"{identifier.Namespace}.{identifier.TypeName}");
        string? owner = null;
        string? description = null;
        string? priority = null;
        List<TrxExpectedProperty> properties = [];
        foreach (TrxTestMetadata metadata in result.Metadata ?? [])
        {
            switch (metadata.Key)
            {
                case "Owner":
                    owner ??= Sanitize(metadata.Value);
                    break;
                case "Description":
                    description ??= Sanitize(metadata.Value);
                    break;
                case "Priority":
                    if (int.TryParse(metadata.Value, out int priorityValue) && priorityValue != int.MaxValue)
                    {
                        priority = metadata.Value;
                    }

                    break;
                default:
                    properties.Add(
                        new TrxExpectedProperty
                        {
                            Key = Sanitize(metadata.Key),
                            Value = Sanitize(metadata.Value),
                        });
                    break;
            }
        }

        return new TrxExpectedResult
        {
            TestId = Guid.Parse(result.Uid).ToString(),
            ExecutionId = executionIdText,
            TestName = displayName,
            Outcome = result.Outcome,
            Duration = result.Duration?.ToString("hh\\:mm\\:ss\\.fffffff", CultureInfo.InvariantCulture)
                ?? "00:00:00",
            StartTime = result.StartTime?.ToUniversalTime() ?? fallbackTime,
            EndTime = result.EndTime?.ToUniversalTime() ?? fallbackTime,
            ComputerName = Sanitize(computerName),
            TestTypeId = UnitTestTypeId,
            TestListId = UncategorizedTestListId,
            RelativeResultsDirectory = executionIdText,
            Definition = new TrxExpectedDefinition
            {
                Name = Sanitize(result.TrxTestDefinitionName ?? displayName),
                Storage = Sanitize(testModule.ToLowerInvariant()),
                ClassName = Sanitize(className),
                MethodName = Sanitize(identifier?.MethodName ?? displayName),
                CodeBase = Sanitize(testModule),
                AdapterTypeName = Sanitize($"executor://{frameworkUid}/{frameworkVersion}"),
                Priority = priority,
                Owner = owner,
                Description = description,
                Categories = [.. (result.Categories ?? []).Select(Sanitize)],
                Properties = properties,
            },
        };
    }

    private static string Sanitize(string value)
    {
        value = value.Replace("\r\n", "\n").Replace('\r', '\n');
        StringBuilder? builder = null;
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            bool valid = current is '\t' or '\n' or '\r'
                or (>= '\x20' and <= '\uD7FF')
                or (>= '\uE000' and <= '\uFFFD');
            bool surrogatePair = char.IsHighSurrogate(current)
                && i + 1 < value.Length
                && char.IsLowSurrogate(value[i + 1]);
            if (valid || surrogatePair)
            {
                if (builder is not null)
                {
                    _ = builder.Append(current);
                    if (surrogatePair)
                    {
                        _ = builder.Append(value[++i]);
                    }
                }
                else if (surrogatePair)
                {
                    i++;
                }

                continue;
            }

            builder ??= new StringBuilder(value, 0, i, value.Length + (value.Length / 2));
            _ = builder.Append(@"\u").Append(((ushort)current).ToString("x4", CultureInfo.InvariantCulture));
        }

        return builder?.ToString() ?? value;
    }
}

internal sealed class TrxDocumentObservationContext
{
    public int? OperationIndex { get; init; }

    public string? OperationKind { get; init; }

    public int? CommittedByteCount { get; init; }

    public long? PreviousTargetLength { get; init; }

    public int PublishedEventSequenceNumber { get; init; }

    public int LatestEventSequenceNumber { get; init; }
}

internal sealed class TrxSchemaValidationResult
{
    private TrxSchemaValidationResult(TrxSchemaStatus status, string? error)
    {
        Status = status;
        Error = error;
    }

    public TrxSchemaStatus Status { get; }

    public string? Error { get; }

    public static TrxSchemaValidationResult NotChecked() => new(TrxSchemaStatus.NotChecked, null);

    public static TrxSchemaValidationResult Valid() => new(TrxSchemaStatus.Valid, null);

    public static TrxSchemaValidationResult Invalid(string error) => new(TrxSchemaStatus.Invalid, error);
}

internal sealed class TrxDocumentObservation
{
    public required TrxDocumentClassification Classification { get; init; }

    public required TrxSchemaStatus SchemaStatus { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public required int TargetLength { get; init; }

    public required int? RecoveryLength { get; init; }

    public required int PublishedEventSequenceNumber { get; init; }

    public required int Staleness { get; init; }

    public required string ByteWindow { get; init; }

    public required string Diagnostic { get; init; }
}

internal static class TrxDocumentClassifier
{
    internal static readonly XNamespace TeamTest2010Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private static readonly string[] DefinitionChildOrder =
        ["TestCategory", "Execution", "Owners", "Description", "Properties", "TestMethod"];

    public static TrxDocumentObservation Classify(
        byte[] targetBytes,
        TrxDocumentExpectation expectation,
        TrxDocumentObservationContext? context = null,
        byte[]? completeRecoveryBytes = null,
        Func<XDocument, TrxSchemaValidationResult>? schemaValidator = null)
    {
        context ??= new TrxDocumentObservationContext();
        ClassificationResult target = ClassifyDocument(targetBytes, expectation, schemaValidator);
        TrxDocumentClassification classification = target.Classification;

        if ((classification is TrxDocumentClassification.Malformed or TrxDocumentClassification.ParseableInconsistent)
            && completeRecoveryBytes is not null)
        {
            ClassificationResult recovery = ClassifyDocument(
                completeRecoveryBytes,
                expectation,
                schemaValidator);

            if (recovery.Classification == TrxDocumentClassification.Truthful)
            {
                classification = TrxDocumentClassification.Repairable;
            }
        }

        int staleness = context.LatestEventSequenceNumber - context.PublishedEventSequenceNumber;
        var errors = target.Errors.ToList();
        if (staleness < 0)
        {
            errors.Add(
                $"Published event sequence {context.PublishedEventSequenceNumber} is newer than latest event sequence {context.LatestEventSequenceNumber}.");
            if (classification == TrxDocumentClassification.Truthful)
            {
                classification = TrxDocumentClassification.ParseableInconsistent;
            }
        }

        string byteWindow = CreateByteWindow(targetBytes, context.CommittedByteCount);
        string diagnostic = CreateDiagnostic(
            classification,
            target.SchemaStatus,
            errors,
            targetBytes.Length,
            completeRecoveryBytes?.Length,
            context,
            staleness,
            byteWindow);

        return new TrxDocumentObservation
        {
            Classification = classification,
            SchemaStatus = target.SchemaStatus,
            Errors = errors,
            TargetLength = targetBytes.Length,
            RecoveryLength = completeRecoveryBytes?.Length,
            PublishedEventSequenceNumber = context.PublishedEventSequenceNumber,
            Staleness = staleness,
            ByteWindow = byteWindow,
            Diagnostic = diagnostic,
        };
    }

    private static ClassificationResult ClassifyDocument(
        byte[] bytes,
        TrxDocumentExpectation expectation,
        Func<XDocument, TrxSchemaValidationResult>? schemaValidator)
    {
        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }
        }
        catch (DecoderFallbackException ex)
        {
            return ClassificationResult.Malformed($"Strict UTF-8 decoding failed: {ex.Message}");
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            return ClassificationResult.Malformed($"XML parsing failed at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}");
        }

        TrxSchemaValidationResult schema = schemaValidator?.Invoke(document)
            ?? TrxSchemaValidationResult.NotChecked();
        List<string> errors = [];

        ValidateDocument(document, expectation, errors);

        return new ClassificationResult(
            errors.Count == 0 ? TrxDocumentClassification.Truthful : TrxDocumentClassification.ParseableInconsistent,
            schema.Status,
            errors);
    }

    private static void ValidateDocument(XDocument document, TrxDocumentExpectation expectation, List<string> errors)
    {
        XElement? root = document.Root;
        if (root is null)
        {
            errors.Add("The XML document has no root element.");
            return;
        }

        if (root.Name != TeamTest2010Namespace + "TestRun")
        {
            errors.Add($"Root must be '{{{TeamTest2010Namespace}}}TestRun' but was '{root.Name}'.");
        }

        string? runIdText = AttributeValue(root, "id", errors);
        if (!Guid.TryParse(runIdText, out Guid runId) || runId != expectation.RunId)
        {
            errors.Add($"TestRun/@id must be '{expectation.RunId}' but was '{runIdText}'.");
        }

        CompareAttribute(root, "name", expectation.RunName, errors);
        ValidateExactOrder(root, expectation.RootChildOrder, "TestRun child order", errors);

        XElement? times = RequiredChild(root, "Times", errors);
        XElement? results = RequiredChild(root, "Results", errors);
        XElement? definitions = RequiredChild(root, "TestDefinitions", errors);
        XElement? entries = RequiredChild(root, "TestEntries", errors);
        XElement? testLists = RequiredChild(root, "TestLists", errors);
        XElement? summary = RequiredChild(root, "ResultSummary", errors);
        _ = RequiredChild(root, "TestSettings", errors);

        if (times is not null)
        {
            ValidateTimes(times, expectation, errors);
        }

        if (results is not null)
        {
            ValidateResults(results, expectation, errors);
            ValidateWhitespaceOnlyStructuralText(results, errors);
        }

        if (definitions is not null)
        {
            ValidateWhitespaceOnlyStructuralText(definitions, errors);
            foreach (XElement definition in definitions.Elements())
            {
                ValidateRelativeOrder(definition, DefinitionChildOrder, "UnitTest child order", errors);
            }
        }

        if (entries is not null)
        {
            ValidateWhitespaceOnlyStructuralText(entries, errors);
        }

        if (testLists is not null)
        {
            ValidateTestLists(testLists, expectation, errors);
        }

        if (results is not null && definitions is not null && entries is not null)
        {
            ValidateDefinitionAndEntryLinks(results, definitions, entries, expectation, errors);
        }

        if (summary is not null)
        {
            CompareAttribute(summary, "outcome", expectation.SummaryOutcome, errors);
            ValidateRelativeOrder(summary, expectation.SummaryChildOrder, "ResultSummary child order", errors);
            ValidateCounters(summary, expectation, errors);
        }
    }

    private static void ValidateTimes(XElement times, TrxDocumentExpectation expectation, List<string> errors)
    {
        DateTimeOffset? creation = ParseTimestamp(times, "creation", errors);
        DateTimeOffset? queuing = ParseTimestamp(times, "queuing", errors);
        DateTimeOffset? start = ParseTimestamp(times, "start", errors);
        DateTimeOffset? finish = ParseTimestamp(times, "finish", errors);

        CompareTimestamp("Times/@creation", creation, expectation.StartTime, errors);
        CompareTimestamp("Times/@queuing", queuing, expectation.StartTime, errors);
        CompareTimestamp("Times/@start", start, expectation.StartTime, errors);
        CompareTimestamp("Times/@finish", finish, expectation.FinishTime, errors);

        if (start is not null && finish is not null && finish.Value < start.Value)
        {
            errors.Add($"Times/@finish '{finish:O}' is earlier than Times/@start '{start:O}'.");
        }
    }

    private static void ValidateResults(XElement results, TrxDocumentExpectation expectation, List<string> errors)
    {
        List<XElement> allResults = [.. results.Elements(TeamTest2010Namespace + "UnitTestResult")];
        List<XElement> completed = [.. allResults.Where(result => result.Attribute("outcome")?.Value != "InProgress")];
        List<XElement> running = [.. allResults.Where(result => result.Attribute("outcome")?.Value == "InProgress")];

        if (completed.Count != expectation.CompletedResults.Count)
        {
            errors.Add($"Completed result count must be {expectation.CompletedResults.Count} but was {completed.Count}.");
        }

        int completedCount = Math.Min(completed.Count, expectation.CompletedResults.Count);
        for (int i = 0; i < completedCount; i++)
        {
            XElement actual = completed[i];
            TrxExpectedResult expected = expectation.CompletedResults[i];
            CompareAttribute(actual, "testId", expected.TestId, errors, $"Result[{i}]");
            CompareAttribute(actual, "executionId", expected.ExecutionId, errors, $"Result[{i}]");
            CompareAttribute(actual, "testName", expected.TestName, errors, $"Result[{i}]");
            CompareAttribute(actual, "outcome", ToXmlOutcome(expected.Outcome), errors, $"Result[{i}]");
            CompareAttribute(actual, "duration", expected.Duration, errors, $"Result[{i}]");
            CompareAttribute(actual, "computerName", expected.ComputerName, errors, $"Result[{i}]");
            CompareGuidAttribute(actual, "testType", expected.TestTypeId, errors, $"Result[{i}]");
            CompareGuidAttribute(actual, "testListId", expected.TestListId, errors, $"Result[{i}]");
            CompareAttribute(
                actual,
                "relativeResultsDirectory",
                expected.RelativeResultsDirectory,
                errors,
                $"Result[{i}]");

            DateTimeOffset? resultStart = ParseAndCompareTimestamp(
                actual,
                "startTime",
                expected.StartTime,
                $"Result[{i}]/@startTime",
                errors);
            DateTimeOffset? resultEnd = ParseAndCompareTimestamp(
                actual,
                "endTime",
                expected.EndTime,
                $"Result[{i}]/@endTime",
                errors);
            if (resultStart is not null && resultEnd is not null && resultEnd.Value < resultStart.Value)
            {
                errors.Add($"Result[{i}]/@endTime '{resultEnd:O}' is earlier than @startTime '{resultStart:O}'.");
            }

            ValidateRelativeOrder(actual, ["Output", "ResultFiles"], $"Result[{i}] child order", errors);
        }

        if (running.Count != expectation.RunningTests.Count)
        {
            errors.Add($"Running result count must be {expectation.RunningTests.Count} but was {running.Count}.");
        }

        int runningCount = Math.Min(running.Count, expectation.RunningTests.Count);
        for (int i = 0; i < runningCount; i++)
        {
            XElement actual = running[i];
            TrxExpectedRunningTest expected = expectation.RunningTests[i];
            CompareAttribute(actual, "testId", expected.TestId, errors, $"Running[{i}]");
            CompareAttribute(actual, "executionId", expected.ExecutionId, errors, $"Running[{i}]");
            CompareAttribute(actual, "testName", expected.TestName, errors, $"Running[{i}]");
            CompareAttribute(actual, "computerName", expected.ComputerName, errors, $"Running[{i}]");
            _ = ParseAndCompareTimestamp(
                actual,
                "startTime",
                expected.StartTime,
                $"Running[{i}]/@startTime",
                errors);
        }
    }

    private static void ValidateDefinitionAndEntryLinks(
        XElement results,
        XElement definitions,
        XElement entries,
        TrxDocumentExpectation expectation,
        List<string> errors)
    {
        List<XElement> resultElements = [.. results.Elements(TeamTest2010Namespace + "UnitTestResult")
            .Where(result => result.Attribute("outcome")?.Value != "InProgress")];
        List<XElement> definitionElements = [.. definitions.Elements(TeamTest2010Namespace + "UnitTest")];
        List<XElement> entryElements = [.. entries.Elements(TeamTest2010Namespace + "TestEntry")];

        int expectedDefinitionCount = expectation.CompletedResults.Select(result => result.TestId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (definitionElements.Count != expectedDefinitionCount)
        {
            errors.Add($"Definition count must be {expectedDefinitionCount} but was {definitionElements.Count}.");
        }

        if (entryElements.Count != expectation.CompletedResults.Count)
        {
            errors.Add($"Entry count must be {expectation.CompletedResults.Count} but was {entryElements.Count}.");
        }

        string[] expectedDefinitionIds =
        [
            .. expectation.CompletedResults
                .Select(result => result.TestId)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        string[] actualDefinitionIds =
        [
            .. definitionElements.Select(definition => definition.Attribute("id")?.Value ?? "<missing>"),
        ];
        if (!actualDefinitionIds.SequenceEqual(expectedDefinitionIds, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(
                $"Definition id order must be [{string.Join(", ", expectedDefinitionIds)}] but was [{string.Join(", ", actualDefinitionIds)}].");
        }

        foreach (IGrouping<string, XElement> duplicate in definitionElements
                     .Where(element => element.Attribute("id") is not null)
                     .GroupBy(element => element.Attribute("id")!.Value, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() != 1))
        {
            errors.Add($"Test id '{duplicate.Key}' has {duplicate.Count()} definitions; exactly one is required.");
        }

        for (int i = 0; i < resultElements.Count; i++)
        {
            XElement result = resultElements[i];
            TrxExpectedResult? expected = i < expectation.CompletedResults.Count
                ? expectation.CompletedResults[i]
                : null;
            string? testId = result.Attribute("testId")?.Value;
            string? executionId = result.Attribute("executionId")?.Value;
            if (testId is null || executionId is null)
            {
                continue;
            }

            int definitionCount = definitionElements.Count(definition =>
                string.Equals(definition.Attribute("id")?.Value, testId, StringComparison.OrdinalIgnoreCase));
            if (definitionCount != 1)
            {
                errors.Add($"Result[{i}] test id '{testId}' has {definitionCount} matching definitions; exactly one is required.");
            }

            int entryCount = entryElements.Count(entry =>
                string.Equals(entry.Attribute("testId")?.Value, testId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Attribute("executionId")?.Value, executionId, StringComparison.OrdinalIgnoreCase));
            if (entryCount != 1)
            {
                errors.Add($"Result[{i}] execution '{executionId}' has {entryCount} matching entries; exactly one is required.");
            }

            if (expected is not null && i < entryElements.Count)
            {
                XElement entry = entryElements[i];
                CompareAttribute(entry, "testId", expected.TestId, errors, $"Entry[{i}]");
                CompareAttribute(entry, "executionId", expected.ExecutionId, errors, $"Entry[{i}]");
                CompareGuidAttribute(entry, "testListId", expected.TestListId, errors, $"Entry[{i}]");
            }
        }

        foreach (IGrouping<string, TrxExpectedResult> expectedByTestId in expectation.CompletedResults.GroupBy(
                     result => result.TestId,
                     StringComparer.OrdinalIgnoreCase))
        {
            XElement? definition = definitionElements.SingleOrDefault(element =>
                string.Equals(element.Attribute("id")?.Value, expectedByTestId.Key, StringComparison.OrdinalIgnoreCase));
            string? definitionExecution = definition?.Element(TeamTest2010Namespace + "Execution")?.Attribute("id")?.Value;
            string expectedFirstExecution = expectedByTestId.First().ExecutionId;
            if (definition is not null
                && !string.Equals(definitionExecution, expectedFirstExecution, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Definition '{expectedByTestId.Key}' execution must link to first execution '{expectedFirstExecution}' but was '{definitionExecution}'.");
            }

            if (definition is not null)
            {
                ValidateDefinition(definition, expectedByTestId.First(), errors);
            }
        }
    }

    private static void ValidateDefinition(
        XElement definition,
        TrxExpectedResult expectedResult,
        List<string> errors)
    {
        TrxExpectedDefinition expected = expectedResult.Definition;
        string location = $"Definition[{expectedResult.TestId}]";
        CompareAttribute(definition, "name", expected.Name, errors, location);
        CompareAttribute(definition, "storage", expected.Storage, errors, location);
        CompareOptionalAttribute(definition, "priority", expected.Priority, errors, location);

        string[] categories =
        [
            .. definition
                .Element(TeamTest2010Namespace + "TestCategory")?
                .Elements(TeamTest2010Namespace + "TestCategoryItem")
                .Select(element => element.Attribute("TestCategory")?.Value ?? "<missing>")
                ?? [],
        ];
        if (!categories.SequenceEqual(expected.Categories, StringComparer.Ordinal))
        {
            errors.Add(
                $"{location} categories must be [{string.Join(", ", expected.Categories)}] but were [{string.Join(", ", categories)}].");
        }

        XElement? owners = definition.Element(TeamTest2010Namespace + "Owners");
        string? owner = owners?.Element(TeamTest2010Namespace + "Owner")?.Attribute("name")?.Value;
        CompareOptionalValue($"{location}/Owners/Owner/@name", owner, expected.Owner, errors);

        string? description = definition.Element(TeamTest2010Namespace + "Description")?.Value;
        CompareOptionalValue($"{location}/Description", description, expected.Description, errors);

        TrxExpectedProperty[] properties =
        [
            .. definition
                .Element(TeamTest2010Namespace + "Properties")?
                .Elements(TeamTest2010Namespace + "Property")
                .Select(
                    property => new TrxExpectedProperty
                    {
                        Key = property.Element(TeamTest2010Namespace + "Key")?.Value ?? "<missing>",
                        Value = property.Element(TeamTest2010Namespace + "Value")?.Value ?? "<missing>",
                    })
                ?? [],
        ];
        if (properties.Length != expected.Properties.Count
            || properties.Where(
                (property, index) => !string.Equals(property.Key, expected.Properties[index].Key, StringComparison.Ordinal)
                    || !string.Equals(property.Value, expected.Properties[index].Value, StringComparison.Ordinal)).Any())
        {
            static string FormatProperties(IEnumerable<TrxExpectedProperty> values)
                => string.Join(", ", values.Select(property => $"{property.Key}={property.Value}"));
            errors.Add(
                $"{location} properties must be [{FormatProperties(expected.Properties)}] but were [{FormatProperties(properties)}].");
        }

        XElement? method = RequiredChild(definition, "TestMethod", errors);
        if (method is not null)
        {
            CompareAttribute(method, "codeBase", expected.CodeBase, errors, $"{location}/TestMethod");
            CompareAttribute(method, "adapterTypeName", expected.AdapterTypeName, errors, $"{location}/TestMethod");
            CompareAttribute(method, "className", expected.ClassName, errors, $"{location}/TestMethod");
            CompareAttribute(method, "name", expected.MethodName, errors, $"{location}/TestMethod");
        }
    }

    private static void ValidateTestLists(
        XElement testLists,
        TrxDocumentExpectation expectation,
        List<string> errors)
    {
        XElement[] actual = [.. testLists.Elements(TeamTest2010Namespace + "TestList")];
        (string Name, string Id)[] expected =
        [
            ("Results Not in a List", expectation.UncategorizedTestListId),
            ("All Loaded Results", expectation.AllLoadedResultsTestListId),
        ];
        if (actual.Length != expected.Length)
        {
            errors.Add($"TestLists must contain {expected.Length} TestList elements but contained {actual.Length}.");
        }

        for (int i = 0; i < Math.Min(actual.Length, expected.Length); i++)
        {
            CompareAttribute(actual[i], "name", expected[i].Name, errors, $"TestList[{i}]");
            CompareGuidAttribute(actual[i], "id", expected[i].Id, errors, $"TestList[{i}]");
        }
    }

    private static void ValidateCounters(XElement summary, TrxDocumentExpectation expectation, List<string> errors)
    {
        XElement? counters = summary.Element(TeamTest2010Namespace + "Counters");
        if (counters is null)
        {
            errors.Add("ResultSummary/Counters is missing.");
            return;
        }

        IReadOnlyDictionary<string, int> expectedCounters = expectation.Counters ?? CalculateCounters(expectation);
        foreach (KeyValuePair<string, int> expected in expectedCounters)
        {
            string? actualText = counters.Attribute(expected.Key)?.Value;
            if (!int.TryParse(actualText, NumberStyles.None, CultureInfo.InvariantCulture, out int actual))
            {
                errors.Add($"Counters/@{expected.Key} must be an integer but was '{actualText}'.");
            }
            else if (actual != expected.Value)
            {
                errors.Add($"Counters/@{expected.Key} must be {expected.Value} but was {actual}.");
            }
        }
    }

    private static IReadOnlyDictionary<string, int> CalculateCounters(TrxDocumentExpectation expectation)
    {
        int passed = expectation.CompletedResults.Count(result => result.Outcome == TrxTestOutcome.Passed);
        int failed = expectation.CompletedResults.Count(result => result.Outcome == TrxTestOutcome.Failed);
        int skipped = expectation.CompletedResults.Count(result => result.Outcome == TrxTestOutcome.Skipped);
        int timedout = expectation.CompletedResults.Count(result => result.Outcome == TrxTestOutcome.Timeout);

        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["total"] = passed + failed + skipped + timedout,
            ["executed"] = passed + failed,
            ["passed"] = passed,
            ["failed"] = failed,
            ["error"] = 0,
            ["timeout"] = timedout,
            ["aborted"] = 0,
            ["inconclusive"] = 0,
            ["passedButRunAborted"] = 0,
            ["notRunnable"] = 0,
            ["notExecuted"] = skipped,
            ["disconnected"] = 0,
            ["warning"] = 0,
            ["completed"] = 0,
            ["inProgress"] = expectation.RunningTests.Count,
            ["pending"] = 0,
        };
    }

    private static DateTimeOffset? ParseTimestamp(XElement element, string attributeName, List<string> errors)
    {
        string? value = element.Attribute(attributeName)?.Value;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
        {
            errors.Add($"{element.Name.LocalName}/@{attributeName} must be a round-trip timestamp but was '{value}'.");
            return null;
        }

        string roundTrip = parsed.ToString("O", CultureInfo.InvariantCulture);
        if (!DateTimeOffset.TryParse(roundTrip, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            errors.Add($"{element.Name.LocalName}/@{attributeName} did not survive round-trip formatting.");
        }

        return parsed;
    }

    private static DateTimeOffset? ParseAndCompareTimestamp(
        XElement element,
        string attributeName,
        DateTimeOffset expected,
        string location,
        List<string> errors)
    {
        DateTimeOffset? actual = ParseTimestamp(element, attributeName, errors);
        CompareTimestamp(location, actual, expected, errors);
        string expectedText = expected.ToString("O", CultureInfo.InvariantCulture);
        string? actualText = element.Attribute(attributeName)?.Value;
        if (actual is not null && !string.Equals(actualText, expectedText, StringComparison.Ordinal))
        {
            errors.Add($"{location} text must be exactly '{expectedText}' but was '{actualText}'.");
        }

        return actual;
    }

    private static void CompareTimestamp(string location, DateTimeOffset? actual, DateTimeOffset expected, List<string> errors)
    {
        if (actual is not null && (actual.Value != expected || actual.Value.Offset != expected.Offset))
        {
            errors.Add($"{location} must be '{expected:O}' but was '{actual:O}'.");
        }
    }

    private static XElement? RequiredChild(XElement parent, string localName, List<string> errors)
    {
        List<XElement> matches = [.. parent.Elements(TeamTest2010Namespace + localName)];
        if (matches.Count != 1)
        {
            errors.Add($"{parent.Name.LocalName}/{localName} must occur exactly once but occurred {matches.Count} times.");
            return matches.FirstOrDefault();
        }

        return matches[0];
    }

    private static string? AttributeValue(XElement element, string name, List<string> errors)
    {
        string? value = element.Attribute(name)?.Value;
        if (value is null)
        {
            errors.Add($"{element.Name.LocalName}/@{name} is missing.");
        }

        return value;
    }

    private static void CompareAttribute(
        XElement element,
        string name,
        string expected,
        List<string> errors,
        string? location = null)
    {
        string? actual = element.Attribute(name)?.Value;
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add($"{location ?? element.Name.LocalName}/@{name} must be '{expected}' but was '{actual}'.");
        }
    }

    private static void CompareGuidAttribute(
        XElement element,
        string name,
        string expected,
        List<string> errors,
        string location)
    {
        string? actual = element.Attribute(name)?.Value;
        if (!Guid.TryParse(actual, out Guid actualGuid)
            || !Guid.TryParse(expected, out Guid expectedGuid)
            || actualGuid != expectedGuid)
        {
            errors.Add($"{location}/@{name} must identify '{expected}' but was '{actual}'.");
        }
    }

    private static void CompareOptionalAttribute(
        XElement element,
        string name,
        string? expected,
        List<string> errors,
        string location)
        => CompareOptionalValue($"{location}/@{name}", element.Attribute(name)?.Value, expected, errors);

    private static void CompareOptionalValue(
        string location,
        string? actual,
        string? expected,
        List<string> errors)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add($"{location} must be '{expected ?? "<absent>"}' but was '{actual ?? "<absent>"}'.");
        }
    }

    private static void ValidateExactOrder(
        XElement parent,
        IReadOnlyList<string> expected,
        string location,
        List<string> errors)
    {
        string[] actual = [.. parent.Elements().Select(element => element.Name.LocalName)];
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            errors.Add($"{location} must be [{string.Join(", ", expected)}] but was [{string.Join(", ", actual)}].");
        }
    }

    private static void ValidateRelativeOrder(
        XElement parent,
        IReadOnlyList<string> allowedOrder,
        string location,
        List<string> errors)
    {
        int previousIndex = -1;
        foreach (XElement child in parent.Elements())
        {
            int currentIndex = IndexOf(allowedOrder, child.Name.LocalName);
            if (currentIndex < 0)
            {
                errors.Add($"{location} contains unexpected child '{child.Name.LocalName}'.");
                continue;
            }

            if (currentIndex < previousIndex)
            {
                errors.Add($"{location} is invalid at '{child.Name.LocalName}'; expected relative order [{string.Join(", ", allowedOrder)}].");
                return;
            }

            previousIndex = currentIndex;
        }
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static void ValidateWhitespaceOnlyStructuralText(XElement section, List<string> errors)
    {
        foreach (XText text in section.Nodes().OfType<XText>())
        {
            if (text.Value.Any(character => !char.IsWhiteSpace(character)))
            {
                errors.Add($"{section.Name.LocalName} contains non-whitespace pad text '{Bound(text.Value, 40)}'.");
            }
        }
    }

    private static string ToXmlOutcome(TrxTestOutcome outcome)
        => outcome switch
        {
            TrxTestOutcome.Passed => "Passed",
            TrxTestOutcome.Skipped => "NotExecuted",
            TrxTestOutcome.Failed or TrxTestOutcome.Timeout => "Failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };

    private static string CreateByteWindow(byte[] bytes, int? committedByteCount)
    {
        const int windowSize = 32;
        int center = committedByteCount ?? bytes.Length;
        center = Math.Max(0, Math.Min(center, bytes.Length));
        int start = Math.Max(0, center - (windowSize / 2));
        if (start + windowSize > bytes.Length)
        {
            start = Math.Max(0, bytes.Length - windowSize);
        }

        int count = Math.Min(windowSize, bytes.Length - start);
        byte[] window = new byte[count];
        Array.Copy(bytes, start, window, 0, count);
        string hex = BitConverter.ToString(window).Replace("-", " ");
        StringBuilder text = new(count);
        foreach (byte value in window)
        {
            text.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
        }

        return $"offset={start}; count={count}; hex={hex}; text={text}";
    }

    private static string CreateDiagnostic(
        TrxDocumentClassification classification,
        TrxSchemaStatus schemaStatus,
        IReadOnlyList<string> errors,
        int targetLength,
        int? recoveryLength,
        TrxDocumentObservationContext context,
        int staleness,
        string byteWindow)
    {
        string error = errors.Count == 0 ? "<none>" : Bound(string.Join(" | ", errors.Take(3)), 360);
        return string.Format(
            CultureInfo.InvariantCulture,
            "classification={0}; schema={1}; operation={2}:{3}; cut={4}; previousLength={5}; targetLength={6}; recoveryLength={7}; eventSequence={8}; latestSequence={9}; staleness={10}; error={11}; window=[{12}]",
            classification,
            schemaStatus,
            context.OperationIndex?.ToString(CultureInfo.InvariantCulture) ?? "<none>",
            context.OperationKind ?? "<none>",
            context.CommittedByteCount?.ToString(CultureInfo.InvariantCulture) ?? "<none>",
            context.PreviousTargetLength?.ToString(CultureInfo.InvariantCulture) ?? "<none>",
            targetLength,
            recoveryLength?.ToString(CultureInfo.InvariantCulture) ?? "<none>",
            context.PublishedEventSequenceNumber,
            context.LatestEventSequenceNumber,
            staleness,
            error,
            byteWindow);
    }

    private static string Bound(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value.Substring(0, maximumLength) + "…";

    private sealed class ClassificationResult
    {
        public ClassificationResult(
            TrxDocumentClassification classification,
            TrxSchemaStatus schemaStatus,
            IReadOnlyList<string> errors)
        {
            Classification = classification;
            SchemaStatus = schemaStatus;
            Errors = errors;
        }

        public TrxDocumentClassification Classification { get; }

        public TrxSchemaStatus SchemaStatus { get; }

        public IReadOnlyList<string> Errors { get; }

        public static ClassificationResult Malformed(string error)
            => new(TrxDocumentClassification.Malformed, TrxSchemaStatus.NotChecked, [error]);
    }
}
