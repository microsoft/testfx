// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AssemblyFixtureProviderTests : AcceptanceTestBase<AssemblyFixtureProviderTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AssemblyFixtureProvider_FromReferencedLibrary_RunsAssemblyInitializeAndCleanup(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.TestProjectName, tfm);
        // Pass --settings my.runsettings so CaptureTraceOutput=false forwards Console.WriteLine
        // output to stdout (otherwise MSTest captures it and the assertions below cannot see it).
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // The assembly-init method declared in the *referenced library* must run before the test,
        // and the assembly-cleanup must run after — even though the test project itself declares
        // no [AssemblyInitialize] / [AssemblyCleanup] method.
        testHostResult.AssertOutputContains("ProviderLibrary.SharedFixtures.AssemblyInit ran");
        testHostResult.AssertOutputContains("MyTests.TestMethod ran");
        testHostResult.AssertOutputContains("ProviderLibrary.SharedFixtures.AssemblyCleanup ran");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        // This asset has a multi-targeting ProjectReference (ProviderLibrary). The default
        // SourceGeneration build redirects bin/obj to an isolated sub-folder, but RAR for the net8.0
        // leg ends up resolving ProviderLibrary's transitive MSTest.TestFramework.Extensions.dll from
        // the net10.0 reflection output (bin/Release/net10.0) instead of the net8.0 one. That produces
        // MSB3277 System.* version conflicts (8.0.0.0 vs 9.0.0.0) which the source-gen build promotes to
        // errors via MSBuildTreatWarningsAsErrors. The test only exercises the reflection build, so opt
        // out of the source-gen build; it stays reflection-only.
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public const string AssetName = "AssemblyFixtureProviderAcceptance";
        public const string TestProjectName = "AssemblyFixtureProvider.Tests";
        public const string LibraryProjectName = "ProviderLibrary";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file AssemblyFixtureProvider.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="ProviderLibrary/ProviderLibrary.csproj" />
    <Compile Remove="ProviderLibrary/**" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file MyTests.cs
using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ProviderLibrary;

namespace MyTests;

[TestClass]
public class MyTests
{
    [TestMethod]
    public void TestMethod()
    {
        // Reference a type from the provider library so the test project unambiguously pulls
        // it in as a runtime dependency (and so the BFS over GetReferencedAssemblies() sees it).
        Assert.AreEqual("ok", SharedFixtures.Ping());
        Console.WriteLine("MyTests.TestMethod ran");
    }
}

#file ProviderLibrary/ProviderLibrary.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <IsPackable>false</IsPackable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file ProviderLibrary/SharedFixtures.cs
using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AssemblyFixtureProvider(typeof(ProviderLibrary.SharedFixtures))]

namespace ProviderLibrary;

public static class SharedFixtures
{
    public static string Ping() => "ok";

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
        => Console.WriteLine("ProviderLibrary.SharedFixtures.AssemblyInit ran");

    [AssemblyCleanup]
    public static void AssemblyCleanup()
        => Console.WriteLine("ProviderLibrary.SharedFixtures.AssemblyCleanup ran");
}
""";
    }

    public TestContext TestContext { get; set; }
}
