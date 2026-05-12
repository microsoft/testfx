// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class WindowsAppTestingSdkTests : AcceptanceTestBase<WindowsAppTestingSdkTests.TestAssetFixture>
{
    private static readonly string DesktopTargetFramework = $"{TargetFrameworks.NetCurrent}-windows";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows app testing is Windows-only")]
    public async Task EnableWindowsAppTesting_WhenUsingMSTestRunner_RunsDesktopTests()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, DesktopTargetFramework);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows app testing is Windows-only")]
    public async Task EnableWindowsAppTesting_WhenUsingVSTest_RunsDesktopTests()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, DesktopTargetFramework);
        DotnetMuxerResult dotnetTestResult = await DotnetCli.RunAsync(
            $"test {testHost.FullName}",
            workingDirectory: AssetFixture.ProjectPath,
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            suppressPreviewDotNetMessage: false,
            cancellationToken: TestContext.CancellationToken);

        dotnetTestResult.AssertExitCodeIs(0);
        dotnetTestResult.AssertOutputContains("Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "WindowsAppTestingSdk";

        private const string SourceCode = """
#file WindowsAppTestingSdk.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableWindowsAppTesting>true</EnableWindowsAppTesting>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$(MicrosoftNETTestSdkVersion)" />
  </ItemGroup>
</Project>

#file NotepadTests.cs
using System.Windows.Automation;
using Microsoft.MSTest.Windows.AppTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[STATestClass]
public class NotepadTests : WindowTest
{
    public override string ApplicationPath => "notepad.exe";

    [TestMethod]
    public void Notepad_MainWindow_IsVisible()
    {
        Assert.AreEqual(ControlType.Window, MainWindow.Current.ControlType,
            "Expected the main window element to be of control type Window.");
    }

    [TestMethod]
    public void Notepad_MainWindow_HasExpectedTitle()
    {
        string title = MainWindow.Current.Name;
        // Notepad window title is "Untitled - Notepad" or localized equivalent.
        // Just verify it's not empty.
        Assert.IsFalse(string.IsNullOrEmpty(title), "Window title should not be empty.");
    }
}

#file global.json
{
  "test": {
    "runner": "VSTest"
  }
}
""";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$TargetFrameworks$", DesktopTargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
    }
}
