// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Reflection;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class RetryFailedTestsTests : AcceptanceTestBase
{
    private const string AssetName = "RetryFailedTests";

    private static TestAssetFixture s_testAssetFixture = null!; // Assigned in ClassInitialize

    public static string Format_Matrix(MethodInfo _, object[] data) => $"{data[0]},{data[1]}";

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
    [DynamicData(nameof(GetMatrix), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(Format_Matrix))]
    public async Task RetryFailedTests_OnlyRetryTimes_Succeeds(string tfm, bool failOnly)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(s_testAssetFixture.TargetAssetPath, AssetName, tfm);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --results-directory {resultDirectory}",
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
    [DynamicData(nameof(GetMatrix), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(Format_Matrix))]
    public async Task RetryFailedTests_MaxPercentage_Succeeds(string tfm, bool fail)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(s_testAssetFixture.TargetAssetPath, AssetName, tfm);
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
    [DynamicData(nameof(GetMatrix), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(Format_Matrix))]
    public async Task RetryFailedTests_MaxTestsCount_Succeeds(string tfm, bool fail)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(s_testAssetFixture.TargetAssetPath, AssetName, tfm);
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

    // We use crash dump, not supported in NetFramework at the moment
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task RetryFailedTests_MoveFiles_Succeeds(string tfm)
        => await RetryHelper.RetryAsync(
            async () =>
            {
                var testHost = TestInfrastructure.TestHost.LocateFrom(s_testAssetFixture.TargetAssetPath, AssetName, tfm);
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

                string[] entries = Directory.GetFiles(resultDirectory, "*.*", SearchOption.AllDirectories)
                    .Where(x => !x.Contains("Retries", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

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

    [ClassInitialize]
    public static async Task InitializeTestAsset(TestContext _)
    {
        s_testAssetFixture = new TestAssetFixture(AcceptanceFixture.Instance);
        await s_testAssetFixture.InitializeAsync();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void CleanupTestAsset()
    {
        s_testAssetFixture.Dispose();
        s_testAssetFixture = null!;
    }

    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture)
        : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                .PatchCodeWithReplace("$TATFVersion$", TATFVersion));
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
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingEnterpriseExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$TATFVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$TATFVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using RetryFailedTests;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
builder.AddRetryProvider();
builder.AddCrashDumpProvider();
builder.AddTrxReportProvider();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
using System;
using System.IO;

namespace RetryFailedTests;

[TestGroup]
public class UnitTest1
{
    static bool _fail = Environment.GetEnvironmentVariable("FAIL") == "1";
    static string _resultDir = Environment.GetEnvironmentVariable("RESULTDIR")!; // Tests are using this env variable so it won't be null.
    static bool _crash = Environment.GetEnvironmentVariable("CRASH") == "1";

    public void TestMethod1()
    {
        if (_crash)
        {
            Environment.FailFast("CRASH");
        }

        bool envVar = Environment.GetEnvironmentVariable("METHOD1") is null;

        if (envVar) return;

        string succeededFile = Path.Combine(_resultDir, "M1_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!_fail)
        {
            if (fileExits) return;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        Assert.IsTrue(assert);
    }

    public void TestMethod2()
    {
        bool envVar = Environment.GetEnvironmentVariable("METHOD2") is null;
        System.Console.WriteLine("envVar " + envVar);

        if (envVar) return;

        string succeededFile = Path.Combine(_resultDir,"M2_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!_fail)
        {
            if (fileExits) return;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        Assert.IsTrue(assert);
    }

    public void TestMethod3()
    {
        bool envVar = Environment.GetEnvironmentVariable("METHOD3") is null;

        if (envVar) return;

        string succeededFile = Path.Combine(_resultDir,"M3_Succeeds");
        bool fileExits = File.Exists(succeededFile);
        bool assert = envVar && fileExits;

        if (!_fail)
        {
            if (fileExits) return;
            if (!assert) File.WriteAllText(succeededFile,"");
        }

        Assert.IsTrue(assert);
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Extensions;
""";
    }
}
