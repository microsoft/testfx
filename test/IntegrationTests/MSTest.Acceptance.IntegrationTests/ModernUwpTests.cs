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
    private const string Net10TargetFramework = "net10.0-windows10.0.26100.0";
    private const string Net11TargetFramework = "net11.0-windows10.0.26100.0";

    [DataRow("x86")]
    [DataRow("ARM64")]
    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Modern UWP builds are supported only on Windows.")]
    public async Task ModernUwp_BuildsMtpPackageForArchitecture(string platform)
    {
        string uniqueSuffix = Guid.NewGuid().ToString("N");
        string assetName = $"ModernUwp{uniqueSuffix[..12]}";
        string sourceCode = ModernUwpSourceCode
            .PatchCodeWithReplace("$AssetName$", assetName)
            .PatchCodeWithReplace("$PackageIdentityName$", $"MSTestModernUwp{uniqueSuffix}")
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", Net10TargetFramework);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(assetName, sourceCode);
        WindowsApplicationModelTestTools.CopySampleAssets(testAsset.TargetAssetPath);
        await EnsureGeneratedCSharpFilesHaveUtf8BomAsync(testAsset.TargetAssetPath, TestContext.CancellationToken);
        VisualStudioTestTools tools = await WindowsApplicationModelTestTools.LocateModernUwpVisualStudioToolsAsync(TestContext.CancellationToken);

        UwpBuildResult build = await WindowsApplicationModelTestTools.BuildUwpAssetAsync(
            tools,
            testAsset,
            $"{assetName}.csproj",
            TestContext.CancellationToken,
            platform: platform);

        Assert.IsTrue(Directory.Exists(build.PackageLayoutPath), build.BinlogPath);
        WindowsApplicationModelTestTools.AssertResolvedMSTestAssets(
            Path.Combine(testAsset.TargetAssetPath, "resolved-mstest-assets.txt"),
            WindowsApplicationModelAssetKind.ModernUwp,
            Net10TargetFramework);
    }

    [DataRow(Net10TargetFramework, false, DisplayName = "ModernUwp_Net10")]
    [DataRow(Net11TargetFramework, false, DisplayName = "ModernUwp_Net11")]
    [DataRow(Net10TargetFramework, true, DisplayName = "ModernUwp_UnsupportedDeployment")]
    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Modern UWP execution is supported only on Windows.")]
    public async Task ModernUwp_ConsumesUwpAssets_AndRunsPlainAndUiTestsThroughMtp(string targetFramework, bool unsupportedDeployment)
    {
        string uniqueSuffix = Guid.NewGuid().ToString("N");
        string assetName = $"ModernUwp{uniqueSuffix[..12]}";
        string packageIdentityName = $"MSTestModernUwp{uniqueSuffix}";
        string sourceCode = ModernUwpSourceCode
            .PatchCodeWithReplace("$AssetName$", assetName)
            .PatchCodeWithReplace("$PackageIdentityName$", packageIdentityName)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", targetFramework);
        if (unsupportedDeployment)
        {
            sourceCode += UnsupportedDeploymentSource.PatchCodeWithReplace("$AssetName$", assetName);
        }

        TestAsset testAsset = await TestAsset.GenerateAssetAsync(assetName, sourceCode);
        string executionDirectory = Path.Combine(Path.GetTempPath(), nameof(ModernUwpTests), uniqueSuffix);
        bool cleanupSucceeded = false;
        try
        {
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
                        WindowsApplicationModelAssetKind.ModernUwp,
                        targetFramework);
                    Assert.IsTrue(
                        Directory.Exists(build.PackageLayoutPath),
                        $"The Modern UWP package layout '{build.PackageLayoutPath}' does not exist. Binlog: '{build.BinlogPath}'.");

                    // AppX resolves subst drives and junctions to their physical paths, so stage only the recipe
                    // and requested executable in a short physical directory before registration.
                    Directory.CreateDirectory(executionDirectory);
                    string stagedTargetPath = Path.Combine(executionDirectory, $"{assetName}.exe");
                    File.Copy(Path.Combine(build.PackageLayoutPath, $"{assetName}.exe"), stagedTargetPath);
                    File.Copy(
                        Path.Combine(build.PackageLayoutPath, "AppxManifest.xml"),
                        Path.Combine(executionDirectory, "AppxManifest.xml"));
                    File.Copy(build.RecipePath, Path.Combine(executionDirectory, Path.GetFileName(build.RecipePath)));

                    string resultsDirectory = Path.Combine(testAsset.TargetAssetPath, "TestResults");
                    UwpRunResult run = await WindowsApplicationModelTestTools.RunMtpUwpProjectAsync(
                        tools,
                        build.ProjectPath,
                        resultsDirectory,
                        TestContext.CancellationToken,
                        testingPlatformPackagedAppTargetPath: stagedTargetPath);
                    string startupError = run.ExitCode == 0
                        ? string.Empty
                        : await WindowsApplicationModelTestTools.TryReadPackageLocalStateFileAsync(
                            packageIdentityName,
                            "mtp-startup-error.txt",
                            TestContext.CancellationToken);
                    string hostDiagnostics = run.ExitCode == 0
                        ? string.Empty
                        : await WindowsApplicationModelTestTools.TryReadPackageLocalStateDiagnosticsAsync(
                            packageIdentityName,
                            TestContext.CancellationToken);

                    Assert.AreEqual(
                        unsupportedDeployment ? 1 : 0,
                        run.ExitCode,
                        $"MTP failed to execute the real Modern UWP project '{build.ProjectPath}'. TRX: '{run.TrxPath}'. " +
                        $"Binlog: '{build.BinlogPath}'.{Environment.NewLine}Standard output:{Environment.NewLine}{run.StandardOutput}" +
                        $"{Environment.NewLine}Standard error:{Environment.NewLine}{run.ErrorOutput}" +
                        $"{Environment.NewLine}Activated host startup error:{Environment.NewLine}{startupError}" +
                        $"{Environment.NewLine}Activated host diagnostics:{Environment.NewLine}{hostDiagnostics}");
                    Assert.DoesNotContain(
                        "No test is available",
                        run.StandardOutput,
                        $"MTP reported no discovered tests for '{build.ProjectPath}'.");
                    Assert.DoesNotContain(
                        "Total tests: 0",
                        run.StandardOutput,
                        $"MTP reported zero discovered tests for '{build.ProjectPath}'.");

                    AssertModernUwpTrx(run.TrxPath, build, unsupportedDeployment);
                });
            cleanupSucceeded = true;
        }
        finally
        {
            if (cleanupSucceeded && Directory.Exists(executionDirectory))
            {
                Directory.Delete(executionDirectory, recursive: true);
            }
        }
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
            Assert.AreSequenceEqual(utf8Preamble, actualPreamble, $"Generated C# file '{path}' is not UTF-8 with BOM.");
        }
    }

    private static void AssertModernUwpTrx(string trxPath, UwpBuildResult build, bool unsupportedDeployment)
    {
        Assert.IsTrue(
            File.Exists(trxPath),
            $"MTP did not create the expected TRX '{trxPath}' for project '{build.ProjectPath}'. Binlog: '{build.BinlogPath}'.");

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var trx = XDocument.Load(trxPath);
        XElement[] results = [.. trx.Descendants(ns + "UnitTestResult")];
        int failures = unsupportedDeployment ? 2 : 0;
        string[] expectedResults = unsupportedDeployment
            ?
            [
                "InheritedClassDeployment:Failed",
                "InheritedMethodDeployment:Failed",
                "PlainTestMethod_RunsInPackagedUwp:Passed",
                "UITestMethod_RunsOnCoreWindowDispatcher:Passed",
            ]
            :
        [
            "PlainTestMethod_RunsInPackagedUwp:Passed",
            "UITestMethod_RunsOnCoreWindowDispatcher:Passed",
        ];
        Assert.AreSequenceEqual(
            expectedResults,
            results.Select(result => $"{result.Attribute("testName")!.Value}:{result.Attribute("outcome")!.Value}").Order(StringComparer.Ordinal),
            $"Unexpected Modern UWP identities/outcomes in '{trxPath}'.");
        foreach (XElement result in results.Where(result => (string?)result.Attribute("outcome") == "Failed"))
        {
            Assert.Contains(
                "DeploymentItemAttribute is not supported by UWP or WinUI test hosts.",
                result.Descendants(ns + "Message").Single().Value);
            Assert.DoesNotContain("DEPLOYMENT_BODY_EXECUTED", result.Value);
        }

        XElement counters = trx.Descendants(ns + "Counters").Single();
        AssertCounter(counters, "total", 2 + failures, trxPath);
        AssertCounter(counters, "executed", 2 + failures, trxPath);
        AssertCounter(counters, "passed", 2, trxPath);
        AssertCounter(counters, "failed", failures, trxPath);
        AssertCounter(counters, "error", 0, trxPath);
        AssertCounter(counters, "inconclusive", 0, trxPath);
        AssertCounter(counters, "notExecuted", 0, trxPath);
    }

    private static void AssertCounter(XElement counters, string name, int expected, string trxPath)
        => Assert.AreEqual(
            expected.ToString(CultureInfo.InvariantCulture),
            (string?)counters.Attribute(name),
            $"Unexpected '{name}' counter in '{trxPath}'.");

    private const string UnsupportedDeploymentSource = """

        #file UnsupportedDeploymentTests.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace $AssetName$;

        public abstract class MethodDeploymentBase
        {
            [TestMethod]
            [DeploymentItem("not-deployed.txt")]
            public void InheritedMethodDeployment() => Assert.Fail("DEPLOYMENT_BODY_EXECUTED");
        }

        [TestClass]
        public sealed class InheritedMethodDeploymentTests : MethodDeploymentBase;

        [DeploymentItem("not-deployed.txt")]
        public abstract class ClassDeploymentBase;

        [TestClass]
        public sealed class InheritedClassDeploymentTests : ClassDeploymentBase
        {
            [TestMethod]
            public void InheritedClassDeployment() => Assert.Fail("DEPLOYMENT_BODY_EXECUTED");
        }
        """;

    private const string ModernUwpSourceCode = """
#file $AssetName$.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>$TargetFramework$</TargetFramework>
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
    <MicrosoftTestingPlatformVersion>$MicrosoftTestingPlatformVersion$</MicrosoftTestingPlatformVersion>
    <MicrosoftTestingExtensionsCommonVersion>$MicrosoftTestingPlatformVersion$</MicrosoftTestingExtensionsCommonVersion>
    <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
  </PropertyGroup>

  <Target Name="WriteResolvedMSTestAssets" AfterTargets="ResolveReferences">
    <WriteLinesToFile
      File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
      Lines="TargetFramework=$(TargetFramework);UseUwpTools=$(UseUwpTools)"
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
using Microsoft.Testing.Extensions;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace $AssetName$;

public sealed partial class App : Application
{
    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var rootFrame = Window.Current.Content as Frame;
        if (rootFrame is null)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            Window.Current.Content = rootFrame;
        }

        Window.Current.Activate();
        try
        {
            string[] testArguments = PackagedAppExtensions.GetTestApplicationArguments(args.Arguments);
            Environment.SetEnvironmentVariable(
                "platformOptions__testHostControllersManager__namedPipeClient__connectTimeoutSeconds",
                "5");
            Environment.ExitCode = await MicrosoftTestingPlatformApplication.RunAsync(
                testArguments);
        }
        catch (Exception ex)
        {
            StorageFile errorFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                "mtp-startup-error.txt",
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(errorFile, ex.ToString());
            Environment.ExitCode = 1;
        }
        finally
        {
            Exit();
        }
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
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PlainTestMethod_RunsInPackagedUwp()
    {
        Assert.AreEqual("$PackageIdentityName$", Package.Current.Id.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Package.Current.Id.FamilyName));
        string[] directories = ["TestRunDirectory", "DeploymentDirectory", "ResultsDirectory", "TestRunResultsDirectory", "TestResultsDirectory", "TestTempDirectory"];
        foreach (string directory in directories)
        {
            TestContext.Properties[directory] = "not-a-ui-directory";
        }

        Assert.IsNull(TestContext.TestRunDirectory);
        Assert.IsNull(TestContext.DeploymentDirectory);
        Assert.IsNull(TestContext.ResultsDirectory);
        Assert.IsNull(TestContext.TestRunResultsDirectory);
        Assert.IsNull(TestContext.TestResultsDirectory);
        Assert.IsNull(TestContext.TestTempDirectory);
        TestContext.WriteLine(typeof(TestContext).Assembly.Location);
        TestContext.WriteLine(typeof(UITestMethodAttribute).Assembly.Location);
        TestContext.WriteLine(TestContext.GetType().Assembly.Location);
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
