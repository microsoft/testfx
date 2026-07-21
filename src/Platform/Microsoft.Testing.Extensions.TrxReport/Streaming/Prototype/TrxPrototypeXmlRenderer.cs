// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

internal sealed class TrxPrototypeCompletion
{
    public required DateTimeOffset FinishTime { get; init; }

    public bool IsTestHostCrashed { get; init; }

    public int ExitCode { get; init; }

    public string CrashText { get; init; } = string.Empty;

    public IReadOnlyList<string> AttachmentWarnings { get; init; } = [];

    public IReadOnlyList<string> CollectorAttachmentHrefs { get; init; } = [];
}

internal sealed class TrxPrototypeXmlRenderer
{
    private const string NamespaceUri = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private const string UnitTestTypeGuid = "13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B";
    private const string UncategorizedTestListId = "8C84FA94-04C1-424b-9868-57A2D4851A1D";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly string _machineName;
    private readonly string _testModule;
    private readonly string _frameworkUid;
    private readonly string _frameworkVersion;

    public TrxPrototypeXmlRenderer(
        string machineName,
        string testModule,
        string frameworkUid,
        string frameworkVersion)
    {
        _machineName = machineName;
        _testModule = testModule;
        _frameworkUid = frameworkUid;
        _frameworkVersion = frameworkVersion;
    }

    public static byte[] RenderInitial(
        Guid runId,
        string runName,
        DateTimeOffset startTime,
        int definitionPadBytes,
        int entryPadBytes,
        int summaryPadBytes,
        int counterWidth,
        int runningSlotCount,
        int runningSlotByteCapacity)
    {
        string rootStart = CreateRootStart(runId, runName);
        string times = SerializeElement(
            new XElement(
                "Times",
                new XAttribute("creation", FormatTimestamp(startTime)),
                new XAttribute("queuing", FormatTimestamp(startTime)),
                new XAttribute("start", FormatTimestamp(startTime)),
                new XAttribute("finish", FormatTimestamp(startTime))));
        string testSettings = CreateTestSettings(runName);
        string counters = RenderCounters(new TrxPrototypeCounts(), counterWidth);

        string document = string.Concat(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            rootStart,
            times,
            testSettings,
            "<TestDefinitions>",
            new string(' ', definitionPadBytes),
            "</TestDefinitions>",
            "<TestEntries>",
            new string(' ', entryPadBytes),
            "</TestEntries>",
            CreateTestLists(),
            "<ResultSummary outcome=\"Failed\"   >",
            counters,
            new string(' ', summaryPadBytes),
            "</ResultSummary>",
            "<Results>",
            new string(' ', checked(runningSlotCount * runningSlotByteCapacity)),
            "</Results></TestRun>");

        return Utf8.GetBytes(document);
    }

    public byte[] RenderCompletedResult(TrxTestResult testResult, Guid executionId, DateTimeOffset fallbackTime)
    {
        string testId = GetTestId(testResult.Uid);
        string displayName = Sanitize(testResult.DisplayName);
        string executionIdText = executionId.ToString();
        var unitTestResult = new XElement(
            "UnitTestResult",
            new XAttribute("executionId", executionIdText),
            new XAttribute("testId", testId),
            new XAttribute("testName", displayName),
            new XAttribute("computerName", Sanitize(_machineName)),
            new XAttribute(
                "duration",
                testResult.Duration?.ToString("hh\\:mm\\:ss\\.fffffff", CultureInfo.InvariantCulture)
                    ?? "00:00:00"),
            new XAttribute("startTime", FormatTimestamp(testResult.StartTime?.ToUniversalTime() ?? fallbackTime)),
            new XAttribute("endTime", FormatTimestamp(testResult.EndTime?.ToUniversalTime() ?? fallbackTime)),
            new XAttribute("testType", UnitTestTypeGuid),
            new XAttribute("outcome", ToXmlOutcome(testResult.Outcome)),
            new XAttribute("testListId", UncategorizedTestListId),
            new XAttribute("relativeResultsDirectory", executionIdText));

        AddOutput(unitTestResult, testResult);
        AddResultFiles(unitTestResult, testResult);
        return Utf8.GetBytes(SerializeElement(unitTestResult));
    }

