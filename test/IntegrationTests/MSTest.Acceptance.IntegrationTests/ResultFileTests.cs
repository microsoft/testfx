// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

using MSTest.Acceptance.IntegrationTests.Messages.V100;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ResultFileTests : ServerModeTestsBase<ResultFileTests.TestAssetFixture>
{
    private const string SourceDirectoryVariable = "MSTEST_RESULT_FILE_SOURCE_DIRECTORY";
    private const string WaitForCancellationVariable = "MSTEST_RESULT_FILE_WAIT_FOR_CANCELLATION";
    private const string FileNameVariable = "MSTEST_RESULT_FILE_NAME";
    private const string DefaultFileName = "attachment.txt";
    private const int WindowsMaxPath = 260;
    private static readonly Dictionary<string, string> ExpectedAttachments = new(StringComparer.Ordinal)
    {
        ["FirstAttachment"] = "first attachment",
        ["SecondAttachment"] = "second attachment",
    };

    public TestContext TestContext { get; set; }

    public static IEnumerable<(string Tfm, bool RelativeResultsDirectory, bool EnableTrx, bool LongResultsDirectory)> DotnetTestCases
    {
        get
        {
            foreach (string tfm in TargetFrameworks.All)
            {
                foreach (bool relativeResultsDirectory in new[] { false, true })
                {
                    foreach (bool enableTrx in new[] { false, true })
                    {
                        yield return (tfm, relativeResultsDirectory, enableTrx, false);
                    }
                }

                if (TargetFrameworks.NetFramework.Contains(tfm))
                {
                    yield return (tfm, false, true, true);
                }
            }
        }
    }

    public static IEnumerable<(string Tfm, bool RelativeResultsDirectory)> JsonRpcTestCases
        => from tfm in TargetFrameworks.All
           from relativeResultsDirectory in new[] { false, true }
           select (tfm, relativeResultsDirectory);

    [TestMethod]
    [DynamicData(nameof(DotnetTestCases))]
    public async Task DotnetTest_PublishedResultFiles_SurviveProcessExit(string tfm, bool relativeResultsDirectory, bool enableTrx, bool longResultsDirectory)
    {
        string resultsName = longResultsDirectory
            ? $"lp-{Guid.NewGuid().ToString("N")[..12]}"
            : $"Test Results-{Guid.NewGuid():N}";
        string resultsRoot = Path.Combine(AssetFixture.TargetAssetPath, resultsName);
        string resultsDirectory = resultsRoot;
        if (longResultsDirectory)
        {
            // Leave room for .NET Framework deployment folders; cross MAX_PATH with the copied file name.
            resultsDirectory = resultsRoot.Length < 180
                ? Path.Combine(resultsRoot, new string('r', 180 - resultsRoot.Length - 1))
                : resultsRoot;
            Assert.IsLessThanOrEqualTo(180, resultsDirectory.Length);
        }

        string sourceDirectory = Path.Combine(AssetFixture.TargetAssetPath, $"s-{Guid.NewGuid():N}");
        string fileName = longResultsDirectory ? $"attachment-{new string('r', 45)}.txt" : DefaultFileName;
        if (longResultsDirectory)
        {
            Assert.IsLessThan(WindowsMaxPath, Path.Combine(sourceDirectory, "SecondAttachment", fileName).Length);
        }

        var host = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string module = TargetFrameworks.NetFramework.Contains(tfm)
            ? Path.GetFileName(host.FullName)
            : $"{TestAssetFixture.ProjectName}.dll";
        string resultsArgument = relativeResultsDirectory ? Path.GetRelativePath(AssetFixture.TargetAssetPath, resultsDirectory) : resultsDirectory;
        string trxArgument = enableTrx ? "--report-trx --report-trx-filename results.trx" : string.Empty;

        try
        {
            DotnetMuxerResult result = await DotnetCli.RunAsync(
                $"test --test-modules \"{module}\" --root-directory \"{host.DirectoryName}\" --results-directory \"{resultsArgument}\" --no-ansi --progress off {trxArgument}",
                workingDirectory: AssetFixture.TargetAssetPath,
                environmentVariables: new()
                {
                    [SourceDirectoryVariable] = sourceDirectory,
                    ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
                    ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
                    [WaitForCancellationVariable] = "0",
                    [FileNameVariable] = fileName,
                },
                failIfReturnValueIsNotZero: false,
                warnAsError: false,
                suppressPreviewDotNetMessage: false,
                useMultithreadedMSBuild: false,
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(ExitCode.Success);
            result.AssertOutputContains("total: 2");
            result.AssertOutputContains("succeeded: 2");
            (string TestName, string Path)[] attachments = [.. Regex.Matches(
                    result.StandardOutput,
                    @"For test '(?<test>[^']+)': (?<path>[^\r\n]+)")
                .Select(match => (match.Groups["test"].Value, Path.GetFullPath(match.Groups["path"].Value.Trim(), AssetFixture.TargetAssetPath)))];

            AssertPublishedAttachments(attachments, resultsDirectory, longResultsDirectory, fileName);
            AssertOriginalAttachments(sourceDirectory, fileName);
            if (enableTrx)
            {
                AssertTrxAttachments(Path.Combine(resultsDirectory, "results.trx"), resultsDirectory);
            }
        }
        finally
        {
            DeleteDirectory(sourceDirectory);
            DeleteDirectory(resultsRoot);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonRpcTestCases))]
    public async Task JsonRpc_PublishedResultFiles_SurviveProcessExit(string tfm, bool relativeResultsDirectory)
    {
        string resultsName = $"Test Results-{Guid.NewGuid():N}";
        string resultsDirectory = Path.Combine(AssetFixture.TargetAssetPath, resultsName);
        string sourceDirectory = Path.Combine(AssetFixture.TargetAssetPath, $"sources-{Guid.NewGuid():N}");
        var host = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string resultsArgument = relativeResultsDirectory ? resultsName : resultsDirectory;

        try
        {
            using TestingPlatformClient client = await StartAsServerAndConnectToTheClientAsync(
                host,
                new Dictionary<string, string?>
                {
                    [SourceDirectoryVariable] = sourceDirectory,
                    [WaitForCancellationVariable] = "0",
                    [FileNameVariable] = DefaultFileName,
                },
                $"--results-directory \"{resultsArgument}\"",
                workingDirectory: AssetFixture.TargetAssetPath);
            await client.Initialize();
            TestNodeUpdateCollector collector = new();
            ResponseListener listener = await client.RunTests(Guid.NewGuid(), collector.CollectNodeUpdates);
            await listener.WaitCompletion().WaitAsync(TimeSpan.FromMinutes(3), TestContext.CancellationToken);
            await client.Exit();
            Assert.AreEqual(0, await client.WaitServerProcessExit(TestContext.CancellationToken));

            TestNode[] results = collector.TestNodeUpdates
                .Select(update => update.Node)
                .Where(node => node.ExecutionState == "passed")
                .ToArray();
            Assert.HasCount(ExpectedAttachments.Count, results);
            Assert.HasCount(ExpectedAttachments.Count, results.Select(node => node.Uid).Distinct(StringComparer.Ordinal));
            (string TestName, string Path)[] attachments = [.. results.SelectMany(node => node.Properties
                .Where(property => property.Key.StartsWith("attachments.", StringComparison.Ordinal)
                    && property.Key.EndsWith(".uri", StringComparison.Ordinal))
                .Select(property => (node.DisplayName, property.Value.ToString())))];

            AssertPublishedAttachments(attachments, resultsDirectory);
            AssertOriginalAttachments(sourceDirectory);
        }
        finally
        {
            DeleteDirectory(sourceDirectory);
            DeleteDirectory(resultsDirectory);
        }
    }

    [TestMethod]
    public async Task Trx_AbortedRun_PreservesRecoveredResultFiles()
    {
        string resultsDirectory = Path.Combine(AssetFixture.TargetAssetPath, $"results-{Guid.NewGuid():N}");
        string sourceDirectory = Path.Combine(AssetFixture.TargetAssetPath, $"sources-{Guid.NewGuid():N}");
        var host = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        string settings = Path.Combine(AssetFixture.TargetAssetPath, "ordered.runsettings");

        try
        {
            TestHostResult result = await host.ExecuteAsync(
                $"--settings \"{settings}\" --report-trx --report-trx-filename recovered.trx --results-directory \"{resultsDirectory}\" --timeout 15s",
                new()
                {
                    [SourceDirectoryVariable] = sourceDirectory,
                    [WaitForCancellationVariable] = "1",
                    [FileNameVariable] = DefaultFileName,
                    ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
                },
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
            Assert.IsTrue(File.Exists(Path.Combine(sourceDirectory, "waiting-for-cancellation")), "The second test must reach its cancellation rendezvous.");
            result.AssertOutputContains("recovered 1 test result(s)");
            result.AssertOutputContains("Test application process didn't exit gracefully, exit code is '3'");
            result.AssertOutputDoesNotContain("The attachment will be skipped.");
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var document = XDocument.Load(Path.Combine(resultsDirectory, "recovered.trx"));
            Assert.AreEqual("Failed", document.Descendants(ns + "ResultSummary").Single().Attribute("outcome")!.Value);
            XElement recovered = Assert.ContainsSingle(
                testResult => testResult.Attribute("testName")?.Value == "FirstAttachment",
                document.Descendants(ns + "UnitTestResult"));
            AssertTrxResultAttachment(document, recovered, resultsDirectory);
        }
        finally
        {
            DeleteDirectory(sourceDirectory);
            DeleteDirectory(resultsDirectory);
        }
    }

    private static void AssertPublishedAttachments(
        (string TestName, string Path)[] attachments,
        string resultsDirectory,
        bool requireLongPaths = false,
        string expectedFileName = DefaultFileName)
    {
        Assert.AreSequenceEqual(
            ExpectedAttachments.Keys.Order(StringComparer.Ordinal),
            attachments.Select(attachment => attachment.TestName).Order(StringComparer.Ordinal));
        Assert.HasCount(ExpectedAttachments.Count, attachments.Select(attachment => attachment.Path).Distinct(StringComparer.Ordinal));
        foreach ((string testName, string path) in attachments)
        {
            Assert.StartsWith(Path.GetFullPath(resultsDirectory) + Path.DirectorySeparatorChar, Path.GetFullPath(path));
            Assert.AreEqual(expectedFileName, Path.GetFileName(path));
            Assert.IsTrue(File.Exists(path), $"Published attachment for '{testName}' must remain readable after host exit: '{path}'.");
            Assert.AreEqual(ExpectedAttachments[testName], File.ReadAllText(path));
            if (requireLongPaths)
            {
                Assert.IsGreaterThan(WindowsMaxPath, Path.GetFullPath(path).Length);
            }
        }
    }

    private static void AssertOriginalAttachments(string sourceDirectory, string fileName = DefaultFileName)
    {
        foreach (KeyValuePair<string, string> attachment in ExpectedAttachments)
        {
            Assert.AreEqual(
                attachment.Value,
                File.ReadAllText(Path.Combine(sourceDirectory, attachment.Key, fileName)));
        }
    }

    private static void AssertTrxAttachments(string trxPath, string resultsDirectory)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var document = XDocument.Load(trxPath);
        XElement[] results = [.. document.Descendants(ns + "UnitTestResult")];
        Assert.AreSequenceEqual(
            ExpectedAttachments.Keys.Order(StringComparer.Ordinal),
            results.Select(result => result.Attribute("testName")!.Value).Order(StringComparer.Ordinal));
        foreach (XElement result in results)
        {
            AssertTrxResultAttachment(document, result, resultsDirectory);
        }
    }

    private static void AssertTrxResultAttachment(XDocument document, XElement result, string resultsDirectory)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        string deploymentRoot = document.Descendants(ns + "Deployment").Single().Attribute("runDeploymentRoot")!.Value;
        Assert.AreEqual("Passed", result.Attribute("outcome")!.Value);
        XElement resultFile = Assert.ContainsSingle(result.Descendants(ns + "ResultFile"));
        string path = Path.Combine(
            resultsDirectory,
            deploymentRoot,
            "In",
            result.Attribute("relativeResultsDirectory")!.Value,
            resultFile.Attribute("path")!.Value.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
        Assert.AreEqual(ExpectedAttachments[result.Attribute("testName")!.Value], File.ReadAllText(path));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase
    {
        public const string ProjectName = "MSTestResultFiles";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (ProjectName, ProjectName, SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file MSTestResultFiles.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file ordered.runsettings
<RunSettings>
  <MSTest>
    <OrderTestsByNameInClass>true</OrderTestsByNameInClass>
    <RandomizeTestOrder>false</RandomizeTestOrder>
  </MSTest>
</RunSettings>

#file global.json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}

#file AttachmentTests.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestResultFiles;

[TestClass]
public sealed class AttachmentTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void FirstAttachment() => AddAttachment(nameof(FirstAttachment), "first attachment");

    [TestMethod]
    public async Task SecondAttachment()
    {
        if (Environment.GetEnvironmentVariable("MSTEST_RESULT_FILE_WAIT_FOR_CANCELLATION") == "1")
        {
            string sourceDirectory = Environment.GetEnvironmentVariable("MSTEST_RESULT_FILE_SOURCE_DIRECTORY")
                ?? throw new InvalidOperationException("MSTEST_RESULT_FILE_SOURCE_DIRECTORY is not set.");
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllText(Path.Combine(sourceDirectory, "waiting-for-cancellation"), "ready");
            await Task.Delay(Timeout.Infinite, TestContext.CancellationToken);
        }
        else
        {
            AddAttachment(nameof(SecondAttachment), "second attachment");
        }
    }

    private void AddAttachment(string testName, string content)
    {
        string sourceDirectory = Environment.GetEnvironmentVariable("MSTEST_RESULT_FILE_SOURCE_DIRECTORY")
            ?? throw new InvalidOperationException("MSTEST_RESULT_FILE_SOURCE_DIRECTORY is not set.");
        string directory = Path.Combine(sourceDirectory, testName);
        Directory.CreateDirectory(directory);
        string fileName = Environment.GetEnvironmentVariable("MSTEST_RESULT_FILE_NAME")
            ?? throw new InvalidOperationException("MSTEST_RESULT_FILE_NAME is not set.");
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        TestContext.AddResultFile(path);
    }
}
""";
    }
}
