// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class AppDomainTests : AcceptanceTestBase<AppDomainTests.TestAssetFixture>
{
    private const string AssetName = "AppDomainTests";

    // In v5 MSTest always runs on Microsoft.Testing.Platform. The test asset is a net462 MTP
    // executable, so we validate MSTest's .NET Framework AppDomain isolation by running the app
    // standalone on Microsoft.Testing.Platform. The DisableAppDomain run setting (child-AppDomain
    // isolation on/off) is honored by MSTest via the passed .runsettings.
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DataRow(null)]
    public async Task RunTests_Standalone(bool? disableAppDomain)
    {
        string exePath = GetTestExePath(AssetFixture.TargetAssetPath, TargetFrameworks.NetFramework[0]);
        Assert.IsTrue(File.Exists(exePath), $"Test exe not found at {exePath}");

        using RunSettingsFile runSettings = CreateRunSettingsFile(disableAppDomain);
        using var commandLine = new CommandLine();
        await commandLine.RunAsync(
            $"\"{exePath}\"{runSettings.MTPArgument}",
            cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DataRow(null)]
    public async Task DiscoverTests_Standalone(bool? disableAppDomain)
    {
        string exePath = GetTestExePath(AssetFixture.TargetAssetPath, TargetFrameworks.NetFramework[0]);
        Assert.IsTrue(File.Exists(exePath), $"Test exe not found at {exePath}");

        using RunSettingsFile runSettings = CreateRunSettingsFile(disableAppDomain);
        using var commandLine = new CommandLine();
        await commandLine.RunAsync(
            $"\"{exePath}\" --list-tests{runSettings.MTPArgument}",
            cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DataRow(null)]
    public async Task RunTests_With_PackagedVSTest(bool? disableAppDomain)
    {
        string exePath = GetTestExePath(AssetFixture.TargetAssetPath, TargetFrameworks.NetFramework[0]);
        Assert.IsTrue(File.Exists(exePath), $"Test exe not found at {exePath}");

        using RunSettingsFile runSettings = CreateRunSettingsFile(disableAppDomain);
        using var commandLine = new CommandLine();
        await commandLine.RunAsync(
            $"\"{VSTestConsoleLocator.GetConsoleRunnerPath()}\" \"{exePath}\"{runSettings.VSTestArgument}",
            cancellationToken: TestContext.CancellationToken);

        Assert.Contains("Test Run Successful.", commandLine.StandardOutput);
        Assert.Contains("Total tests: 2", commandLine.StandardOutput);
        Assert.Contains("Passed: 2", commandLine.StandardOutput);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DataRow(null)]
    public async Task DiscoverTests_With_PackagedVSTest(bool? disableAppDomain)
    {
        string exePath = GetTestExePath(AssetFixture.TargetAssetPath, TargetFrameworks.NetFramework[0]);
        Assert.IsTrue(File.Exists(exePath), $"Test exe not found at {exePath}");

        using RunSettingsFile runSettings = CreateRunSettingsFile(disableAppDomain);
        using var commandLine = new CommandLine();
        await commandLine.RunAsync(
            $"\"{VSTestConsoleLocator.GetConsoleRunnerPath()}\" \"{exePath}\" /ListTests{runSettings.VSTestArgument}",
            cancellationToken: TestContext.CancellationToken);

        Assert.Contains("TestMethod1", commandLine.StandardOutput);
        Assert.Contains("TestMethod2", commandLine.StandardOutput);
    }

    private static RunSettingsFile CreateRunSettingsFile(bool? disableAppDomain)
    {
        if (disableAppDomain is not bool value)
        {
            return new(null);
        }

        string runSettingsPath = Path.Combine(Path.GetTempPath(), $"AppDomainTests-{Guid.NewGuid():N}.runsettings");
        File.WriteAllText(
            runSettingsPath,
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>{(value ? "true" : "false")}</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        return new(runSettingsPath);
    }

    private static string GetTestExePath(string assetPath, string targetFramework) =>
        Path.Combine(assetPath, "bin", "Release", targetFramework, $"{AssetName}.exe");

    private sealed class RunSettingsFile(string? path) : IDisposable
    {
        public string MTPArgument => path is null ? string.Empty : $" --settings \"{path}\"";

        public string VSTestArgument => path is null ? string.Empty : $" /Settings:\"{path}\"";

        public void Dispose()
        {
            if (path is not null)
            {
                File.Delete(path);
            }
        }
    }

    public TestContext TestContext { get; set; }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string SingleTestSourceCode = """
#file AppDomainTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PlatformTarget>x64</PlatformTarget>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <!-- Build as a Microsoft.Testing.Platform executable so it runs standalone on MTP. -->
    <OutputType>Exe</OutputType>
    <!-- Force the locally-built Microsoft.Testing.Platform (our test infrastructure keys the local -dev
         platform dependency off this property); otherwise a preview transitive would win. -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <NoWarn>$(NoWarn);NU1507;NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageDownload Include="Microsoft.TestPlatform" Version="[$MicrosoftNETTestSdkVersion$]" />
  </ItemGroup>

</Project>

#file global.json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
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
        // AppDomain.BaseDirectory carries a trailing directory separator while Path.GetDirectoryName
        // does not, so normalize both before comparing. This validates that MSTest still runs .NET
        // Framework tests in an AppDomain based at the test assembly's directory (isolation).
        private static string NormalizeDir(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual(NormalizeDir(Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location)), NormalizeDir(AppDomain.CurrentDomain.BaseDirectory));
        }

        [TestMethod]
        [DynamicData(nameof(GetData))]
        public void TestMethod2(int _)
        {
            Assert.AreEqual(NormalizeDir(Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location)), NormalizeDir(AppDomain.CurrentDomain.BaseDirectory));
        }

        public static IEnumerable<int> GetData()
        {
            if (NormalizeDir(Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location)) != NormalizeDir(AppDomain.CurrentDomain.BaseDirectory))
            {
                Environment.FailFast(
                    $"Expected {Path.GetDirectoryName(typeof(UnitTest1).Assembly.Location)} to be equal to {AppDomain.CurrentDomain.BaseDirectory}");
            }

            yield return 1;
        }
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SingleTestSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetFramework[0])
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion));
    }
}
