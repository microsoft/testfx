// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class AppDomainTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "AppDomainTests";

    private const string SingleTestSourceCode = """
#file AppDomainTests.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$" >

  <PropertyGroup>
    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <NoWarn>$(NoWarn);NU1507</NoWarn>
    <UseVSTest>true</UseVSTest>
  </PropertyGroup>

</Project>

#file global.json
{
  "test": {
    "runner": "VSTest"
  }
}

#file UnitTest1.cs
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppDomainTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual(Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location), AppDomain.CurrentDomain.BaseDirectory);
        }

        [TestMethod]
        [DynamicData(nameof(GetData))]
        public void TestMethod2(int _)
        {
            Assert.AreEqual(Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location), AppDomain.CurrentDomain.BaseDirectory);
        }

        public static IEnumerable<int> GetData()
        {
            if (Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location) != AppDomain.CurrentDomain.BaseDirectory)
            {
                Environment.FailFast(
                    $"Expected {Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location)} to be equal to {AppDomain.CurrentDomain.BaseDirectory}");
            }

            yield return 1;
        }
    }
}
""";

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DataRow(null)]
    public async Task RunTests_With_VSTest(bool? disableAppDomain)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetFramework[0]));

        string disableAppDomainCommand = disableAppDomain switch
        {
            true => " -- RunConfiguration.DisableAppDomain=true",
            false => " -- RunConfiguration.EnableAppDomain=false",
            null => string.Empty,
        };

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test {testAsset.TargetAssetPath}{disableAppDomainCommand}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, workingDirectory: testAsset.TargetAssetPath, cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, compilationResult.ExitCode);

        compilationResult.AssertOutputContains(@"Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DataRow(null)]
    public async Task DiscoverTests_With_VSTest(bool? disableAppDomain)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SingleTestSourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetFramework[0]));

        string disableAppDomainCommand = disableAppDomain switch
        {
            true => " -- RunConfiguration.DisableAppDomain=true",
            false => " -- RunConfiguration.EnableAppDomain=false",
            null => string.Empty,
        };

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test {testAsset.TargetAssetPath} --list-tests{disableAppDomainCommand}", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, workingDirectory: testAsset.TargetAssetPath, cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, compilationResult.ExitCode);
    }

    public TestContext TestContext { get; set; }
}
