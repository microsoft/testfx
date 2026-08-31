// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Exercises the locally packed MSTest packages from a real classic UWP application.
/// </summary>
[TestClass]
[TestCategory("WindowsApplicationModel")]
[MemberCondition(
    typeof(AcceptanceTestBase),
    nameof(AcceptanceTestBase.IsWindowsApplicationModelTestEnvironment),
    IgnoreMessage = "Requires the dedicated preflight-gated Windows application-model test environment.")]
[DoNotParallelize]
public sealed class ClassicUwpTests : AcceptanceTestBase
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Classic UWP execution is supported only on Windows.")]
    public async Task ClassicUwp_ConsumesUapAssets_AndRunsPlainAndUiTestsThroughVSTest()
    {
        string uniqueSuffix = Guid.NewGuid().ToString("N");
        string assetName = $"ClassicUwp{uniqueSuffix[..12]}";
        string packageIdentityName = $"MSTestClassicUwp{uniqueSuffix}";
        string sourceCode = ClassicUwpSourceCode
            .PatchCodeWithReplace("$AssetName$", assetName)
            .PatchCodeWithReplace("$PackageIdentityName$", packageIdentityName)
            .PatchCodeWithReplace("$MSBuildSdkExtrasVersion$", MSBuildSdkExtrasVersion)
            .PatchCodeWithReplace("$MicrosoftNETCoreUniversalWindowsPlatformVersion$", MicrosoftNETCoreUniversalWindowsPlatformVersion)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

        TestAsset testAsset = await TestAsset.GenerateAssetAsync(assetName, sourceCode);
        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            testAsset,
            packageIdentityName,
            async () =>
            {
                WindowsApplicationModelTestTools.CopySampleAssets(testAsset.TargetAssetPath);
                await EnsureGeneratedCSharpFilesHaveUtf8BomAsync(testAsset.TargetAssetPath, TestContext.CancellationToken);

                VisualStudioTestTools tools = await WindowsApplicationModelTestTools.LocateClassicUwpVisualStudioToolsAsync(TestContext.CancellationToken);
                UwpBuildResult build = await WindowsApplicationModelTestTools.BuildUwpAssetAsync(
                    tools,
                    testAsset,
                    $"{assetName}.csproj",
                    TestContext.CancellationToken);

                string resolvedAssetsReport = Path.Combine(testAsset.TargetAssetPath, "resolved-mstest-assets.txt");
                WindowsApplicationModelTestTools.AssertResolvedMSTestAssets(
                    resolvedAssetsReport,
                    WindowsApplicationModelAssetKind.ClassicUwp);
                AssertClassicUwpPackageLayout(build, packageIdentityName);

                string resultsDirectory = Path.Combine(testAsset.TargetAssetPath, "TestResults");
                UwpRunResult run = await WindowsApplicationModelTestTools.RunUwpRecipeAsync(
                    tools,
                    build.RecipePath,
                    resultsDirectory,
                    TestContext.CancellationToken);

                Assert.AreEqual(
                    0,
                    run.ExitCode,
                    $"VSTest failed to execute the real classic UWP recipe '{build.RecipePath}'. TRX: '{run.TrxPath}'. " +
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

                AssertClassicUwpTrx(run.TrxPath, build);
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

    private static void AssertClassicUwpPackageLayout(UwpBuildResult build, string expectedPackageIdentityName)
    {
        Assert.IsTrue(
            Directory.Exists(build.PackageLayoutPath),
            $"The classic UWP package layout '{build.PackageLayoutPath}' does not exist. Binlog: '{build.BinlogPath}'.");

        string manifestPath = Path.Combine(build.PackageLayoutPath, "AppxManifest.xml");
        Assert.IsTrue(
            File.Exists(manifestPath),
            $"The classic UWP package layout does not contain the tooling-generated manifest '{manifestPath}'. " +
            $"Recipe: '{build.RecipePath}'. Binlog: '{build.BinlogPath}'.");

        XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        var manifest = XDocument.Load(manifestPath);
        XElement identity = manifest.Root?.Element(foundation + "Identity")
            ?? throw new AssertFailedException($"The tooling-generated package manifest '{manifestPath}' has no Identity element.");
        Assert.AreEqual(
            expectedPackageIdentityName,
            (string?)identity.Attribute("Name"),
            $"Unexpected package identity in the tooling-generated manifest '{manifestPath}'.");
    }

    private static void AssertClassicUwpTrx(string trxPath, UwpBuildResult build)
    {
        Assert.IsTrue(
            File.Exists(trxPath),
            $"VSTest did not create the expected TRX '{trxPath}' for recipe '{build.RecipePath}'. Binlog: '{build.BinlogPath}'.");

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var trx = XDocument.Load(trxPath);
        XElement[] results = [.. trx.Descendants(ns + "UnitTestResult")];
        Assert.HasCount(2, results, $"Expected exactly the plain and UI classic UWP tests in '{trxPath}'.");

        string[] actualTestNames = results
            .Select(result => (string?)result.Attribute("testName"))
            .Where(name => name is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expectedTestNames =
        [
            "PlainTestMethod_RunsInClassicUwpPackage",
            "UITestMethod_RunsOnCoreWindowDispatcher",
        ];
        CollectionAssert.AreEqual(expectedTestNames, actualTestNames, $"Unexpected test results were written to '{trxPath}'.");
        Assert.IsTrue(
            results.All(result => (string?)result.Attribute("outcome") == "Passed"),
            $"Every classic UWP result must pass. TRX: '{trxPath}'.");

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

    private const string ClassicUwpSourceCode = """
#file $AssetName$.csproj
<Project Sdk="MSBuild.Sdk.Extras/$MSBuildSdkExtrasVersion$">
  <PropertyGroup>
    <TargetFramework>uap10.0.16299</TargetFramework>
    <TargetPlatformVersion>10.0.16299.0</TargetPlatformVersion>
    <TargetPlatformMinVersion>10.0.16299.0</TargetPlatformMinVersion>
    <OutputType>AppContainerExe</OutputType>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <DefaultLanguage>en-US</DefaultLanguage>
    <UseDotNetNativeToolchain>false</UseDotNetNativeToolchain>
    <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
  </PropertyGroup>

  <ItemGroup>
    <ProjectCapability Include="TestContainer" />
    <SDKReference Include="TestPlatform.Universal, Version=$(VisualStudioVersion)" />
    <ApplicationDefinition Include="App.xaml">
      <Generator>MSBuild:Compile</Generator>
      <SubType>Designer</SubType>
    </ApplicationDefinition>
    <Compile Update="App.xaml.cs">
      <DependentUpon>App.xaml</DependentUpon>
    </Compile>
    <AppxManifest Include="Package.appxmanifest" />
    <Content Include="Assets\**\*" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NETCore.UniversalWindowsPlatform" Version="$MicrosoftNETCoreUniversalWindowsPlatformVersion$" />
    <PackageReference Include="Newtonsoft.Json" Version="9.0.1" GeneratePathProperty="true">
      <NoWarn>NU1903</NoWarn>
    </PackageReference>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" GeneratePathProperty="true" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="$(PkgNewtonsoft_Json)\lib\portable-net45+wp80+win8+wpa81\Newtonsoft.Json.dll"
             Link="Newtonsoft.Json.dll" />
    <Content Include="$(PkgMSTest_TestFramework)\lib\uap10.0\MSTest.TestFramework.dll"
             Link="MSTest.TestFramework.dll" />
    <Content Include="$(PkgMSTest_TestFramework)\lib\uap10.0\MSTest.TestFramework.Extensions.dll"
             Link="MSTest.TestFramework.Extensions.dll" />
  </ItemGroup>

  <Target Name="WriteResolvedMSTestAssets" AfterTargets="ResolveReferences">
    <WriteLinesToFile
      File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
      Lines="TargetFramework=$(TargetFramework);TargetPlatformVersion=$(TargetPlatformVersion);MSBuildRuntimeType=$(MSBuildRuntimeType)"
      Overwrite="true" />
    <WriteLinesToFile
      File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
      Lines="@(ReferencePath->'%(FullPath)')"
      Overwrite="false" />
  </Target>
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

namespace $AssetName$
{
    sealed partial class App : Application
    {
        public App() => InitializeComponent();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
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
        {
            throw new InvalidOperationException("Failed to load page '" + args.SourcePageType.FullName + "'.");
        }
    }
}

#file Package.appxmanifest
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  IgnorableNamespaces="uap mp">
  <Identity Name="$PackageIdentityName$" Publisher="CN=MSTest" Version="1.0.0.0" />
  <mp:PhoneIdentity PhoneProductId="8c009b7d-f28c-4f09-8e97-0669b8aac67d" PhonePublisherId="00000000-0000-0000-0000-000000000000" />
  <Properties>
    <DisplayName>$AssetName$</DisplayName>
    <PublisherDisplayName>MSTest</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Universal" MinVersion="10.0.16299.0" MaxVersionTested="10.0.16299.0" />
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
        Description="MSTest classic UWP acceptance asset"
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

namespace $AssetName$
{
    [TestClass]
    public sealed class ClassicUwpPackageTests
    {
        [TestMethod]
        public void PlainTestMethod_RunsInClassicUwpPackage()
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
}
""";
}
