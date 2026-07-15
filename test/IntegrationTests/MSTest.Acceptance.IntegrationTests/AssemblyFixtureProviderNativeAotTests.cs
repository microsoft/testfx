// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AssemblyFixtureProviderNativeAotTests : AcceptanceTestBase<AssemblyFixtureProviderNativeAotTests.TestAssetFixture>
{
    [TestMethod]
    public async Task AssemblyFixtureProvider_WhenDynamicCodeUnsupported_IsSkipped()
    {
        // PublishAot=true sets the RuntimeFeature.IsDynamicCodeSupported feature switch to false even
        // for a managed build (see PublishAotNonNativeTests), so this exercises the runtime guard that
        // skips [AssemblyFixtureProvider] discovery without needing an actual native compile.
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.TestProjectName, TargetFrameworks.NetCurrent);

        // Pass --settings my.runsettings so CaptureTraceOutput=false forwards Console.WriteLine
        // output to stdout (otherwise MSTest captures it and the assertions below cannot see it).
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // The test still runs...
        testHostResult.AssertOutputContains("MyTests.TestMethod ran");

        // ...but the assembly-init / assembly-cleanup declared in the referenced provider library are
        // NOT executed, because provider discovery is skipped when dynamic code is unsupported.
        testHostResult.AssertOutputDoesNotContain("ProviderLibrary.SharedFixtures.AssemblyInit ran");
        testHostResult.AssertOutputDoesNotContain("ProviderLibrary.SharedFixtures.AssemblyCleanup ran");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetName = "AssemblyFixtureProviderNativeAotAcceptance";
        public const string TestProjectName = "AssemblyFixtureProviderNativeAot.Tests";
        public const string LibraryProjectName = "ProviderLibrary";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file AssemblyFixtureProviderNativeAot.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <LangVersion>latest</LangVersion>
    <!-- Sets RuntimeFeature.IsDynamicCodeSupported to false without an actual native compile. -->
    <PublishAot>true</PublishAot>
    <!-- [AssemblyFixtureProvider] is intentionally unsupported here; suppress MSTEST0072 so the
         managed build used by this test is not failed by the analyzer warning it emits. -->
    <NoWarn>$(NoWarn);MSTEST0072</NoWarn>
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
        // Reference a type from the provider library so it is pulled in as a runtime dependency.
        Assert.AreEqual("ok", SharedFixtures.Ping());
        Console.WriteLine("MyTests.TestMethod ran");
    }
}

#file ProviderLibrary/ProviderLibrary.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
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
