// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Exercises the locally packed MSTest packages from a real Modern UWP application.
/// </summary>
[TestClass]
[TestCategory("WindowsApplicationModel")]
[MemberCondition(
    typeof(AcceptanceTestBase),
    nameof(AcceptanceTestBase.IsWindowsApplicationModelTestEnvironment),
    IgnoreMessage = "Requires the dedicated preflight-gated Windows application-model test environment.")]
[DoNotParallelize]
public sealed class ModernUwpTests : AcceptanceTestBase
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Modern UWP execution is supported only on Windows.")]
    public async Task ModernUwp_ConsumesUwpAssets_AndRunsPlainAndUiTestsThroughVSTest()
    {
        string uniqueSuffix = Guid.NewGuid().ToString("N");
        string assetName = $"ModernUwp{uniqueSuffix[..12]}";
        string packageIdentityName = $"MSTestModernUwp{uniqueSuffix}";
        string sourceCode = ModernUwpSourceCode
            .PatchCodeWithReplace("$AssetName$", assetName)
            .PatchCodeWithReplace("$PackageIdentityName$", packageIdentityName)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$TestPlatformVersion$", MicrosoftNETTestSdkVersion);

        TestAsset testAsset = await TestAsset.GenerateAssetAsync(assetName, sourceCode);
        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            testAsset,
            packageIdentityName,
            async () =>
            {
                WindowsApplicationModelTestTools.CopySampleAssets(testAsset.TargetAssetPath);
                await EnsureGeneratedCSharpFilesHaveUtf8BomAsync(testAsset.TargetAssetPath, TestContext.CancellationToken);

                VisualStudioTestTools tools = await WindowsApplicationModelTestTools.LocateModernUwpVisualStudioToolsAsync(TestContext.CancellationToken);
                UwpBuildResult build = await WindowsApplicationModelTestTools.BuildUwpAssetAsync(
                    tools,
                    testAsset,
                    $"{assetName}.csproj",
                    TestContext.CancellationToken);

                string resolvedAssetsReport = Path.Combine(testAsset.TargetAssetPath, "resolved-mstest-assets.txt");
                WindowsApplicationModelTestTools.AssertResolvedMSTestAssets(
                    resolvedAssetsReport,
                    WindowsApplicationModelAssetKind.ModernUwp);
                Assert.IsTrue(
                    Directory.Exists(build.PackageLayoutPath),
                    $"The Modern UWP package layout '{build.PackageLayoutPath}' does not exist. Binlog: '{build.BinlogPath}'.");

                string resultsDirectory = Path.Combine(testAsset.TargetAssetPath, "TestResults");
                UwpRunResult run = await WindowsApplicationModelTestTools.RunUwpRecipeAsync(
                    tools,
                    build.RecipePath,
                    resultsDirectory,
                    TestContext.CancellationToken);

                Assert.AreEqual(
                    0,
                    run.ExitCode,
                    $"VSTest failed to execute the real Modern UWP recipe '{build.RecipePath}'. TRX: '{run.TrxPath}'. " +
                    $"Binlog: '{build.BinlogPath}'.{Environment.NewLine}Standard output:{Environment.NewLine}{run.StandardOutput}" +
                    $"{Environment.NewLine}Standard error:{Environment.NewLine}{run.ErrorOutput}");
                Assert.DoesNotContain(
                    "No test is available",
                    run.StandardOutput,
                    $"VSTest reported no discovered tests for '{build.RecipePath}'.");
                Assert.DoesNotContain(
                    "Total tests: 0",
                    run.StandardOutput,
                    $"VSTest reported zero discovered tests for '{build.RecipePath}'.");

                AssertModernUwpTrx(run.TrxPath, build);
            });
    }

    public TestContext TestContext { get; set; } = default!;

    private static async Task EnsureGeneratedCSharpFilesHaveUtf8BomAsync(
        string targetAssetPath,
        CancellationToken cancellationToken)
    {
        byte[] utf8Preamble = Encoding.UTF8.GetPreamble();
        foreach (string path in Directory.GetFiles(targetAssetPath, "*.cs", SearchOption.AllDirectories))
        {
            string content = await File.ReadAllTextAsync(path, cancellationToken);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
            byte[] actualPreamble = new byte[utf8Preamble.Length];
            await using FileStream stream = File.OpenRead(path);
            int bytesRead = await stream.ReadAsync(actualPreamble, cancellationToken);
            Assert.AreEqual(utf8Preamble.Length, bytesRead, $"Generated C# file '{path}' is shorter than the UTF-8 BOM.");
            CollectionAssert.AreEqual(utf8Preamble, actualPreamble, $"Generated C# file '{path}' is not UTF-8 with BOM.");
        }
    }

    private static void AssertModernUwpTrx(string trxPath, UwpBuildResult build)
    {
        Assert.IsTrue(
            File.Exists(trxPath),
            $"VSTest did not create the expected TRX '{trxPath}' for recipe '{build.RecipePath}'. Binlog: '{build.BinlogPath}'.");

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var trx = XDocument.Load(trxPath);
        XElement[] results = [.. trx.Descendants(ns + "UnitTestResult")];
        Assert.HasCount(2, results, $"Expected exactly the plain and UI Modern UWP tests in '{trxPath}'.");

        string[] actualTestNames = results
            .Select(result => (string?)result.Attribute("testName"))
            .Where(name => name is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expectedTestNames =
        [
            "PlainTestMethod_RunsInPackagedUwp",
            "UITestMethod_RunsOnCoreWindowDispatcher",
        ];
        CollectionAssert.AreEqual(expectedTestNames, actualTestNames, $"Unexpected test results were written to '{trxPath}'.");
        Assert.IsTrue(
            results.All(result => (string?)result.Attribute("outcome") == "Passed"),
            $"Every Modern UWP result must pass. TRX: '{trxPath}'.");

        XElement counters = trx.Descendants(ns + "Counters").Single();
        AssertCounter(counters, "total", 2, trxPath);
        AssertCounter(counters, "executed", 2, trxPath);
        AssertCounter(counters, "passed", 2, trxPath);
        AssertCounter(counters, "failed", 0, trxPath);
        AssertCounter(counters, "error", 0, trxPath);
        AssertCounter(counters, "inconclusive", 0, trxPath);
        AssertCounter(counters, "notExecuted", 0, trxPath);
    }

    private static void AssertCounter(XElement counters, string name, int expected, string trxPath)
        => Assert.AreEqual(
            expected.ToString(CultureInfo.InvariantCulture),
            (string?)counters.Attribute(name),
            $"Unexpected '{name}' counter in '{trxPath}'.");

    private const string ModernUwpSourceCode = """
#file $AssetName$.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <Platforms>x64</Platforms>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
    <PublishProfile>win-$(Platform).pubxml</PublishProfile>
    <DefaultLanguage>en-US</DefaultLanguage>
    <PublishAot>true</PublishAot>
    <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
    <UseUwp>true</UseUwp>
    <DisableRuntimeMarshalling>true</DisableRuntimeMarshalling>
    <EnableMsixTooling>true</EnableMsixTooling>
  </PropertyGroup>

  <ItemGroup>
    <ProjectCapability Include="TestContainer" />
    <SDKReference Include="TestPlatform.Universal, Version=$(VisualStudioVersion)" />
    <RuntimeHostConfigurationOption Include="MSTest.EnableParentProcessQuery"
                                    Value="false"
                                    Trim="true" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.TestPlatform.ObjectModel" Version="$TestPlatformVersion$" ExcludeAssets="runtime" />
    <PackageReference Include="Microsoft.TestPlatform.TestHost" Version="$TestPlatformVersion$" ExcludeAssets="build" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <Target Name="UseNuGetTestPlatformRuntime" BeforeTargets="SDKRedistOutputGroup">
    <ItemGroup>
      <ResolvedRedistFiles Remove="$(MSBuildProgramFiles32)\Microsoft SDKs\Windows Kits\10\ExtensionSDKs\TestPlatform.Universal\$(VisualStudioVersion)\Redist\**\*" />
    </ItemGroup>
  </Target>

  <Target Name="WriteResolvedMSTestAssets" AfterTargets="ResolveReferences">
    <WriteLinesToFile
      File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
      Lines="UseUwpTools=$(UseUwpTools)"
      Overwrite="true" />
    <WriteLinesToFile
      File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
      Lines="@(ReferencePath->'%(FullPath)')"
      Overwrite="false" />
  </Target>
</Project>

#file Properties/PublishProfiles/win-x64.pubxml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <PublishProtocol>FileSystem</PublishProtocol>
    <Platform>x64</Platform>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishDir>bin\$(Configuration)\$(TargetFramework)\$(RuntimeIdentifier)\publish\</PublishDir>
    <PublishAot>true</PublishAot>
    <SelfContained>true</SelfContained>
  </PropertyGroup>
</Project>

#file App.xaml
<?xml version="1.0" encoding="utf-8"?>
<Application
    x:Class="$AssetName$.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:$AssetName$" />

#file App.xaml.cs
using System;
using Microsoft.VisualStudio.TestPlatform.TestExecutor;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace $AssetName$;

public sealed partial class App : Application
{
    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var rootFrame = Window.Current.Content as Frame;
        if (rootFrame is null)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = rootFrame;
        }

        UnitTestClient.CreateDefaultUI();
        Window.Current.Activate();
        UnitTestClient.Run(args.Arguments);
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs args)
        => throw new InvalidOperationException($"Failed to load page '{args.SourcePageType.FullName}'.");
}

#file MainPage.xaml
<?xml version="1.0" encoding="utf-8"?>
<Page
    x:Class="$AssetName$.MainPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="using:$AssetName$"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"
    mc:Ignorable="d">
  <Grid>
    <TextBlock Text="MSTest Modern UWP acceptance asset" />
  </Grid>
</Page>

#file MainPage.xaml.cs
using Windows.UI.Xaml.Controls;

namespace $AssetName$;

public sealed partial class MainPage : Page
{
    public MainPage() => InitializeComponent();
}

#file Package.appxmanifest
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  IgnorableNamespaces="uap mp">
  <Identity Name="$PackageIdentityName$" Publisher="CN=MSTest" Version="1.0.0.0" />
  <mp:PhoneIdentity PhoneProductId="75f918cb-650b-479f-98d2-7f9f05b9a5a1" PhonePublisherId="00000000-0000-0000-0000-000000000000" />
  <Properties>
    <DisplayName>$AssetName$</DisplayName>
    <PublisherDisplayName>MSTest</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Universal" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="x-generate" />
  </Resources>
  <Applications>
    <Application Id="vstest.executionengine.universal.App" Executable="$targetnametoken$.exe" EntryPoint="$AssetName$.App">
      <uap:VisualElements
        DisplayName="$AssetName$"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png"
        Description="MSTest Modern UWP acceptance asset"
        BackgroundColor="transparent">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
        <uap:SplashScreen Image="Assets\SplashScreen.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <Capability Name="internetClientServer" />
    <Capability Name="privateNetworkClientServer" />
  </Capabilities>
</Package>

#file UnitTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.ApplicationModel;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace $AssetName$;

[TestClass]
public sealed class ModernUwpPackageTests
{
    [TestMethod]
    public void PlainTestMethod_RunsInPackagedUwp()
    {
        Assert.AreEqual("$PackageIdentityName$", Package.Current.Id.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Package.Current.Id.FamilyName));
    }

    [UITestMethod]
    public void UITestMethod_RunsOnCoreWindowDispatcher()
    {
        CoreWindow coreWindow = CoreWindow.GetForCurrentThread();
        Assert.IsNotNull(coreWindow);
        Assert.IsTrue(coreWindow.Dispatcher.HasThreadAccess);

        var grid = new Grid();
        Assert.IsNotNull(grid.Dispatcher);
        Assert.IsTrue(grid.Dispatcher.HasThreadAccess);
    }
}
""";
}