    public byte[] RenderDefinition(TrxTestResult testResult, Guid executionId)
    {
        string testId = GetTestId(testResult.Uid);
        string displayName = Sanitize(testResult.DisplayName);
        string definitionName = Sanitize(testResult.TrxTestDefinitionName ?? displayName);
        var unitTest = new XElement(
            "UnitTest",
            new XAttribute("name", definitionName),
            new XAttribute("storage", Sanitize(_testModule.ToLowerInvariant())),
            new XAttribute("id", testId));

        if (testResult.Categories is { Count: > 0 })
        {
            unitTest.Add(
                new XElement(
                    "TestCategory",
                    testResult.Categories.Select(
                        category => new XElement(
                            "TestCategoryItem",
                            new XAttribute("TestCategory", Sanitize(category))))));
        }

        unitTest.Add(new XElement("Execution", new XAttribute("id", executionId.ToString())));
        AddDefinitionMetadata(unitTest, testResult.Metadata);

        (string className, string methodName) = GetClassAndMethodName(testResult, displayName);
        unitTest.Add(
            new XElement(
                "TestMethod",
                new XAttribute("codeBase", Sanitize(_testModule)),
                new XAttribute("adapterTypeName", Sanitize($"executor://{_frameworkUid}/{_frameworkVersion}")),
                new XAttribute("className", className),
                new XAttribute("name", methodName)));

        return Utf8.GetBytes(SerializeElement(unitTest));
    }

    public static byte[] RenderEntry(string uid, Guid executionId)
        => Utf8.GetBytes(
            SerializeElement(
                new XElement(
                    "TestEntry",
                    new XAttribute("testId", GetTestId(uid)),
                    new XAttribute("executionId", executionId.ToString()),
                    new XAttribute("testListId", UncategorizedTestListId))));

    public byte[] RenderRunningSlot(
        string uid,
        string displayName,
        Guid executionId,
        DateTimeOffset startTime,
        int byteCapacity)
    {
        string sanitizedName = Sanitize(displayName);
        byte[] unpadded = RenderRunningSlotCore(uid, sanitizedName, executionId, startTime);
        if (unpadded.Length > byteCapacity)
        {
            string truncatedName = TruncateRunningName(
                sanitizedName,
                candidate => RenderRunningSlotCore(uid, candidate, executionId, startTime).Length,
                byteCapacity);
            unpadded = RenderRunningSlotCore(uid, truncatedName, executionId, startTime);
        }

        if (unpadded.Length > byteCapacity)
        {
            throw new InvalidOperationException(
                $"The running-slot byte capacity {byteCapacity} is too small for the fixed XML attributes ({unpadded.Length} bytes required).");
        }

        byte[] padded = new byte[byteCapacity];
        Array.Copy(unpadded, padded, unpadded.Length);
        for (int i = unpadded.Length; i < padded.Length; i++)
        {
            padded[i] = (byte)' ';
        }

        return padded;
    }

    public byte[] RenderSummaryAdditions(TrxPrototypeCompletion completion)
    {
        var builder = new StringBuilder();
        XElement? runInfos = null;
        if (completion.IsTestHostCrashed)
        {
            AddRunInfo(ref runInfos, "Error", completion.CrashText, completion.FinishTime);
        }
        else if (completion.ExitCode != 0)
        {
            AddRunInfo(
                ref runInfos,
                "Error",
                $"Exit code indicates failure: '{completion.ExitCode}'. Please refer to https://aka.ms/testingplatform/exitcodes for more information.",
                completion.FinishTime);
        }

        foreach (string warning in completion.AttachmentWarnings)
        {
            AddRunInfo(ref runInfos, "Warning", warning, completion.FinishTime);
        }

        if (runInfos is not null)
        {
            _ = builder.Append(SerializeElement(runInfos));
        }

        if (completion.CollectorAttachmentHrefs.Count > 0)
        {
            var collectorDataEntries = new XElement(
                "CollectorDataEntries",
                new XElement(
                    "Collector",
                    new XAttribute("agentName", Sanitize(_machineName)),
                    new XAttribute("uri", "datacollector://prototype/1.0"),
                    new XAttribute("collectorDisplayName", "TRX prototype"),
                    new XElement(
                        "UriAttachments",
                        completion.CollectorAttachmentHrefs.Select(
                            href => new XElement(
                                "UriAttachment",
                                new XElement("A", new XAttribute("href", Sanitize(href))))))));
            _ = builder.Append(SerializeElement(collectorDataEntries));
        }

        return Utf8.GetBytes(builder.ToString());
    }

