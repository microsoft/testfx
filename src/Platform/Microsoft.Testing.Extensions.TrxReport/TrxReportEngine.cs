// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private const string UnitTestTypeGuid = "13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B";
    private const string UncategorizedTestListId = "8C84FA94-04C1-424b-9868-57A2D4851A1D";

    private static readonly Regex ReservedFileNamesRegex = BuildReservedFileNameRegex();
    private static readonly Regex InvalidXmlCharReplace = BuildInvalidXmlCharReplace();
    private static readonly MatchEvaluator InvalidXmlEvaluator = ReplaceInvalidCharacterWithUniCodeEscapeSequence;

    private static readonly HashSet<char> InvalidFileNameChars =
    [
        '\"',
        '<',
        '>',
        '|',
        '\0',
        (char)1,
        (char)2,
        (char)3,
        (char)4,
        (char)5,
        (char)6,
        (char)7,
        (char)8,
        (char)9,
        (char)10,
        (char)11,
        (char)12,
        (char)13,
        (char)14,
        (char)15,
        (char)16,
        (char)17,
        (char)18,
        (char)19,
        (char)20,
        (char)21,
        (char)22,
        (char)23,
        (char)24,
        (char)25,
        (char)26,
        (char)27,
        (char)28,
        (char)29,
        (char)30,
        (char)31,
        ':',
        '*',
        '?',
        '\\',
        '/',
        '@',
        '(',
        ')',
        '^',
        ' '
    ];

    private static readonly XNamespace NamespaceUri = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IEnvironment _environment;
    private readonly ICommandLineOptions _commandLineOptionsService;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly Dictionary<IExtension, List<SessionFileArtifact>> _artifactsByExtension;
    private readonly ITestFramework _testFrameworkAdapter;
    private readonly DateTimeOffset _testStartTime;
#if NETCOREAPP
    private readonly CancellationToken _cancellationToken;
#endif
    private readonly int _exitCode;
    private readonly IFileSystem _fileSystem;

    public TrxReportEngine(
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IEnvironment environment,
        ICommandLineOptions commandLineOptionsService,
        IConfiguration configuration,
        IClock clock,
        Dictionary<IExtension, List<SessionFileArtifact>> artifactsByExtension,
        ITestFramework testFrameworkAdapter,
        DateTimeOffset testStartTime,
#if NETCOREAPP
        int exitCode,
        CancellationToken cancellationToken)
#else
        int exitCode)
