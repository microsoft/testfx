// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ExecutableConditionTests : AcceptanceTestBase<ExecutableConditionTests.TestAssetFixture>
{
    [TestMethod]
    public async Task ExecutableCondition_RunsAndSkipsTestsBasedOnToolAvailability()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 1);
        testHostResult.AssertOutputContains("skipped MissingExecutable");
        testHostResult.AssertOutputContains("Test is only supported when executable 'definitely-not-a-real-executable-mstest-10615' is available on PATH");
        testHostResult.AssertOutputDoesNotContain("MISSING EXECUTABLE TEST RAN");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = nameof(ExecutableConditionTests);

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file ExecutableConditionTests.csproj
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

#file Tests.cs
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Tests
{
    [TestMethod]
    [ExecutableCondition("dotnet")]
    public void PresentExecutable()
        => Console.WriteLine("PRESENT EXECUTABLE TEST RAN");

    [TestMethod]
    [ExecutableCondition("definitely-not-a-real-executable-mstest-10615")]
    public void MissingExecutable()
        => Console.WriteLine("MISSING EXECUTABLE TEST RAN");
}
""";
    }

    public TestContext TestContext { get; set; }
}
