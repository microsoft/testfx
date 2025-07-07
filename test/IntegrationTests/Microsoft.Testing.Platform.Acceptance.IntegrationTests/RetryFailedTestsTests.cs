#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class RetryFailedTestsTests : AcceptanceTestBase<RetryFailedTestsTests.TestAssetFixture>
{
    private const string AssetName = "RetryFailedTests";

    internal static IEnumerable<(string Arguments, bool FailOnly)> GetMatrix()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            foreach (bool failOnly in new[] { true, false })
            {
                yield return (tfm, failOnly);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetMatrix))]
    public async Task RetryFailedTests_OnlyRetryTimes_Succeeds(string tfm, bool failOnly)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --results-directory {resultDirectory} --report-trx",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "METHOD1", "1" },
                { "FAIL", failOnly ? "1" : "0" },
                { "RESULTDIR", resultDirectory },
            });

        if (!failOnly)
        {
            testHostResult.AssertExitCodeIs(ExitCodes.Success);
            testHostResult.AssertOutputContains("Tests suite completed successfully in 2 attempts");
            testHostResult.AssertOutputContains("Failed! -");
            testHostResult.AssertOutputContains("Passed! -");

            string[] trxFiles = Directory.GetFiles(resultDirectory, "*.trx", SearchOption.AllDirectories);
            Assert.AreEqual(2, trxFiles.Length);
            string trxContents1 = File.ReadAllText(trxFiles[0]);
            string trxContents2 = File.ReadAllText(trxFiles[1]);
            Assert.AreNotEqual(trxContents1, trxContents2);
            string id1 = Regex.Match(trxContents1, "<TestRun id=\"(.+?)\"").Groups[1].Value;
            string id2 = Regex.Match(trxContents2, "<TestRun id=\"(.+?)\"").Groups[1].Value;
            Assert.AreEqual(id1, id2);
        }
        else
        {
            testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
            testHostResult.AssertOutputContains("Tests suite failed in all 4 attempts");
            testHostResult.AssertOutputContains("Tests suite failed, total failed tests: 1, exit code: 2, attempt: 1/4");
            testHostResult.AssertOutputContains("Tests suite failed, total failed tests: 1, exit code: 2, attempt: 2/4");
            testHostResult.AssertOutputContains("Tests suite failed, total failed tests: 1, exit code: 2, attempt: 3/4");
            testHostResult.AssertOutputContains("Tests suite failed, total failed tests: 1, exit code: 2, attempt: 4/4");
            testHostResult.AssertOutputDoesNotContain("Tests suite failed, total failed tests: 1, exit code: 2, attempt: 5/4");
            testHostResult.AssertOutputContains("Failed! -");
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetMatrix))]
    public async Task RetryFailedTests_MaxPercentage_Succeeds(string tfm, bool fail)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --retry-failed-tests-max-percentage 50 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "RESULTDIR", resultDirectory },
                { "METHOD1", "1" },
                { fail ? "METHOD2" : "UNUSED", "1" },
            });

        string retriesPath = Path.Combine(resultDirectory, "Retries");
        Assert.IsTrue(Directory.Exists(retriesPath));
        string[] retriesDirectories = Directory.GetDirectories(retriesPath);
        Assert.AreEqual(1, retriesDirectories.Length);
        string createdDirName = Path.GetFileName(retriesDirectories[0]);

        // Asserts that we are not using long names, to reduce long path issues.
        // See https://github.com/microsoft/testfx/issues/4002
        Assert.AreEqual(5, createdDirName.Length, $"Expected directory '{createdDirName}' to be of length 5.");

        if (fail)
        {
            testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
            testHostResult.AssertOutputContains("Failure threshold policy is enabled, failed tests will not be restarted.");
            testHostResult.AssertOutputContains("Percentage failed threshold is 50% and 66.67% tests failed (2/3)");
            testHostResult.AssertOutputContains("Failed! -");
        }
        else
        {
            testHostResult.AssertExitCodeIs(ExitCodes.Success);
            testHostResult.AssertOutputContains("Tests suite completed successfully in 2 attempts");
            testHostResult.AssertOutputContains("Failed! -");
            testHostResult.AssertOutputContains("Passed! -");
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetMatrix))]
    public async Task RetryFailedTests_MaxTestsCount_Succeeds(string tfm, bool fail)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --retry-failed-tests-max-tests 1 --results-directory {resultDirectory}",
            new()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "RESULTDIR", resultDirectory },
                { "METHOD1", "1" },
                { fail ? "METHOD2" : "UNUSED", "1" },
            });

        if (fail)
        {
            testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
            testHostResult.AssertOutputContains("Failure threshold policy is enabled, failed tests will not be restarted.");
            testHostResult.AssertOutputContains("Maximum failed tests threshold is 1 and 2 tests failed");
            testHostResult.AssertOutputContains("Failed! -");
        }
        else
        {
            testHostResult.AssertExitCodeIs(ExitCodes.Success);
            testHostResult.AssertOutputContains("Tests suite completed successfully in 2 attempts");
            testHostResult.AssertOutputContains("Failed! -");
            testHostResult.AssertOutputContains("Passed! -");
        }
    }

    [TestMethod]
    // We use crash dump, not supported in NetFramework at the moment
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_MoveFiles_Succeeds(string tfm)
    {
        // TODO: Crash dump is not working properly on macos, so we skip the test for now
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        await RetryHelper.RetryAsync(
            async () =>
            {
                var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
                string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
                TestHostResult testHostResult = await testHost.ExecuteAsync(
                    $"--report-trx --crashdump --retry-failed-tests 1 --results-directory {resultDirectory}",
                    new()
                    {
                        { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                        { "RESULTDIR", resultDirectory },
                        { "CRASH", "1" },
                    });

                testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);

                string[] entries = [.. Directory.GetFiles(resultDirectory, "*.*", SearchOption.AllDirectories).Where(x => !x.Contains("Retries", StringComparison.OrdinalIgnoreCase))];

                // 1 trx file
                Assert.AreEqual(1, entries.Count(x => x.EndsWith("trx", StringComparison.OrdinalIgnoreCase)));

                // Number of dmp files seems to differ locally and in CI
                int dumpFilesCount = entries.Count(x => x.EndsWith("dmp", StringComparison.OrdinalIgnoreCase));

                if (dumpFilesCount == 2)
                {
                    // Dump file inside the trx structure
                    Assert.AreEqual(1, entries.Count(x => x.Contains($"{Path.DirectorySeparatorChar}In{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && x.EndsWith("dmp", StringComparison.OrdinalIgnoreCase)));
                }
                else if (dumpFilesCount is 0 or > 2)
                {
                    Assert.Fail($"Expected 1 or 2 dump files, but found {dumpFilesCount}");
                }
            }, 3, TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task RetryFailedTests_PassingFromFirstTime_UsingOldDotnetTest_MoveFiles_Succeeds()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"test \"{AssetFixture.TargetAssetPath}\" -- --retry-failed-tests 1 --results-directory \"{resultDirectory}\"",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            workingDirectory: AssetFixture.TargetAssetPath);

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);

        // File names are on the form: RetryFailedTests_tfm_architecture.log
        string[] logFilesFromInvokeTestingPlatformTask = Directory.GetFiles(resultDirectory, "RetryFailedTests_*_*.log");
        Assert.AreEqual(TargetFrameworks.All.Length, logFilesFromInvokeTestingPlatformTask.Length);
        foreach (string logFile in logFilesFromInvokeTestingPlatformTask)
        {
            string logFileContents = File.ReadAllText(logFile);
            Assert.Contains("Test run summary: Passed!", logFileContents);
            Assert.Contains("total: 3", logFileContents);
            Assert.Contains("succeeded: 3", logFileContents);
            Assert.Contains("Tests suite completed successfully in 1 attempts", logFileContents);
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }

        private const string TestCode = """
#file RetryFailedTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
        <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
        <TestingPlatformCaptureOutput>false</TestingPlatformCaptureOutput>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file dotnet.config
[dotnet.test.runner]
name= "VSTest"

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.MSBuild;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(new TrxReportCapability()),
            (_,__) => new DummyTestFramework());
        builder.AddCrashDumpProvider();
        builder.AddTrxReportProvider();
        builder.AddRetryProvider();
        builder.AddMSBuild();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TrxReportCapability : ITrxReportCapability
{
    bool ITrxReportCapability.IsSupported { get; } = true;
    void ITrxReportCapability.Enable()
    {
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        bool fail = Environment.GetEnvironmentVariable("FAIL") == "1";
        // Tests are using this env variable so it won't be null.
        string resultDir = Environment.GetEnvironmentVariable("RESULTDIR")!; 
        bool crash = Environment.GetEnvironmentVariable("CRASH") == "1";

        if (TestMethod1(fail, resultDir, crash))
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "1", DisplayName = "TestMethod1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        }
        else
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "1", DisplayName = "TestMethod1", Properties = new(new FailedTestNodeStateProperty()) }));
        }

        if (TestMethod2(fail, resultDir))
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "2", DisplayName = "TestMethod2", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        }
        else
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "2", DisplayName = "TestMethod2", Properties = new(new FailedTestNodeStateProperty()) }));
        }

        if (TestMethod3(fail, resultDir))
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "3", DisplayName = "TestMethod3", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        }
        else
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "3", DisplayName = "TestMethod3", Properties = new(new FailedTestNodeStateProperty()) }));
        }
        
        context.Complete();
    }

    private bool TestMethod1(bool fail, string resultDir, bool crash)
    {
        if (crash)
        {
            Environment.FailFast("CRASH");
        }

        bool envVar = Environment.GetEnvironmentVariable("METHOD1") is null;

        if (envVar) return true;

        string succeededFile = Path.Combine(resultDir, "M1_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!fail)
        {
            if (fileExits) return true;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        return assert;        
    }

    private bool TestMethod2(bool fail, string resultDir)
    {
        bool envVar = Environment.GetEnvironmentVariable("METHOD2") is null;
        System.Console.WriteLine("envVar " + envVar);

        if (envVar) return true;

        string succeededFile = Path.Combine(resultDir,"M2_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!fail)
        {
            if (fileExits) return true;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        return assert;
    }

    private bool TestMethod3(bool fail, string resultDir)
    {
        bool envVar = Environment.GetEnvironmentVariable("METHOD3") is null;

        if (envVar) return true;

        string succeededFile = Path.Combine(resultDir,"M3_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!fail)
        {
            if (fileExits) return true;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        return assert;
    }
}
""";
    }
}