#endif
    {
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _environment = environment;
        _commandLineOptionsService = commandLineOptionsService;
        _configuration = configuration;
        _clock = clock;
        _artifactsByExtension = artifactsByExtension;
        _testFrameworkAdapter = testFrameworkAdapter;
        _testStartTime = testStartTime;
#if NETCOREAPP
        _cancellationToken = cancellationToken;
#endif
        _exitCode = exitCode;
        _fileSystem = fileSystem;
    }

    public async Task<(string FileName, string? Warning)> GenerateReportAsync(TestNodeUpdateMessage[] testNodeUpdateMessages, string testHostCrashInfo = "", bool isTestHostCrashed = false)
        => await RetryWhenIOExceptionAsync(async () =>
        {
            string testAppModule = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();

            // create the xml doc
            var document = new XDocument(new XDeclaration("1.0", "UTF-8", null));
            var testRun = new XElement(NamespaceUri + "TestRun");
            if (!Guid.TryParse(_environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TRX_TESTRUN_ID), out Guid testRunId))
            {
                testRunId = Guid.NewGuid();
            }

            // TODO: VSTest implementation seems to also add "runUser" attribute.
            // Revise that.
            testRun.SetAttributeValue("id", testRunId);
            string testRunName = $"{_environment.GetEnvironmentVariable("UserName")}@{_environment.MachineName} {FormatDateTimeForRunName(_clock.UtcNow)}";
            testRun.SetAttributeValue("name", testRunName);

            AddTimes(testRun);

            // If the user added the trxFileName the runDeploymentRoot would stay the same, We think it's a bug but I found that same behavior on vstest
            string runDeploymentRoot = AddTestSettings(testRun, testRunName);
            bool isFileNameExplicitlyProvided;
            string trxFileName;
            if (_commandLineOptionsService.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out string[]? fileName))
            {
                trxFileName = ReplaceInvalidFileNameChars(fileName[0]);
                isFileNameExplicitlyProvided = true;
            }
            else
            {
                trxFileName = $"{runDeploymentRoot}.trx";
                isFileNameExplicitlyProvided = false;
            }

            var testDefinitions = new XElement("TestDefinitions");
            var testEntries = new XElement("TestEntries");
            SummaryCounts summaryCounts = AddResults(testNodeUpdateMessages, testAppModule, testRun, runDeploymentRoot, testDefinitions, testEntries);
            testRun.Add(testDefinitions);
            testRun.Add(testEntries);
            AddTestLists(testRun);

            bool hasFailedTests = summaryCounts.Failed > 0 || summaryCounts.Timedout > 0;
            string trxOutcome = isTestHostCrashed || _exitCode != (int)ExitCode.Success || hasFailedTests ? "Failed" : "Completed";

            AddResultSummary(testRun, trxOutcome, runDeploymentRoot, testHostCrashInfo, _exitCode, summaryCounts, isTestHostCrashed);

            // will need catch Unauthorized access
            document.Add(testRun);

            foreach (XElement node in document.Root!.Descendants().Where(n => n.Name.NamespaceName == string.Empty))
            {
                // Remove the xmlns='' attribute. Note the use of
                // Attributes rather than Attribute, in case the
                // attribute doesn't exist (which it might not if we'd
                // created the document "manually" instead of loading
                // it from a file.)
                node.Attributes("xmlns").Remove();

                // Inherit the parent namespace instead
                node.Name = node.Parent!.Name.Namespace + node.Name.LocalName;
            }

            string outputDirectory = _configuration.GetTestResultDirectory(); // add var for this
            string finalFileName = Path.Combine(outputDirectory, trxFileName);

            bool isFileNameExplicitlyProvidedAndFileExists = isFileNameExplicitlyProvided && _fileSystem.ExistFile(finalFileName);

            // Note that we need to dispose the IFileStream, not the inner stream.
            // IFileStream implementations will be responsible to dispose their inner stream.
            using IFileStream stream = _fileSystem.NewFileStream(finalFileName, isFileNameExplicitlyProvided ? FileMode.Create : FileMode.CreateNew);
#if NETCOREAPP
            await document.SaveAsync(stream.Stream, SaveOptions.None, _cancellationToken).ConfigureAwait(false);
#else
            document.Save(stream.Stream, SaveOptions.None);
