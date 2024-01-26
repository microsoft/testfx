// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class AssemblyAndClassInitializeTimeout : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public AssemblyAndClassInitializeTimeout(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_ClassInitializeIsCancelled(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameAttributeTargetAssetPath, TestAssetFixture.MSTestAssetNameAttribute, tfm, buildConfiguration: BuildConfiguration.Release);
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_CLASSINIT", "1" } });
        testHostResult.AssertOutputContains("Class Initialization method TestClass.ClassInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled..");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_ClassInitializeIsCancelled(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameAttributeTargetAssetPath, TestAssetFixture.MSTestAssetNameAttribute, tfm, buildConfiguration: BuildConfiguration.Release);
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_BASE_CLASSINIT", "1" } });
        testHostResult.AssertOutputContains("Class Initialization method TestClass.ClassInitBase threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled..");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_AssemblyInitializeIsCancelled(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameAttributeTargetAssetPath, TestAssetFixture.MSTestAssetNameAttribute, tfm, buildConfiguration: BuildConfiguration.Release);
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_ASSEMBLYINIT", "1" } });
        testHostResult.AssertOutputContains("Assembly Initialization method TestClass.AssemblyInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled.. Aborting test execution.");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_FromRunSettings_ClassInitializeIsCancelled(string tfm)
    {
        string runSettings = """
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <ClassInitializeTimeout>3000</ClassInitializeTimeout>
    </MSTest>
</RunSettings>
""";
        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameRunSettingsTargetAssetPath, TestAssetFixture.MSTestAssetNameRunSettings, tfm, buildConfiguration: BuildConfiguration.Release);
        string runsettingsFilePath = Path.Combine(testHost.DirectoryName, "config.runsettings");
        File.WriteAllText(runsettingsFilePath, runSettings);
        var testHostResult = await testHost.ExecuteAsync(command: $"--settings {runsettingsFilePath}", environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_CLASSINIT", "1" } });
        testHostResult.AssertOutputContains("Class Initialization method TestClass.ClassInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled..");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_FromRunSettings_AssemblyInitializeIsCancelled(string tfm)
    {
        string runSettings = """
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <AssemblyInitializeTimeout>3000</AssemblyInitializeTimeout>
    </MSTest>
</RunSettings>
""";

        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameRunSettingsTargetAssetPath, TestAssetFixture.MSTestAssetNameRunSettings, tfm, buildConfiguration: BuildConfiguration.Release);
        string runsettingsFilePath = Path.Combine(testHost.DirectoryName, "config.runsettings");
        File.WriteAllText(runsettingsFilePath, runSettings);
        var testHostResult = await testHost.ExecuteAsync(command: $"--settings {runsettingsFilePath}", environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_ASSEMBLYINIT", "1" } });
        testHostResult.AssertOutputContains("Assembly Initialization method TestClass.AssemblyInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled.. Aborting test execution.");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task ClassInitialize_WhenTimeoutExpires_ClassInitializeIsCancelled_AttributeTakesPrecedence(string tfm)
    {
        string runSettings = """
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <ClassInitializeTimeout>25000</ClassInitializeTimeout>
    </MSTest>
</RunSettings>
""";

        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameAttributeTargetAssetPath, TestAssetFixture.MSTestAssetNameAttribute, tfm, buildConfiguration: BuildConfiguration.Release);
        string runsettingsFilePath = Path.Combine(testHost.DirectoryName, "config.runsettings");
        File.WriteAllText(runsettingsFilePath, runSettings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        var testHostResult = await testHost.ExecuteAsync(command: $"--settings {runsettingsFilePath}", environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_CLASSINIT", "1" } });
        stopwatch.Stop();
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 25);
        testHostResult.AssertOutputContains("Class Initialization method TestClass.ClassInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled..");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task BaseClassInitialize_WhenTimeoutExpires_ClassInitializeIsCancelled_AttributeTakesPrecedence(string tfm)
    {
        string runSettings = """
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <ClassInitializeTimeout>25000</ClassInitializeTimeout>
    </MSTest>
</RunSettings>
""";

        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameAttributeTargetAssetPath, TestAssetFixture.MSTestAssetNameAttribute, tfm, buildConfiguration: BuildConfiguration.Release);
        string runsettingsFilePath = Path.Combine(testHost.DirectoryName, "config.runsettings");
        File.WriteAllText(runsettingsFilePath, runSettings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_BASE_CLASSINIT", "1" } });
        stopwatch.Stop();
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 25);
        testHostResult.AssertOutputContains("Class Initialization method TestClass.ClassInitBase threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled..");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task AssemblyInitialize_WhenTimeoutExpires_AssemblyInitializeIsCancelled_AttributeTakesPrecedence(string tfm)
    {
        string runSettings = """
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
    <MSTest>
        <AssemblyInitializeTimeout>25000</AssemblyInitializeTimeout>
    </MSTest>
</RunSettings>
""";

        var testHost = TestHost.LocateFrom(_testAssetFixture.MSTestAssetNameAttributeTargetAssetPath, TestAssetFixture.MSTestAssetNameAttribute, tfm, buildConfiguration: BuildConfiguration.Release);
        string runsettingsFilePath = Path.Combine(testHost.DirectoryName, "config.runsettings");
        File.WriteAllText(runsettingsFilePath, runSettings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        var testHostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { "TIMEOUT_ASSEMBLYINIT", "1" } });
        stopwatch.Stop();
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds < 25);
        testHostResult.AssertOutputContains("Assembly Initialization method TestClass.AssemblyInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled.. Aborting test execution.");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string MSTestAssetNameAttribute = "AssemblyAndClassInitializeTimeoutAttribute";
        public const string MSTestAssetNameRunSettings = "AssemblyAndClassInitializeTimeoutRunSettings";

        public string MSTestAssetNameAttributeTargetAssetPath => GetAssetPath(MSTestAssetNameAttribute);

        public string MSTestAssetNameRunSettingsTargetAssetPath => GetAssetPath(MSTestAssetNameRunSettings);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (MSTestAssetNameRunSettings, MSTestAssetNameRunSettings,
            SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", string.Empty)
                .PatchCodeWithReplace("$ProjectName$", MSTestAssetNameRunSettings)
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (MSTestAssetNameAttribute, MSTestAssetNameAttribute,
                SourceCode
                .PatchCodeWithReplace("$TimeoutAttribute$", "[Timeout(2000)]")
                .PatchCodeWithReplace("$ProjectName$", MSTestAssetNameAttribute)
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

public class TestClassBase
{
    $TimeoutAttribute$
    [ClassInitialize(inheritanceBehavior: InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task ClassInitBase(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TIMEOUT_BASE_CLASSINIT") == "1")
        {
            await Task.Delay(10_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

}

[TestClass]
public class TestClass : TestClassBase
{
    $TimeoutAttribute$
    [AssemblyInitialize]
    public static async Task AssemblyInit(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TIMEOUT_ASSEMBLYINIT") == "1")
        {
            await Task.Delay(60_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    $TimeoutAttribute$
    [ClassInitialize]
    public static async Task ClassInit(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TIMEOUT_CLASSINIT") == "1")
        {
            await Task.Delay(60_000, testContext.CancellationTokenSource.Token);
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