    public byte[] RenderCompact(
        Guid runId,
        string runName,
        DateTimeOffset startTime,
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<Guid> executionIds,
        TrxPrototypeCompletion completion)
    {
        if (results.Count != executionIds.Count)
        {
            throw new ArgumentException("Every result must have exactly one execution id.", nameof(executionIds));
        }

        var counts = new TrxPrototypeCounts();
        var resultBuilder = new StringBuilder();
        var definitionBuilder = new StringBuilder();
        var entryBuilder = new StringBuilder();
        var definitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < results.Count; i++)
        {
            TrxTestResult result = results[i];
            Guid executionId = executionIds[i];
            _ = resultBuilder.Append(Utf8.GetString(RenderCompletedResult(result, executionId, completion.FinishTime)));
            string testId = GetTestId(result.Uid);
            if (definitions.Add(testId))
            {
                _ = definitionBuilder.Append(Utf8.GetString(RenderDefinition(result, executionId)));
            }

            _ = entryBuilder.Append(Utf8.GetString(RenderEntry(result.Uid, executionId)));
            counts.Add(result.Outcome);
        }

        string outcome = IsFailedCompletion(completion, counts) ? "Failed" : "Completed";
        string summary = string.Concat(
            "<ResultSummary outcome=\"",
            outcome,
            "\">",
            RenderCounters(counts, counterWidth: 0),
            Utf8.GetString(RenderSummaryAdditions(completion)),
            "</ResultSummary>");

