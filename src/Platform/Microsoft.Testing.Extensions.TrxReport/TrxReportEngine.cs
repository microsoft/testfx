// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed partial class TrxReportEngine
{
    private const string UnitTestTypeGuid = "13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B";
    private const string UncategorizedTestListId = "8C84FA94-04C1-424b-9868-57A2D4851A1D";

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

    public async Task<(string FileName, string? Warning)> GenerateReportAsync(IReadOnlyList<TrxTestResult> testResults, string testHostCrashInfo = "", bool isTestHostCrashed = false)
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
                // The argument may be a bare file name, a relative path or an absolute path. Placeholders
                // are resolved first against the whole input. Only the leaf file name is sanitized for
                // invalid characters — the directory portion is treated as a literal path so that it can
                // contain path separators, drive letters or UNC prefixes. Invalid characters in the
                // directory portion (e.g. introduced by an unexpected placeholder value) are deferred to
                // the OS and will surface as an IOException at file creation time.
                string resolved = ResolveTrxFileNamePlaceholders(fileName[0]);
                string directoryPart = Path.GetDirectoryName(resolved) ?? string.Empty;
                string sanitizedFileName = ReplaceInvalidFileNameChars(Path.GetFileName(resolved));
                trxFileName = directoryPart.Length == 0
                    ? sanitizedFileName
                    : Path.Combine(directoryPart, sanitizedFileName);
                isFileNameExplicitlyProvided = true;
            }
            else
            {
                trxFileName = $"{runDeploymentRoot}.trx";
                isFileNameExplicitlyProvided = false;
            }

            var testDefinitions = new XElement("TestDefinitions");
            var testEntries = new XElement("TestEntries");
            var attachmentWarnings = new List<string>();
            SummaryCounts summaryCounts = AddResults(testResults, testAppModule, testRun, runDeploymentRoot, testDefinitions, testEntries, attachmentWarnings);
            testRun.Add(testDefinitions);
            testRun.Add(testEntries);
            AddTestLists(testRun);

            bool hasFailedTests = summaryCounts.Failed > 0 || summaryCounts.Timedout > 0;
            string trxOutcome = isTestHostCrashed || _exitCode != (int)ExitCode.Success || hasFailedTests ? "Failed" : "Completed";

            AddResultSummary(testRun, trxOutcome, runDeploymentRoot, testHostCrashInfo, _exitCode, summaryCounts, attachmentWarnings, isTestHostCrashed);

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

            string outputDirectory = _configuration.GetTestResultDirectory();

            // Path.Combine short-circuits when the second argument is rooted, so an absolute trxFileName
            // overrides the test results directory while a validated relative one (including one with
            // subdirectories, but not parent traversal) stays nested under it.
            string finalFileName = Path.Combine(outputDirectory, trxFileName);

            // Ensure intermediate directories exist when the user-provided file name introduced
            // sub-directories or pointed at an absolute path under a directory that doesn't exist yet.
            string? finalDirectory = Path.GetDirectoryName(finalFileName);
            if (!RoslynString.IsNullOrEmpty(finalDirectory))
            {
                _fileSystem.CreateDirectory(finalDirectory);
            }

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

    private string ResolveTrxFileNamePlaceholders(string template)
    {
        string processName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string processId = _environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements(processName, processId, _clock.UtcNow);
        return ArtifactNamingHelper.ResolveTemplate(template, replacements);
    }

    private readonly record struct SummaryCounts(int Passed, int Failed, int Skipped, int Timedout);
}
