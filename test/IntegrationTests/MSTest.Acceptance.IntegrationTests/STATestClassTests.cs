// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class STATestClassTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;
    private const string AssetName = "STATestClass";

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public STATestClassTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestClass_OnWindows_ThreadIsSTA(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed!");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file mta.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>MTA</ExecutionThreadApartmentState>
    </RunConfiguration>
</RunSettings>

#file STATestClass.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[STATestClass]
public class TestClass : IDisposable
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        AssertCorrectThreadApartmentMTAState();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        AssertCorrectThreadApartmentMTAState();
    }
    public TestClass()
    {
        AssertCorrectThreadApartmentSTAState();
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        AssertCorrectThreadApartmentSTAState();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        AssertCorrectThreadApartmentSTAState();
    }

    [TestInitialize]
    public void TestInitialize()
    {
        AssertCorrectThreadApartmentSTAState();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        AssertCorrectThreadApartmentSTAState();
    }

    [TestMethod]
    public void TestMethod1()
    {
        AssertCorrectThreadApartmentSTAState();
    }

    private static void AssertCorrectThreadApartmentMTAState()
    {
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        Assert.AreEqual(ApartmentState.MTA, apartmentState);
    }

    private static void AssertCorrectThreadApartmentSTAState()
    {
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        Assert.AreEqual(ApartmentState.STA, apartmentState);
    }

    public void Dispose()
    {
        AssertCorrectThreadApartmentSTAState();
    }
}
""";
    }
}
