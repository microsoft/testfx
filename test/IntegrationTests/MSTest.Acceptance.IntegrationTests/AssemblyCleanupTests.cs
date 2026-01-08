// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AssemblyCleanupTests : AcceptanceTestBase<AssemblyCleanupTests.TestAssetFixture>
{
    [TestMethod]
    public async Task AssemblyCleanupShouldRunAfterAllClassCleanupsHaveCompleted()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "AssemblyCleanupTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file AssemblyCleanupTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file TestClass1.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

[TestClass]
public class TestClass1
{
    public static bool ClassCleanupFinished { get; private set; }

    [TestMethod]
    public void Test1()
    {
    }

    [ClassCleanup]
    public static void Cleanup1()
    {
        Thread.Sleep(4000);
        ClassCleanupFinished = true;
    }
}

[TestClass]
public class TestClass2
{
    [TestMethod]
    public void Test2()
    {
    }

    [ClassCleanup]
    public static void Cleanup2()
        => Thread.Sleep(2000);
}

[TestClass]
public static class Asm
{
    [AssemblyCleanup]
    public static void AsmCleanup()
        => Assert.IsTrue(TestClass1.ClassCleanupFinished);
}

""";
    }

    public TestContext TestContext { get; set; }
}