#endif
            return isFileNameExplicitlyProvidedAndFileExists
                ? (finalFileName, string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxFileExistsAndWillBeOverwritten, finalFileName))
                : (finalFileName, null);
        }).ConfigureAwait(false);

    private async Task<(string FileName, string? Warning)> RetryWhenIOExceptionAsync(Func<Task<(string FileName, string? Warning)>> func)
    {
        DateTimeOffset firstTryTime = _clock.UtcNow;
        bool throwIOException = false;
        while (true)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // In case of file with the same name we retry with a new name.
                if (throwIOException)
                {
                    throw;
                }
            }

            // We try for 5 seconds to create a file with a unique name.
            if (_clock.UtcNow - firstTryTime > TimeSpan.FromSeconds(5))
            {
                throwIOException = true;
            }
        }
    }

    public async Task AddArtifactsAsync(FileInfo trxFile, Dictionary<IExtension, List<SessionFileArtifact>> artifacts)
    {
        var document = XDocument.Load(trxFile.FullName);
        XElement testRun = document.Element(NamespaceUri + "TestRun")
            ?? throw new InvalidOperationException("TestRun element not found");
        XElement deployment = testRun.Element(NamespaceUri + "TestSettings")?.Element(NamespaceUri + "Deployment")
            ?? throw new InvalidOperationException("Deployment element not found");
        string runDeploymentRoot = deployment.Attribute("runDeploymentRoot")?.Value
            ?? throw new InvalidOperationException("Unexpected null 'runDeploymentRoot'");
        XElement resultSummary = testRun.Element(NamespaceUri + "ResultSummary")
            ?? throw new InvalidOperationException("ResultSummary element not found");
        XElement? collectorDataEntries = resultSummary.Element(NamespaceUri + "CollectorDataEntries");
        if (collectorDataEntries is null)
        {
            collectorDataEntries = new XElement(NamespaceUri + "CollectorDataEntries");
            resultSummary.Add(collectorDataEntries);
        }

        AddArtifactsToCollection(artifacts, collectorDataEntries, runDeploymentRoot);

        using FileStream fs = File.OpenWrite(trxFile.FullName);
#if NETCOREAPP
        await document.SaveAsync(fs, SaveOptions.None, _cancellationToken).ConfigureAwait(false);
#else
        document.Save(fs, SaveOptions.None);
#endif
    }

    private void AddArtifactsToCollection(Dictionary<IExtension, List<SessionFileArtifact>> artifacts, XElement collectorDataEntries, string runDeploymentRoot)
    {
        foreach (KeyValuePair<IExtension, List<SessionFileArtifact>> extensionArtifacts in artifacts)
        {
            var collector = new XElement(
                NamespaceUri + "Collector",
                new XAttribute("agentName", _environment.MachineName),
                new XAttribute("uri", $"datacollector://{extensionArtifacts.Key.Uid}/{extensionArtifacts.Key.Version}"),
                new XAttribute("collectorDisplayName", extensionArtifacts.Key.DisplayName));
            collectorDataEntries.Add(collector);

            var uriAttachments = new XElement(NamespaceUri + "UriAttachments");
            collector.Add(uriAttachments);

            foreach (SessionFileArtifact artifact in extensionArtifacts.Value)
            {
                string href = CopyArtifactIntoTrxDirectoryAndReturnHrefValue(artifact.FileInfo, runDeploymentRoot);
                uriAttachments.Add(new XElement(NamespaceUri + "UriAttachment", new XElement(NamespaceUri + "A", new XAttribute("href", href))));
            }
        }
    }

    private void AddResultSummary(XElement testRun, string resultSummaryOutcome, string runDeploymentRoot, string testHostCrashInfo, int exitCode, SummaryCounts summaryCounts, bool isTestHostCrashed = false)
    {
        // TODO: VSTest adds Output/StdOut element to ResultSummary which we don't add.
        // VSTest adds mainly two things in that element:
        // 1. Skipped test messages (see AddRunLevelInformationalMessage call in HandleSkippedTest in VSTest's TrxLogger implementation)
        // 2. Messages published with TestMessageLevel.Informational.
        var resultSummary = new XElement(
            NamespaceUri + "ResultSummary",
            new XAttribute("outcome", resultSummaryOutcome));
        testRun.Add(resultSummary);

        // NOTE: Looking at VSTest implementation:
        // 1. timeout is always set to 0 (it seems ObjectModel doesn't have the concept of timeout at all)
        // 2. Skipped tests are not counted in VSTest implementation.
        //    An informative message is added to indicate that test was skipped.
        // While what we have is reasonable, tooling implemented around might have been relying on VSTest implementation details.
        var counters = new XElement(
            NamespaceUri + "Counters",
            new XAttribute("total", summaryCounts.Passed + summaryCounts.Failed + summaryCounts.Skipped + summaryCounts.Timedout),
            new XAttribute("executed", summaryCounts.Passed + summaryCounts.Failed),
            new XAttribute("passed", summaryCounts.Passed),
            new XAttribute("failed", summaryCounts.Failed),
            new XAttribute("error", 0),
            new XAttribute("timeout", summaryCounts.Timedout),
            new XAttribute("aborted", 0),
            new XAttribute("inconclusive", 0),
            new XAttribute("passedButRunAborted", 0),
            new XAttribute("notRunnable", 0),
            new XAttribute("notExecuted", summaryCounts.Skipped),
            new XAttribute("disconnected", 0),
            new XAttribute("warning", 0),
            new XAttribute("completed", 0),
            new XAttribute("inProgress", 0),
            new XAttribute("pending", 0));
        resultSummary.Add(counters);

        // TODO: VSTest adds two additional things to RunInfos
        // 1. Messages published with TestMessageLevel.Warning or TestMessageLevel.Error
        // 2. Errors when constructing result files.
        // In addition, in these cases, it turns TRX outcome to error.
        if (isTestHostCrashed)
        {
            var runInfos = new XElement(NamespaceUri + "RunInfos");
            resultSummary.Add(runInfos);
            var runInfo = new XElement(
                NamespaceUri + "RunInfo",
                new XAttribute("computerName", _environment.MachineName),
                new XAttribute("outcome", "Error"),
                new XAttribute("timestamp", _clock.UtcNow.DateTime));
            var text = new XElement(NamespaceUri + "Text", testHostCrashInfo);
            runInfo.Add(text);
            runInfos.Add(runInfo);
        }
        else if (exitCode != (int)ExitCode.Success)
        {
            var runInfos = new XElement(NamespaceUri + "RunInfos");
            resultSummary.Add(runInfos);
            var runInfo = new XElement(
                NamespaceUri + "RunInfo",
                new XAttribute("computerName", _environment.MachineName),
                new XAttribute("outcome", "Error"),
                new XAttribute("timestamp", _clock.UtcNow.DateTime));
            var text = new XElement(NamespaceUri + "Text", $"Exit code indicates failure: '{exitCode}'. Please refer to https://aka.ms/testingplatform/exitcodes for more information.");
            runInfo.Add(text);
            runInfos.Add(runInfo);
        }

        if (_artifactsByExtension.Count == 0)
        {
            return;
        }

        // TODO: VSTest seems to also add ResultFiles element, and not only CollectorDataEntries.
        // TODO: Revise VSTest implementation for Converter.ToCollectionEntries and Converter.ToResultFiles
        var collectorDataEntries = new XElement(NamespaceUri + "CollectorDataEntries");
        resultSummary.Add(collectorDataEntries);

        AddArtifactsToCollection(_artifactsByExtension, collectorDataEntries, runDeploymentRoot);
    }

    private string CopyArtifactIntoTrxDirectoryAndReturnHrefValue(FileInfo artifact, string runDeploymentRoot)
    {
        string artifactDirectory = CreateOrGetTrxArtifactDirectory(runDeploymentRoot);
        string fileName = artifact.Name;

        string destination = Path.Combine(artifactDirectory, fileName);
        int nameCounter = 0;

        // If the file already exists, append a number to the end of the file name
        while (true)
        {
            if (File.Exists(destination))
            {
                nameCounter++;
                destination = Path.Combine(artifactDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_{nameCounter}{Path.GetExtension(fileName)}");
                continue;
            }

            break;
        }

        _fileSystem.CopyFile(artifact.FullName, new FileInfo(destination).FullName);

        return Path.Combine(_environment.MachineName, Path.GetFileName(destination));
    }

    private string CreateOrGetTrxArtifactDirectory(string runDeploymentRoot)
    {
        string directoryName = Path.Combine(_configuration.GetTestResultDirectory(), runDeploymentRoot, "In", _environment.MachineName);
        if (!Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        return directoryName;
    }

    private static void AddTestLists(XElement testRun)
    {
        var testLists = new XElement(
            "TestLists",
            new XElement(
                "TestList",
                // NOTE: VSTest localizes this string.
                new XAttribute("name", "Results Not in a List"),
                new XAttribute("id", UncategorizedTestListId)),
            new XElement(
                "TestList",
                new XAttribute("name", "All Loaded Results"),
                // parent of all categories (fake, not real category).
                new XAttribute("id", new Guid("19431567-8539-422a-85D7-44EE4E166BDA"))));

        testRun.Add(testLists);
    }

    private SummaryCounts AddResults(TestNodeUpdateMessage[] testNodeUpdateMessages, string testAppModule, XElement testRun, string runDeploymentRoot, XElement testDefinitions, XElement testEntries)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int timedout = 0;
        var results = new XElement("Results");

        // Duplicate test ids are not allowed inside the TestDefinitions element.
        // We create a dictionary to map test id to test definition name.
        // It's not expected to get the same test id twice but with different test definition name.
        // However, due to backcompat concerns, we will disallow this only for frameworks that start using TrxTestDefinitionName property.
        var uniqueTestDefinitionTestIds = new Dictionary<string, (string TestDefinitionName, bool IsExplicitlyProvided)>();

        foreach (TestNodeUpdateMessage nodeMessage in testNodeUpdateMessages)
        {
            TestNode testNode = nodeMessage.TestNode;

            // If already a guid (it's the case for at least MSTest), use that guid directly.
            // Otherwise, convert the string to a guid.
            if (!Guid.TryParse(testNode.Uid.Value, out Guid guid))
            {
                guid = GuidFromString(testNode.Uid.Value);
            }

            // NOTE: In VSTest, MSTestDiscoverer.TmiTestId property is preferred if present.
            string id = guid.ToString();
            string testResultDisplayName = RemoveInvalidXmlChar(testNode.DisplayName)!;
            (string testDefinitionName, bool isExplicitlyProvided) = testNode.Properties.SingleOrDefault<TrxTestDefinitionName>() is { } trxTestDefinitionName
                ? (RemoveInvalidXmlChar(trxTestDefinitionName.TestDefinitionName), true)
                : (testResultDisplayName, false);

            string executionId = Guid.NewGuid().ToString();

            // Results
            var unitTestResult = new XElement(
                "UnitTestResult",
                new XAttribute("executionId", executionId),
                new XAttribute("testId", id),
                new XAttribute("testName", testResultDisplayName),
                new XAttribute("computerName", _environment.MachineName));

            TimingProperty? timing = testNode.Properties.SingleOrDefault<TimingProperty>();
            string testDuration = timing?.GlobalTiming.Duration is { } duration
                ? duration.ToString("hh\\:mm\\:ss\\.fffffff", CultureInfo.InvariantCulture)
                : "00:00:00";
            unitTestResult.SetAttributeValue("duration", testDuration);

            unitTestResult.SetAttributeValue(
                "startTime",
                timing?.GlobalTiming.StartTime.ToUniversalTime().ToString("O") ?? _clock.UtcNow.ToString("O"));
            unitTestResult.SetAttributeValue(
                "endTime",
                timing?.GlobalTiming.EndTime.ToUniversalTime().ToString("O") ?? _clock.UtcNow.ToString("O"));

            // In VSTest, other test types originate from adding TestProperty with
            // Id TestType (see Constants.TestTypePropertyIdentifier).
            // The property is only considered if it has value ec4800e8-40e5-4ab3-8510-b8bf29b1904d (OrderedTestType)
            // In the context of MTP, we don't care.
            unitTestResult.SetAttributeValue("testType", UnitTestTypeGuid);

            string currentTestOutcome = "Passed";

            // In TrxReportGenerator.ConsumeAsync, we already filtered to only the nodes that contain TestNodeStateProperty.
            // We also filtered out discovered and in-progress states.
            // So the call to Single here should never fail, and should never be discovered or in-progress.
            TestNodeStateProperty testState = testNode.Properties.Single<TestNodeStateProperty>();
            if (testState is DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
            {
                throw ApplicationStateGuard.Unreachable();
            }

            if (testState is SkippedTestNodeStateProperty)
            {
                currentTestOutcome = "NotExecuted";
                skipped++;
            }
            else if (testState is PassedTestNodeStateProperty)
            {
                passed++;
            }
            else if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, testState.GetType()) >= 0)
            {
                currentTestOutcome = "Failed";

                if (testState is TimeoutTestNodeStateProperty)
                {
                    timedout++;
                }
                else
                {
                    failed++;
                }
            }
            else
            {
                // Above conditions should have handled all state properties.
                throw ApplicationStateGuard.Unreachable();
            }

            unitTestResult.SetAttributeValue("outcome", currentTestOutcome);

            unitTestResult.SetAttributeValue("testListId", UncategorizedTestListId);

            // It has the same value as executionId
            unitTestResult.SetAttributeValue("relativeResultsDirectory", executionId);

            // Below we're escaping most "dynamic body" using .Replace("\0", ""), because this is an invalid xml character.
            // There are other invalid xml characters, but they're transformed inside the writer in a correct way so we try to
            // rely on the built-in escaping/conversion.
            // i.e. https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlEncodedRawTextWriter.cs#L890
            var output = new XElement("Output");

            TrxMessagesProperty? trxMessages = testNode.Properties.SingleOrDefault<TrxMessagesProperty>();
            IEnumerable<string?>? nonErrorMessages = trxMessages?.Messages.Where(x => x is not StandardErrorTrxMessage and not DebugOrTraceTrxMessage).Select(x => x.Message);
            if (nonErrorMessages?.Any() == true)
            {
                output.Add(new XElement("StdOut", RemoveInvalidXmlChar(string.Join(Environment.NewLine, nonErrorMessages))));
            }

            IEnumerable<string?>? errorMessages = trxMessages?.Messages.Where(x => x is StandardErrorTrxMessage).Select(x => x.Message);
            if (errorMessages?.Any() == true)
            {
                output.Add(new XElement("StdErr", RemoveInvalidXmlChar(string.Join(Environment.NewLine, errorMessages))));
            }

            IEnumerable<string?>? debugOrTraceMessages = trxMessages?.Messages.Where(x => x is DebugOrTraceTrxMessage).Select(x => x.Message);
            if (debugOrTraceMessages?.Any() == true)
            {
                output.Add(new XElement("DebugTrace", RemoveInvalidXmlChar(string.Join(Environment.NewLine, debugOrTraceMessages))));
            }

            TrxExceptionProperty? trxException = testNode.Properties.SingleOrDefault<TrxExceptionProperty>();
            if (trxException?.Message is not null || trxException?.StackTrace is not null)
            {
                XElement errorInfoElement = new("ErrorInfo");

                if (trxException.Message is not null)
                {
                    errorInfoElement.Add(new XElement("Message", RemoveInvalidXmlChar(trxException.Message)));
                }

                if (trxException.StackTrace is not null)
                {
                    errorInfoElement.Add(new XElement("StackTrace", RemoveInvalidXmlChar(trxException.StackTrace)));
                }

                output.Add(errorInfoElement);
            }

            if (output.HasElements)
            {
                unitTestResult.Add(output);
            }

            // TODO: VSTest used to store the relative paths in a sorted list (ignoring case).
            // TODO: VSTest is able to classify per-test attachments into two categories:
            // 1. ResultFiles
            // 2. CollectorDataEntries
            // So far, we only have "ResultFiles".
            XElement? resultFiles = null;
            foreach (FileArtifactProperty testFileArtifact in testNode.Properties.OfType<FileArtifactProperty>())
            {
                resultFiles ??= new XElement("ResultFiles");

                string href = CopyArtifactIntoTrxDirectoryAndReturnHrefValue(testFileArtifact.FileInfo, runDeploymentRoot);
                resultFiles.Add(new XElement(
                    "ResultFile",
                    new XAttribute("path", href)));
            }

            if (resultFiles is not null)
            {
                unitTestResult.Add(resultFiles);
            }

            results.Add(unitTestResult);

            // TestDefinitions
            // Add the test method to the test definitions if it's not already there
            if (uniqueTestDefinitionTestIds.TryGetValue(id, out (string ExistingTestDefinitionName, bool ExistingIsExplicitlyProvided) existing))
            {
                // Value already exists. We only do a validation.
                // Owner, Description, Priority, and TestCategory are also part of the test definition.
                // Unfortunately, MSTest allows TestCategories via TestDataRow, which is one case where
                // we might receive the same test id and same test definition name, but different categories.
                // It's probably best if TRX is able to "merge" categories in this case (which we don't do yet).
                // For Owner, Description, Priority, this needs investigation whether or not it's expected to be different,
                // and what should we do in this case.
                if ((isExplicitlyProvided || existing.ExistingIsExplicitlyProvided) &&
                    existing.ExistingTestDefinitionName != testDefinitionName)
                {
                    throw new InvalidOperationException($"Received two different test definition names ('{existing.ExistingTestDefinitionName}' and '{testDefinitionName}') for the same test id '{id}'.");
                }

                if (!existing.ExistingIsExplicitlyProvided && isExplicitlyProvided)
                {
                    // We got a first result that didn't have explicit test definition name, but a second result that has an explicit test definition name.
                    uniqueTestDefinitionTestIds[id] = (testDefinitionName, true);
                }
            }
            else
            {
                uniqueTestDefinitionTestIds.Add(id, (testDefinitionName, isExplicitlyProvided));
                XElement unitTest = CreateUnitTestElementForTestDefinition(testDefinitionName, testAppModule, id, testNode, executionId);

                var testMethod = new XElement(
                    "TestMethod",
                    new XAttribute("codeBase", testAppModule),
                    new XAttribute("adapterTypeName", $"executor://{_testFrameworkAdapter.Uid}/{_testFrameworkAdapter.Version}"));

                // NOTE: className is required by TRX XSD.
                (string className, string? testMethodName) = GetClassAndMethodName(testNode);
                testMethod.SetAttributeValue("className", className);

                // NOTE: Historically, MTP used to always use testResultDisplayName here.
                // While VSTest never uses testResultDisplayName.
                // The use of testResultDisplayName here is very wrong.
                // We keep it as a fallback if we cannot determine the testMethodName (when TestMethodIdentifierProperty isn't present).
                // This will most likely be hit for NUnit.
                // However, this is very wrong and we probably should fail if TestMethodIdentifierProperty isn't present.
                testMethod.SetAttributeValue("name", testMethodName ?? testResultDisplayName);

                unitTest.Add(testMethod);

                testDefinitions.Add(unitTest);
            }

            // testEntry
            // NOTE: VSTest implementation ensures that we don't duplicate TestEntry elements with the same executionId.
            // However, our implementation always gets a fresh Guid so we don't need that special handling.
            // If we added the concept of "parent execution id" to MTP TRX and allow a way to
            // specify a parent-child relationship (e.g, for parameterized tests), we will need to
            // revise this.
            // The way VSTest does it, it allows test frameworks to set executionId and parentExecutionId on test results.
            var testEntry = new XElement(
                "TestEntry",
                new XAttribute("testId", id),
                new XAttribute("executionId", executionId),
                new XAttribute("testListId", UncategorizedTestListId));
            testEntries.Add(testEntry);
        }

        testRun.Add(results);

        return new SummaryCounts(passed, failed, skipped, timedout);
    }

    private (string ClassName, string? TestMethodName) GetClassAndMethodName(TestNode testNode)
    {
        TestMethodIdentifierProperty? testMethodIdentifierProperty = testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>();

        if (testNode.Properties.SingleOrDefault<TrxFullyQualifiedTypeNameProperty>()?.FullyQualifiedTypeName is { } className)
        {
            return (className, testMethodIdentifierProperty?.MethodName);
        }

        _ = testMethodIdentifierProperty ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportFrameworkDoesNotSupportTrxReportCapability, _testFrameworkAdapter.DisplayName, _testFrameworkAdapter.Uid));

        string classNameFromIdentifierProperty = RoslynString.IsNullOrEmpty(testMethodIdentifierProperty.Namespace)
            ? testMethodIdentifierProperty.TypeName
            : $"{testMethodIdentifierProperty.Namespace}.{testMethodIdentifierProperty.TypeName}";

        // TODO: Are we expected to append backtick and arity here for generic methods?
        return (classNameFromIdentifierProperty, testMethodIdentifierProperty.MethodName);
    }

    private static XElement CreateUnitTestElementForTestDefinition(string testDefinitionName, string testAppModule, string id, TestNode testNode, string executionId)
    {
        var unitTest = new XElement(
            "UnitTest",
            new XAttribute("name", testDefinitionName),
            new XAttribute("storage", testAppModule.ToLowerInvariant()),
            new XAttribute("id", id));

        TrxCategoriesProperty? trxCategories = testNode.Properties.SingleOrDefault<TrxCategoriesProperty>();
        if (trxCategories?.Categories.Length > 0)
        {
            unitTest.Add(new XElement("TestCategory", trxCategories.Categories.Select(c => new XElement("TestCategoryItem", new XAttribute("TestCategory", c)))));
        }

        unitTest.Add(new XElement("Execution", new XAttribute("id", executionId)));

        XElement? properties = null;
        XElement? owners = null;
        XElement? description = null;
        foreach (TestMetadataProperty property in testNode.Properties.OfType<TestMetadataProperty>())
        {
            switch (property.Key)
            {
                case "Owner":
                    owners ??= new XElement("Owners", new XElement("Owner", new XAttribute("name", property.Value)));
                    break;

                case "Priority":
                    // 2147483647 (int.MaxValue) is already the default priority.
                    if (int.TryParse(property.Value, out int priorityValue) && priorityValue != int.MaxValue)
                    {
                        unitTest.SetAttributeValue("priority", property.Value);
                    }

                    break;

                case "Description":
                    description ??= new XElement("Description", property.Value);
                    break;

                default:
                    // NOTE: VSTest doesn't produce Properties as of writing this.
                    // It was historically fixed, but the fix wasn't correct and the fix was reverted and never revisited to be properly fixed.
                    // Revert PR: https://github.com/microsoft/vstest/pull/15080
                    // The original implementation (buggy) was setting "Key" and "Value" as attributes on "Property" element.
                    // However, Visual Studio will validate the TRX file against vstst.xsd file in
                    //  C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Xml\Schemas\vstst.xsd
                    // In xsd, "Properties" element is defined as:
                    // <xs:element name="Properties" minOccurs="0">
                    //   <xs:complexType>
                    //     <xs:sequence>
                    //       <xs:element name="Property" minOccurs="0" maxOccurs="unbounded">
                    //         <xs:complexType>
                    //           <xs:sequence>
                    //             <xs:element name="Key" />
                    //             <xs:element name="Value" />
                    //           </xs:sequence>
                    //         </xs:complexType>
                    //       </xs:element>
                    //     </xs:sequence>
                    //   </xs:complexType>
                    // </xs:element>
                    // So, Key and Value are **elements**, not attributes.
                    // In MTP, we do the right thing and follow the XSD definition.
                    properties ??= new XElement("Properties");
                    properties.Add(new XElement(
                        "Property",
                        new XElement("Key", property.Key), new XElement("Value", property.Value)));
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

        // TODO: We are not adding Workitems, but VSTest does.
        return unitTest;
    }

    private static string AddTestSettings(XElement testRun, string testRunName)
    {
        var testSettings = new XElement(
            "TestSettings",
            new XAttribute("name", "default"),
            new XAttribute("id", Guid.NewGuid()));
        string runDeploymentRoot = ReplaceInvalidFileNameChars(testRunName);
        testSettings.Add(new XElement("Deployment", new XAttribute("runDeploymentRoot", runDeploymentRoot)));
        testRun.Add(testSettings);
        return runDeploymentRoot;
    }

    private void AddTimes(XElement testRun)
    {
        var times = new XElement(
            "Times",
            new XAttribute("creation", _testStartTime),
            new XAttribute("queuing", _testStartTime),
            new XAttribute("start", _testStartTime),
            new XAttribute("finish", _clock.UtcNow));
        testRun.Add(times);
    }

    private static string FormatDateTimeForRunName(DateTimeOffset date) =>

        // We use custom format string to make sure that runs are sorted in the same way on all intl machines.
        // This is both for directory names and for Data Warehouse.
        date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", DateTimeFormatInfo.InvariantInfo);

    private static string ReplaceInvalidFileNameChars(string fileName)
    {
        // Replace bad chars by this.
        char replacementChar = '_';
        char[] result = new char[fileName.Length];

        // Replace each invalid char with replacement char.
        for (int i = 0; i < fileName.Length; ++i)
        {
            result[i] = InvalidFileNameChars.Contains(fileName[i]) ? replacementChar : fileName[i];
        }

        // We trim spaces in the end because CreateFile/Dir trim those.
        string replaced = new string(result).TrimEnd();
        ArgumentGuard.Ensure(replaced.Length > 0, nameof(fileName), $"File name {fileName} is empty after removing invalid characters.");

        if (IsReservedFileName(replaced))
        {
            replaced = replacementChar + replaced;  // Cannot add to the end because it can have extensions.
        }

        return replaced;
    }

    private static bool IsReservedFileName(string fileName) =>

        // CreateFile:
        // The following reserved device names cannot be used as the name of a file:
        // CON, PRN, AUX, NUL, COM1, COM2, COM3, COM4, COM5, COM6, COM7, COM8, COM9,
        // LPT1, LPT2, LPT3, LPT4, LPT5, LPT6, LPT7, LPT8, and LPT9.
        // Also avoid these names followed by an extension, for example, NUL.tx7.
        // Windows NT: CLOCK$ is also a reserved device name.
        ReservedFileNamesRegex.IsMatch(fileName);

    private static Guid GuidFromString(string data)
    {
        byte[] hash = TestFx.Hashing.XxHash128.Hash(Encoding.Unicode.GetBytes(data));
        return new Guid(hash);
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"(?i:^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]|CLOCK\$)(\..*)?)$", RegexOptions.None, "en-150")]
    private static partial Regex BuildReservedFileNameRegex();
#else
    private static Regex BuildReservedFileNameRegex() => new(@"(?i:^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]|CLOCK\$)(\..*)?)$");
#endif

    // From xml spec (http://www.w3.org/TR/xml/#charsets) valid chars:
    // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
    // we are handling only #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
    // because C# support unicode character in range \u0000 to \uFFFF
#if NET7_0_OR_GREATER
    [GeneratedRegex(@"[^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD]")]
    private static partial Regex BuildInvalidXmlCharReplace();
#else
    private static Regex BuildInvalidXmlCharReplace() => new(@"[^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD]");
#endif

    private static string RemoveInvalidXmlChar(string str) => InvalidXmlCharReplace.Replace(str, InvalidXmlEvaluator);

    private static string ReplaceInvalidCharacterWithUniCodeEscapeSequence(Match match)
    {
        char x = match.Value[0];
        return $@"\u{(ushort)x:x4}";
    }

    private readonly record struct SummaryCounts(int Passed, int Failed, int Skipped, int Timedout);
}
