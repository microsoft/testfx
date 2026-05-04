// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class DesktopTestingSdkTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "DesktopTestingSdk";

    private static readonly string DesktopTargetFramework = $"{TargetFrameworks.NetCurrent}-windows";

    private const string SourceCode = """
#file DesktopTestingSdk.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFramework$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableDesktopTesting>true</EnableDesktopTesting>
    $ExtraProperties$
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$(MicrosoftNETTestSdkVersion)" />
  </ItemGroup>
</Project>

#file NotepadTests.cs
using System.Windows.Automation;
using Microsoft.MSTest.DesktopTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NotepadTests : WindowTest
{
    public override string ApplicationPath => "notepad.exe";

    [TestMethod]
    public void Notepad_MainWindow_IsNotNull()
    {
        Assert.IsNotNull(MainWindow);
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
""";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Desktop testing is Windows-only")]
    public async Task EnableDesktopTesting_WhenUsingMSTestRunner_RunsDesktopTests()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", DesktopTargetFramework)
            .PatchCodeWithReplace("$ExtraProperties$", string.Empty));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"test --no-progress --no-ansi -c Release {testAsset.TargetAssetPath}",
            workingDirectory: testAsset.TargetAssetPath,
            cancellationToken: TestContext.CancellationToken);

        compilationResult.AssertExitCodeIs(ExitCodes.Success);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Desktop testing is Windows-only")]
    public async Task EnableDesktopTesting_WhenUsingVSTest_RunsDesktopTests()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TargetFramework$", DesktopTargetFramework)
            .PatchCodeWithReplace("$ExtraProperties$", "<UseVSTest>true</UseVSTest>"));

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"test -c Release {testAsset.TargetAssetPath}",
            workingDirectory: testAsset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            suppressPreviewDotNetMessage: false,
            cancellationToken: TestContext.CancellationToken);

        compilationResult.AssertExitCodeIs(0);
        compilationResult.AssertOutputContains("Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2");
    }
}