        string document = string.Concat(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            CreateRootStart(runId, runName),
            SerializeElement(
                new XElement(
                    "Times",
                    new XAttribute("creation", FormatTimestamp(startTime)),
                    new XAttribute("queuing", FormatTimestamp(startTime)),
                    new XAttribute("start", FormatTimestamp(startTime)),
                    new XAttribute("finish", FormatTimestamp(completion.FinishTime)))),
            CreateTestSettings(runName),
            "<Results>",
            resultBuilder,
            "</Results><TestDefinitions>",
            definitionBuilder,
            "</TestDefinitions><TestEntries>",
            entryBuilder,
            "</TestEntries>",
            CreateTestLists(),
            summary,
            "</TestRun>");
        return Utf8.GetBytes(document);
    }

    public void WriteJournalReplayedRecord(
        ITrxPrototypeFile destination,
        TrxTestResult result,
        Guid executionId,
        DateTimeOffset fallbackTime)
    {
        byte[] bytes = RenderCompletedResult(result, executionId, fallbackTime);
        destination.Write(bytes, 0, bytes.Length);
    }

    public static string GetTestId(string uid)
    {
        if (Guid.TryParse(uid, out Guid guid))
        {
            return guid.ToString();
        }

        byte[] hash = TestFx.Hashing.XxHash128.Hash(Encoding.Unicode.GetBytes(uid));
        return new Guid(hash).ToString();
    }

    private static string RenderCounters(TrxPrototypeCounts counts, int counterWidth)
    {
        string Format(int value)
            => counterWidth == 0
                ? value.ToString(CultureInfo.InvariantCulture)
                : value.ToString($"D{counterWidth}", CultureInfo.InvariantCulture);

        return string.Concat(
            "<Counters total=\"", Format(counts.Total),
            "\" executed=\"", Format(counts.Executed),
            "\" passed=\"", Format(counts.Passed),
            "\" failed=\"", Format(counts.Failed),
            "\" error=\"", Format(0),
            "\" timeout=\"", Format(counts.Timeout),
            "\" aborted=\"", Format(0),
            "\" inconclusive=\"", Format(0),
            "\" passedButRunAborted=\"", Format(0),
            "\" notRunnable=\"", Format(0),
            "\" notExecuted=\"", Format(counts.Skipped),
            "\" disconnected=\"", Format(0),
            "\" warning=\"", Format(0),
            "\" completed=\"", Format(0),
            "\" inProgress=\"", Format(counts.InProgress),
            "\" pending=\"", Format(0),
            "\"/>");
    }

    private static bool IsFailedCompletion(TrxPrototypeCompletion completion, TrxPrototypeCounts counts)
        => completion.IsTestHostCrashed
            || completion.ExitCode != 0
            || counts.Failed > 0
            || counts.Timeout > 0;

    private byte[] RenderRunningSlotCore(
        string uid,
        string displayName,
        Guid executionId,
        DateTimeOffset startTime)
        => Utf8.GetBytes(
            SerializeElement(
                new XElement(
                    "UnitTestResult",
                    new XAttribute("executionId", executionId.ToString()),
                    new XAttribute("testId", GetTestId(uid)),
                    new XAttribute("testName", displayName),
                    new XAttribute("computerName", Sanitize(_machineName)),
                    new XAttribute("startTime", FormatTimestamp(startTime)),
                    new XAttribute("outcome", "InProgress"))));

    private static string TruncateRunningName(string value, Func<string, int> getRenderedByteCount, int byteCapacity)
    {
        var builder = new StringBuilder();
        int index = 0;
        while (index < value.Length)
        {
            int scalarLength = char.IsHighSurrogate(value[index])
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1])
                    ? 2
                    : 1;
            _ = builder.Append(value, index, scalarLength);
            if (getRenderedByteCount(builder.ToString()) > byteCapacity)
            {
                builder.Length -= scalarLength;
                break;
            }

            index += scalarLength;
        }

        return builder.ToString();
    }

    private static void AddOutput(XElement unitTestResult, TrxTestResult testResult)
    {
        var output = new XElement("Output");
        StringBuilder? stdOut = null;
        StringBuilder? stdErr = null;
        StringBuilder? debugTrace = null;
        foreach (TrxStreamMessage message in testResult.Messages ?? [])
        {
            switch (message.Kind)
            {
                case TrxStreamMessageKind.StandardOutput:
                    stdOut?.Append(Environment.NewLine);
                    _ = (stdOut ??= new StringBuilder()).Append(message.Message);
                    break;
                case TrxStreamMessageKind.StandardError:
                    stdErr?.Append(Environment.NewLine);
                    _ = (stdErr ??= new StringBuilder()).Append(message.Message);
                    break;
                case TrxStreamMessageKind.DebugOrTrace:
                    debugTrace?.Append(Environment.NewLine);
                    _ = (debugTrace ??= new StringBuilder()).Append(message.Message);
                    break;
            }
        }

        if (stdOut is not null)
        {
            output.Add(new XElement("StdOut", Sanitize(stdOut.ToString())));
        }

        if (stdErr is not null)
        {
            output.Add(new XElement("StdErr", Sanitize(stdErr.ToString())));
        }

        if (debugTrace is not null)
        {
            output.Add(new XElement("DebugTrace", Sanitize(debugTrace.ToString())));
        }

        if (testResult.ExceptionMessage is not null || testResult.ExceptionStackTrace is not null)
        {
            var errorInfo = new XElement("ErrorInfo");
            if (testResult.ExceptionMessage is not null)
            {
                errorInfo.Add(new XElement("Message", Sanitize(testResult.ExceptionMessage)));
            }

            if (testResult.ExceptionStackTrace is not null)
            {
                errorInfo.Add(new XElement("StackTrace", Sanitize(testResult.ExceptionStackTrace)));
            }

            output.Add(errorInfo);
        }

        if (output.HasElements)
        {
            unitTestResult.Add(output);
        }
    }

    private static void AddResultFiles(XElement unitTestResult, TrxTestResult testResult)
    {
        if (testResult.FileArtifacts is not { Count: > 0 })
        {
            return;
        }

        var resultFiles = new XElement("ResultFiles");
        foreach (TrxTestFileArtifact artifact in testResult.FileArtifacts)
        {
            resultFiles.Add(
                new XElement(
                    "ResultFile",
                    new XAttribute("path", Sanitize(artifact.FullPath))));
        }

        unitTestResult.Add(resultFiles);
    }

    private static void AddDefinitionMetadata(XElement unitTest, IReadOnlyList<TrxTestMetadata>? metadata)
    {
        XElement? owners = null;
        XElement? description = null;
        XElement? properties = null;
        foreach (TrxTestMetadata item in metadata ?? [])
        {
            switch (item.Key)
            {
                case "Owner":
                    owners ??= new XElement(
                        "Owners",
                        new XElement("Owner", new XAttribute("name", Sanitize(item.Value))));
                    break;
                case "Priority":
                    if (int.TryParse(item.Value, out int priority) && priority != int.MaxValue)
                    {
                        unitTest.SetAttributeValue("priority", item.Value);
                    }

                    break;
                case "Description":
                    description ??= new XElement("Description", Sanitize(item.Value));
                    break;
                default:
                    properties ??= new XElement("Properties");
                    properties.Add(
                        new XElement(
                            "Property",
                            new XElement("Key", Sanitize(item.Key)),
                            new XElement("Value", Sanitize(item.Value))));
                    break;
            }
        }

        if (owners is not null)
        {
            unitTest.Add(owners);
        }

        if (description is not null)
        {
            unitTest.Add(description);
        }

        if (properties is not null)
        {
            unitTest.Add(properties);
        }
    }

    private static (string ClassName, string MethodName) GetClassAndMethodName(
        TrxTestResult testResult,
        string displayName)
    {
        TrxTestMethodIdentifier? identifier = testResult.TestMethodIdentifier;
        string className = testResult.TrxFullyQualifiedTypeName
            ?? (identifier is null
                ? string.Empty
                : RoslynString.IsNullOrEmpty(identifier.Namespace)
                    ? identifier.TypeName
                    : $"{identifier.Namespace}.{identifier.TypeName}");
        return (Sanitize(className), Sanitize(identifier?.MethodName ?? displayName));
    }

    private void AddRunInfo(
        ref XElement? runInfos,
        string outcome,
        string text,
        DateTimeOffset timestamp)
    {
        runInfos ??= new XElement("RunInfos");
        runInfos.Add(
            new XElement(
                "RunInfo",
                new XAttribute("computerName", Sanitize(_machineName)),
                new XAttribute("outcome", outcome),
                new XAttribute("timestamp", FormatTimestamp(timestamp)),
                new XElement("Text", Sanitize(text))));
    }

    private static string CreateRootStart(Guid runId, string runName)
    {
        string serialized = new XElement(
            XNamespace.Get(NamespaceUri) + "TestRun",
            new XAttribute("id", runId),
            new XAttribute("name", Sanitize(runName))).ToString(SaveOptions.DisableFormatting);
        int emptyElementMarker = serialized.LastIndexOf("/>", StringComparison.Ordinal);
        return serialized.Substring(0, emptyElementMarker) + ">";
    }

    private static string CreateTestSettings(string runName)
        => SerializeElement(
            new XElement(
                "TestSettings",
                new XAttribute("name", "default"),
                new XAttribute("id", Guid.Empty),
                new XElement(
                    "Deployment",
                    new XAttribute("runDeploymentRoot", Sanitize(runName)))));

    private static string CreateTestLists()
        => SerializeElement(
            new XElement(
                "TestLists",
                new XElement(
                    "TestList",
                    new XAttribute("name", "Results Not in a List"),
                    new XAttribute("id", UncategorizedTestListId)),
                new XElement(
                    "TestList",
                    new XAttribute("name", "All Loaded Results"),
                    new XAttribute("id", "19431567-8539-422a-85D7-44EE4E166BDA"))));

    private static string SerializeElement(XElement element)
        => element.ToString(SaveOptions.DisableFormatting);

    private static string ToXmlOutcome(TrxTestOutcome outcome)
        => outcome switch
        {
            TrxTestOutcome.Skipped => "NotExecuted",
            TrxTestOutcome.Passed => "Passed",
            TrxTestOutcome.Failed or TrxTestOutcome.Timeout => "Failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

    private static string FormatTimestamp(DateTimeOffset value)
        => value.ToString("O", CultureInfo.InvariantCulture);

    private static string Sanitize(string value)
    {
        StringBuilder? builder = null;
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            bool isValidChar = current is '\t' or '\n' or '\r'
                or (>= '\x20' and <= '\uD7FF')
                or (>= '\uE000' and <= '\uFFFD');
            bool isValidSurrogatePair = char.IsHighSurrogate(current)
                && i + 1 < value.Length
                && char.IsLowSurrogate(value[i + 1]);

            if (isValidChar || isValidSurrogatePair)
            {
                if (builder is not null)
                {
                    _ = builder.Append(current);
                    if (isValidSurrogatePair)
                    {
                        _ = builder.Append(value[++i]);
                    }
                }
                else if (isValidSurrogatePair)
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

    private sealed class TrxPrototypeCounts
    {
        public int Passed { get; private set; }

        public int Failed { get; private set; }

        public int Skipped { get; private set; }

        public int Timeout { get; private set; }

        public int InProgress { get; set; }

        public int Total => Passed + Failed + Skipped + Timeout;

        public int Executed => Passed + Failed;

        public void Add(TrxTestOutcome outcome)
        {
            switch (outcome)
            {
                case TrxTestOutcome.Passed:
                    Passed++;
                    break;
                case TrxTestOutcome.Failed:
                    Failed++;
                    break;
                case TrxTestOutcome.Skipped:
                    Skipped++;
                    break;
                case TrxTestOutcome.Timeout:
                    Timeout++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }
    }
}
