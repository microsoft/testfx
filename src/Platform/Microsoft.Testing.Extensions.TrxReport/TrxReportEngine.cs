// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Buffers;
#endif
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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

    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IEnvironment _environment;
    private readonly ICommandLineOptions _commandLineOptionsService;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly TestNodeUpdateMessage[] _testNodeUpdatedMessages;
    private readonly int _failedTestsCount;
    private readonly int _passedTestsCount;
    private readonly int _notExecutedTestsCount;
    private readonly int _timeoutTestsCount;
    private readonly Dictionary<IExtension, List<SessionFileArtifact>> _artifactsByExtension;
    private readonly Dictionary<TestNodeUid, List<SessionFileArtifact>> _artifactsByTestNode;
    private readonly bool? _adapterSupportTrxCapability;
    private readonly ITestFramework _testFrameworkAdapter;
    private readonly DateTimeOffset _testStartTime;
    private readonly CancellationToken _cancellationToken;
    private readonly int _exitCode;
    private readonly XNamespace _namespaceUri = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");
    private readonly IFileSystem _fileSystem;
    private readonly bool _isCopyingFileAllowed;

    public TrxReportEngine(ITestApplicationModuleInfo testApplicationModuleInfo, IEnvironment environment, ICommandLineOptions commandLineOptionsService, IConfiguration configuration, IClock clock, TestNodeUpdateMessage[] testNodeUpdatedMessages, int failedTestsCount, int passedTestsCount, int notExecutedTestsCount, int timeoutTestsCount, Dictionary<IExtension, List<SessionFileArtifact>> artifactsByExtension, Dictionary<TestNodeUid, List<SessionFileArtifact>> artifactsByTestNode, bool? adapterSupportTrxCapability, ITestFramework testFrameworkAdapter, DateTimeOffset testStartTime, int exitCode, CancellationToken cancellationToken)
    : this(
        new SystemFileSystem(),
        testApplicationModuleInfo,
        environment,
        commandLineOptionsService,
        configuration,
        clock,
        testNodeUpdatedMessages,
        failedTestsCount,
        passedTestsCount,
        notExecutedTestsCount,
        timeoutTestsCount,
        artifactsByExtension,
        artifactsByTestNode,
        adapterSupportTrxCapability,
        testFrameworkAdapter,
        testStartTime,
        exitCode,
        cancellationToken)
    {
    }

    public TrxReportEngine(IFileSystem fileSystem, ITestApplicationModuleInfo testApplicationModuleInfo, IEnvironment environment, ICommandLineOptions commandLineOptionsService, IConfiguration configuration, IClock clock, TestNodeUpdateMessage[] testNodeUpdatedMessages, int failedTestsCount, int passedTestsCount, int notExecutedTestsCount, int timeoutTestsCount, Dictionary<IExtension, List<SessionFileArtifact>> artifactsByExtension, Dictionary<TestNodeUid, List<SessionFileArtifact>> artifactsByTestNode, bool? adapterSupportTrxCapability, ITestFramework testFrameworkAdapter, DateTimeOffset testStartTime, int exitCode, CancellationToken cancellationToken, bool isCopyingFileAllowed = true)
    {
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _environment = environment;
        _commandLineOptionsService = commandLineOptionsService;
        _configuration = configuration;
        _clock = clock;
        _testNodeUpdatedMessages = testNodeUpdatedMessages;
        _failedTestsCount = failedTestsCount;
        _passedTestsCount = passedTestsCount;
        _notExecutedTestsCount = notExecutedTestsCount;
        _timeoutTestsCount = timeoutTestsCount;
        _artifactsByExtension = artifactsByExtension;
        _artifactsByTestNode = artifactsByTestNode;
        _adapterSupportTrxCapability = adapterSupportTrxCapability;
        _testFrameworkAdapter = testFrameworkAdapter;
        _testStartTime = testStartTime;
        _cancellationToken = cancellationToken;
        _exitCode = exitCode;
        _fileSystem = fileSystem;
        _isCopyingFileAllowed = isCopyingFileAllowed;
    }

    public async Task<string> GenerateReportAsync(string testHostCrashInfo = "", bool isTestHostCrashed = false, bool keepReportFileStreamOpen = false)
        => await RetryWhenIOExceptionAsync(async () =>
        {
            string testAppModule = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();

            // create the xml doc
            var document = new XDocument(new XDeclaration("1.0", "UTF-8", null));
            var testRun = new XElement(_namespaceUri + "TestRun");
            testRun.SetAttributeValue("id", Guid.NewGuid());
            string testRunName = $"{_environment.GetEnvironmentVariable("UserName")}@{_environment.MachineName} {FormatDateTimeForRunName(_clock.UtcNow)}";
            testRun.SetAttributeValue("name", testRunName);

            AddTimes(testRun);

            // If the user added the trxFileName the runDeploymentRoot would stay the same, We think it's a bug but I found that same behavior on vstest
            string runDeploymentRoot = AddTestSettings(testRun, testRunName);
            string trxFileName = _commandLineOptionsService.TryGetOptionArgumentList(TrxReportGeneratorCommandLine.TrxReportFileNameOptionName, out string[]? fileName)
                ? ReplaceInvalidFileNameChars(fileName[0])
                : $"{runDeploymentRoot}.trx";
            AddResults(testAppModule, testRun, out XElement testDefinitions, out XElement testEntries, out string uncategorizedTestId, out string resultSummaryOutcome);
            testRun.Add(testDefinitions);
            testRun.Add(testEntries);
            AddTestLists(testRun, uncategorizedTestId);

            // NotExecuted is the status for the skipped test.
            resultSummaryOutcome = isTestHostCrashed || _exitCode != ExitCodes.Success ? "Failed" : resultSummaryOutcome is "Passed" or "NotExecuted" ? "Completed" : resultSummaryOutcome;

            await AddResultSummaryAsync(testRun, resultSummaryOutcome, runDeploymentRoot, testHostCrashInfo, _exitCode, isTestHostCrashed);

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
            Stream stream = _fileSystem.NewFileStream(finalFileName, FileMode.CreateNew).Stream;
            try
            {
                await document.SaveAsync(stream, SaveOptions.None, _cancellationToken);
                return finalFileName;
            }
            finally
            {
                if (!keepReportFileStreamOpen)
                {
#if NET
                    await stream.DisposeAsync();
#else
                    stream.Dispose();
#endif
                }
            }
        });

    private async Task<string> RetryWhenIOExceptionAsync(Func<Task<string>> func)
    {
        DateTimeOffset firstTryTime = _clock.UtcNow;
        bool throwIOException = false;
        while (true)
        {
            try
            {
                return await func();
            }
            catch (IOException)
            {
                // In case of file with the same name we retry with a new name.
                if (throwIOException)
                {
                    throw;
                }
            }

            // We try for 30 seconds to create a file with a unique name.
            if (_clock.UtcNow - firstTryTime > TimeSpan.FromSeconds(30))
            {
                throwIOException = true;
            }
        }
    }

    public async Task AddArtifactsAsync(FileInfo trxFile, Dictionary<IExtension, List<SessionFileArtifact>> artifacts)
    {
        var document = XDocument.Load(trxFile.FullName);
        XElement testRun = document.Element(_namespaceUri + "TestRun")
            ?? throw new InvalidOperationException("TestRun element not found");
        XElement deployment = testRun.Element(_namespaceUri + "TestSettings")?.Element(_namespaceUri + "Deployment")
            ?? throw new InvalidOperationException("Deployment element not found");
        string runDeploymentRoot = deployment.Attribute("runDeploymentRoot")?.Value
            ?? throw new InvalidOperationException("Unexpected null 'runDeploymentRoot'");
        XElement resultSummary = testRun.Element(_namespaceUri + "ResultSummary")
            ?? throw new InvalidOperationException("ResultSummary element not found");
        XElement? collectorDataEntries = resultSummary.Element(_namespaceUri + "CollectorDataEntries");
        if (collectorDataEntries is null)
        {
            collectorDataEntries = new XElement(_namespaceUri + "CollectorDataEntries");
            resultSummary.Add(collectorDataEntries);
        }

        await AddArtifactsToCollectionAsync(artifacts, collectorDataEntries, runDeploymentRoot);

        using FileStream fs = File.OpenWrite(trxFile.FullName);
        await document.SaveAsync(fs, SaveOptions.None, _cancellationToken);
    }

    private async Task AddArtifactsToCollectionAsync(Dictionary<IExtension, List<SessionFileArtifact>> artifacts, XElement collectorDataEntries, string runDeploymentRoot)
    {
        foreach (KeyValuePair<IExtension, List<SessionFileArtifact>> extensionArtifacts in artifacts)
        {
            var collector = new XElement(
                _namespaceUri + "Collector",
                new XAttribute("agentName", _environment.MachineName),
                new XAttribute("uri", $"datacollector://{extensionArtifacts.Key.Uid}/{extensionArtifacts.Key.Version}"),
                new XAttribute("collectorDisplayName", extensionArtifacts.Key.DisplayName));
            collectorDataEntries.Add(collector);

            var uriAttachments = new XElement(_namespaceUri + "UriAttachments");
            collector.Add(uriAttachments);

            foreach (SessionFileArtifact artifact in extensionArtifacts.Value)
            {
                string href = await CopyArtifactIntoTrxDirectoryAndReturnHrefValueAsync(artifact.FileInfo, runDeploymentRoot);
                uriAttachments.Add(new XElement(_namespaceUri + "UriAttachment", new XElement(_namespaceUri + "A", new XAttribute("href", href))));
            }
        }
    }

    private async Task AddResultSummaryAsync(XElement testRun, string resultSummaryOutcome, string runDeploymentRoot, string testHostCrashInfo, int exitCode, bool isTestHostCrashed = false)
    {
        var resultSummary = new XElement(
            _namespaceUri + "ResultSummary",
            new XAttribute("outcome", resultSummaryOutcome));
        testRun.Add(resultSummary);

        var counters = new XElement(
            _namespaceUri + "Counters",
            new XAttribute("total", _testNodeUpdatedMessages.Length),
            new XAttribute("executed", _passedTestsCount + _failedTestsCount),
            new XAttribute("passed", _passedTestsCount),
            new XAttribute("failed", _failedTestsCount),
            new XAttribute("error", 0),
            new XAttribute("timeout", _timeoutTestsCount),
            new XAttribute("aborted", 0),
            new XAttribute("inconclusive", 0),
            new XAttribute("passedButRunAborted", 0),
            new XAttribute("notRunnable", 0),
            new XAttribute("notExecuted", _notExecutedTestsCount),
            new XAttribute("disconnected", 0),
            new XAttribute("warning", 0),
            new XAttribute("completed", 0),
            new XAttribute("inProgress", 0),
            new XAttribute("pending", 0));
        resultSummary.Add(counters);

        if (isTestHostCrashed)
        {
            var runInfos = new XElement(_namespaceUri + "RunInfos");
            resultSummary.Add(runInfos);
            var runInfo = new XElement(
                _namespaceUri + "RunInfo",
                new XAttribute("computerName", _environment.MachineName),
                new XAttribute("outcome", "Error"),
                new XAttribute("timestamp", _clock.UtcNow.DateTime));
            var text = new XElement(_namespaceUri + "Text", testHostCrashInfo);
            runInfo.Add(text);
            runInfos.Add(runInfo);
        }
        else if (exitCode != ExitCodes.Success)
        {
            var runInfos = new XElement(_namespaceUri + "RunInfos");
            resultSummary.Add(runInfos);
            var runInfo = new XElement(
                _namespaceUri + "RunInfo",
                new XAttribute("computerName", _environment.MachineName),
                new XAttribute("outcome", "Error"),
                new XAttribute("timestamp", _clock.UtcNow.DateTime));
            var text = new XElement(_namespaceUri + "Text", $"Exit code indicates failure: '{exitCode}'. Please refer to https://aka.ms/testingplatform/exitcodes for more information.");
            runInfo.Add(text);
            runInfos.Add(runInfo);
        }

        if (_artifactsByExtension.Count == 0)
        {
            return;
        }

        var collectorDataEntries = new XElement(_namespaceUri + "CollectorDataEntries");
        resultSummary.Add(collectorDataEntries);

        await AddArtifactsToCollectionAsync(_artifactsByExtension, collectorDataEntries, runDeploymentRoot);
    }

    private async Task<string> CopyArtifactIntoTrxDirectoryAndReturnHrefValueAsync(FileInfo artifact, string runDeploymentRoot)
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

        await CopyFileAsync(artifact, new FileInfo(destination));

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

    private async Task CopyFileAsync(FileInfo origin, FileInfo destination)
    {
        if (!_isCopyingFileAllowed)
        {
            return;
        }

        using FileStream fileStream = File.OpenRead(origin.FullName);
        using var destinationStream = new FileStream(destination.FullName, FileMode.Create);
        await fileStream.CopyToAsync(destinationStream);
    }

    private static void AddTestLists(XElement testRun, string uncategorizedTestId)
    {
        var testLists = new XElement(
            "TestLists",
            new XElement(
                "TestList",
                new XAttribute("name", "Results Not in a List"),
                new XAttribute("id", uncategorizedTestId)),
            new XElement(
                "TestList",
                new XAttribute("name", "All Loaded Results"),
                // parent of all categories (fake, not real category).
                new XAttribute("id", new Guid("19431567-8539-422a-85D7-44EE4E166BDA"))));

        testRun.Add(testLists);
    }

    private void AddResults(string testAppModule, XElement testRun, out XElement testDefinitions, out XElement testEntries, out string uncategorizedTestId, out string resultSummaryOutcome)
    {
        var results = new XElement("Results");

        // Duplicate test ids are not allowed inside the TestDefinitions element.
        testDefinitions = new XElement("TestDefinitions");
        var uniqueTestDefinitionTestIds = new HashSet<string>();

        testEntries = new XElement("TestEntries");
        uncategorizedTestId = "8C84FA94-04C1-424b-9868-57A2D4851A1D";
        resultSummaryOutcome = "Passed";
        foreach (TestNodeUpdateMessage nodeMessage in _testNodeUpdatedMessages)
        {
            TestNode testNode = nodeMessage.TestNode;

            string id = GuidFromString($"{testNode.Uid.Value} {testNode.DisplayName}").ToString();
            string displayName = RemoveInvalidXmlChar(testNode.DisplayName)!;
            string executionId = Guid.NewGuid().ToString();

            // Results
            var unitTestResult = new XElement(
                "UnitTestResult",
                new XAttribute("executionId", executionId),
                new XAttribute("testId", id),
                new XAttribute("testName", displayName),
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

            // TODO: Are there other types?
            unitTestResult.SetAttributeValue("testType", UnitTestTypeGuid);

            string outcome = "Passed";
            TestNodeStateProperty? testState = testNode.Properties.SingleOrDefault<TestNodeStateProperty>();
            if (testState is { } state
                && TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties.Contains(testState.GetType()))
            {
                outcome = resultSummaryOutcome = "Failed";
            }
            else if (testState is SkippedTestNodeStateProperty)
            {
                outcome = resultSummaryOutcome = "NotExecuted";
            }

            unitTestResult.SetAttributeValue("outcome", outcome);

            unitTestResult.SetAttributeValue("testListId", uncategorizedTestId);

            // It has the same value as executionId
            unitTestResult.SetAttributeValue("relativeResultsDirectory", executionId);

            // Below we're escaping most "dynamic body" using .Replace("\0", ""), because this is an invalid xml character.
            // There are other invalid xml characters, but they're transformed inside the writer in a correct way so we try to
            // rely on the built-in escaping/conversion.
            // i.e. https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlEncodedRawTextWriter.cs#L890
            var output = new XElement("Output");

            TrxMessagesProperty? trxMessages = testNode.Properties.SingleOrDefault<TrxMessagesProperty>();
            IEnumerable<string?>? nonErrorMessages = trxMessages?.Messages.Where(x => x is not StandardErrorTrxMessage).Select(x => x.Message);
            if (nonErrorMessages?.Any() == true)
            {
                output.Add(new XElement("StdOut", RemoveInvalidXmlChar(string.Join(Environment.NewLine, nonErrorMessages))));
            }

            IEnumerable<string?>? errorMessages = trxMessages?.Messages.Where(x => x is StandardErrorTrxMessage).Select(x => x.Message);
            if (errorMessages?.Any() == true)
            {
                output.Add(new XElement("StdErr", RemoveInvalidXmlChar(string.Join(Environment.NewLine, errorMessages))));
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

            // add collectorDataEntries details
            if (output.HasElements && outcome != "NotExecuted")
            {
                unitTestResult.Add(output);
            }

            if (_artifactsByTestNode.TryGetValue(testNode.Uid, out List<SessionFileArtifact>? fileArtifacts))
            {
                var resultFiles = new XElement("ResultFiles");

                foreach (SessionFileArtifact fileArtifact in fileArtifacts)
                {
                    resultFiles.Add(new XElement(
                        "ResultFile",
                        new XAttribute("path", fileArtifact.FileInfo.FullName)));
                }

                unitTestResult.Add(resultFiles);
            }

            results.Add(unitTestResult);

            // TestDefinitions
            var unitTest = new XElement(
                "UnitTest",
                new XAttribute("name", displayName),
                new XAttribute("storage", testAppModule.ToLowerInvariant()),
                new XAttribute("id", id));

            TrxCategoriesProperty? trxCategories = testNode.Properties.SingleOrDefault<TrxCategoriesProperty>();
            if (trxCategories?.Categories.Length > 0)
            {
                unitTest.Add(new XElement("TestCategory", trxCategories.Categories.Select(c => new XElement("TestCategoryItem", new XAttribute("TestCategory", c)))));
            }

            unitTest.Add(new XElement("Execution", new XAttribute("id", executionId)));

            var testMethod = new XElement(
                "TestMethod",
                new XAttribute("codeBase", testAppModule),
                new XAttribute("adapterTypeName", $"executor://{_testFrameworkAdapter.Uid}/{_testFrameworkAdapter.Version}"));

            if (_adapterSupportTrxCapability == true)
            {
                string? className = testNode.Properties.SingleOrDefault<TrxFullyQualifiedTypeNameProperty>()?.FullyQualifiedTypeName;
                if (className is not null)
                {
                    testMethod.SetAttributeValue("className", className);
                }
            }

            testMethod.SetAttributeValue("name", displayName);

            unitTest.Add(testMethod);

            // Add the test method to the test definitions if it's not already there
            if (!uniqueTestDefinitionTestIds.Contains(id))
            {
                testDefinitions.Add(unitTest);
                uniqueTestDefinitionTestIds.Add(id);
            }

            // testEntry
            var testEntry = new XElement(
                "TestEntry",
                new XAttribute("testId", id),
                new XAttribute("executionId", executionId),
                new XAttribute("testListId", uncategorizedTestId));
            testEntries.Add(testEntry);
        }

        testRun.Add(results);
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
        date.ToString("yyyy-MM-dd HH:mm:ss.fff", DateTimeFormatInfo.InvariantInfo);

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
        ReservedFileNamesRegex.Match(fileName).Success;

    private static Guid GuidFromString(string data)
    {
#if NETCOREAPP
        int byteCount = Encoding.Unicode.GetByteCount(data);
        Span<byte> hash = stackalloc byte[32];
        byte[] dataBytes = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            Encoding.Unicode.GetBytes(data, dataBytes);
            SHA256.HashData(dataBytes.AsSpan()[..byteCount], hash);
            return new Guid(hash[..16]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBytes);
        }
#else
        var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.Unicode.GetBytes(data));
        byte[] bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
#endif
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

    private static string? RemoveInvalidXmlChar(string? str) => str is null ? null : InvalidXmlCharReplace.Replace(str, InvalidXmlEvaluator);

    private static string ReplaceInvalidCharacterWithUniCodeEscapeSequence(Match match)
    {
        char x = match.Value[0];
        return $@"\u{(ushort)x:x4}";
    }
}
