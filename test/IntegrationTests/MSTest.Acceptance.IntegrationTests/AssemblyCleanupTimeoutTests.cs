// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class AssemblyCleanupTimeoutTests : AcceptanceTestBase
{
    private static readonly Dictionary<string, (string MethodFullName, string Prefix, string EnvVarSuffix, string RunSettingsEntryName)> InfoByKind = new()
    {
        ["assembly"] = ("TestClass.AssemblyCleanup", "Assembly", "ASSEMBLYCLEANUP", "AssemblyCleanupTimeout"),
    };

    private readonly TestAssetFixture _testAssetFixture;

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public AssemblyCleanupTimeoutTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupTaskIsCancelled(string tfm)
        => await RunAndAssertTestTimedOutAsync(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout,
            tfm, "LONG_WAIT_", "assembly");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_FromRunSettings_AssemblyCleanupIsCancelled(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 300, false, "assembly");

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyCleanup_WhenTimeoutExpires_AssemblyCleanupIsCancelled_AttributeTakesPrecedence(string tfm)
        => await RunAndAssertWithRunSettingsAsync(tfm, 25000, true, "assembly");

    private async Task RunAndAssertTestTimedOutAsync(string rootFolder, string assetName, string tfm, string envVarPrefix, string entryKind)
    {
        var testHost = TestHost.LocateFrom(rootFolder, assetName, tfm);
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { { envVarPrefix + InfoByKind[entryKind].EnvVarSuffix, "1" } });
        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} cleanup method '{InfoByKind[entryKind].MethodFullName}' timed out");
    }

    private async Task RunAndAssertWithRunSettingsAsync(string tfm, int timeoutValue, bool assertAttributePrecedence, string entryKind)
    {
        string runSettingsEntry = InfoByKind[entryKind].RunSettingsEntryName;
        string runSettings = $"""
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <{runSettingsEntry}>{timeoutValue}</{runSettingsEntry}>
    </MSTest>
</RunSettings>
""";

        var testHost = assertAttributePrecedence
            ? TestHost.LocateFrom(_testAssetFixture.CodeWithOneSecTimeoutAssetPath, TestAssetFixture.CodeWithOneSecTimeout, tfm)
            : TestHost.LocateFrom(_testAssetFixture.CodeWithNoTimeoutAssetPath, TestAssetFixture.CodeWithNoTimeout, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.runsettings");
        File.WriteAllText(runSettingsFilePath, runSettings);

        Stopwatch stopwatch = Stopwatch.StartNew();
        var testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", environmentVariables: new() { { $"TIMEOUT_{InfoByKind[entryKind].EnvVarSuffix}", "1" } });
        stopwatch.Stop();

        if (assertAttributePrecedence)
        {
            Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 25);
        }

        testHostResult.AssertOutputContains($"{InfoByKind[entryKind].Prefix} initialize method '{InfoByKind[entryKind].MethodFullName}' timed out");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string CodeWithOneSecTimeout = nameof(CodeWithOneSecTimeout);
        public const string CodeWithSixtySecTimeout = nameof(CodeWithSixtySecTimeout);
        public const string CodeWithNoTimeout = nameof(CodeWithNoTimeout);

        public string CodeWithOneSecTimeoutAssetPath => GetAssetPath(CodeWithOneSecTimeout);

        public string CodeWithSixtySecTimeoutAssetPath => GetAssetPath(CodeWithSixtySecTimeout);

        public string CodeWithNoTimeoutAssetPath => GetAssetPath(CodeWithNoTimeout);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (CodeWithNoTimeout, CodeWithNoTimeout,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", string.Empty)
                .PatchCodeWithReplace("$ProjectName$", CodeWithNoTimeout)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CodeWithOneSecTimeout, CodeWithOneSecTimeout,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", "[Timeout(1000)]")
                .PatchCodeWithReplace("$ProjectName$", CodeWithOneSecTimeout)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CodeWithSixtySecTimeout, CodeWithSixtySecTimeout,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", "[Timeout(60000)]")
                .PatchCodeWithReplace("$ProjectName$", CodeWithSixtySecTimeout)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    $TimeoutAttribute$
    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        
        if (Environment.GetEnvironmentVariable("LONG_WAIT_ASSEMBLYCLEANUP") == "1")
        {
            await Task.Delay(10_000);
        }
        else
        {
            await Task.CompletedTask;
        }
    }
    [TestMethod]
    public Task Test1() => Task.CompletedTask;
}
""";
    }
}
