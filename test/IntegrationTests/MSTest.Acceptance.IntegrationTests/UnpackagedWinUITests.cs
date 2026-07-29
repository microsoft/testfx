// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for testing an <b>unpackaged</b> WinUI 3 app
/// (<c>&lt;WindowsPackageType&gt;None&lt;/WindowsPackageType&gt;</c>) on Microsoft.Testing.Platform — the
/// scenario tracked by <see href="https://github.com/microsoft/testfx/issues/2784"/>. Unlike
/// <see cref="WinUITests"/>, which only checks that the WinUI-flavored MSTest assemblies load, these
/// assets reference the real <c>Microsoft.WindowsAppSDK</c>, so the produced apps are actual WinUI apps
/// with no package identity.
/// </summary>
/// <remarks>
/// <para>
/// The two hosting shapes are covered separately because they take different code paths:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <b>Self-hosted</b> — the WinUI app is itself the test host and has already called
///     <c>Application.Start</c>, so it publishes its dispatcher through
///     <c>UITestMethodAttribute.DispatcherQueue</c>. <c>WinUITestTarget</c> must not be used here: it
///     would make the attribute call <c>Application.Start</c> a second time in the same process.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>WinUITestTarget</b> — the test host is an ordinary executable and the attribute starts the
///     application itself. This is the path that owns application startup, and hence the one that can
///     fail while bringing it up.
///     </description>
///   </item>
/// </list>
/// <para>
/// Both assets are built with the dotnet muxer like every other acceptance asset, so these tests run on
/// any Windows leg. (Older Windows App SDK releases needed the MrtCore PRI MSBuild tasks that ship with
/// Visual Studio; the version pinned below builds fine without them.)
/// </para>
/// <para>
/// Every run is bounded by an explicit timeout, because the failure mode
/// <see cref="WinUITestTarget_WhenApplicationConstructorThrows_FailsInsteadOfHanging"/> guards against is
/// a <em>hang</em>, which would otherwise stall the job instead of failing it.
/// </para>
/// </remarks>
[TestClass]
[OSCondition(OperatingSystems.Windows, IgnoreMessage = "WinUI is Windows-only.")]
public sealed class UnpackagedWinUITests : AcceptanceTestBase<NopAssetFixture>
{
    private const string TargetFramework = "net8.0-windows10.0.19041.0";

    // The Windows App SDK version the assets build against, kept in one place so both assets stay in sync.
    // Must stay at or above the release that builds without the Visual Studio MrtCore PRI tasks.
    private const string WindowsAppSdkVersion = "1.8.251106002";
    private const string WindowsSdkBuildToolsVersion = "10.0.26100.7175";

    private const string ThrowingConstructorMessage = "Application constructor failed on purpose";

    // Bounds every child process so a regression of the deadlock fix in UITestMethodAttribute surfaces as
    // a test failure instead of stalling the whole job.
    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromMinutes(5);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SelfHostedUnpackagedWinUI_RunsTests_AndProcessExits()
    {
        const string AssetName = "SelfHostedWinUITests";

        using TestAsset testAsset = await GenerateAssetAsync(AssetName, SelfHostedSourceCode);
        string testHostDirectory = await BuildAssetAsync(AssetName, testAsset);
        var testHost = TestHost.LocateFrom(testHostDirectory, AssetName);

        using CancellationTokenSource cancellation = CreateTimeoutCancellation();
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: cancellation.Token);

        // Passing all three proves in a single run that:
        //  - the app has no package identity yet still hosts the platform (unpackaged works at all),
        //  - a plain [TestMethod] can call a Windows App SDK WinRT API, so the bootstrapper ran, and
        //  - a [UITestMethod] reaches the UI thread through UITestMethodAttribute.DispatcherQueue.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);

        // Getting a result at all means the process exited rather than being killed by the timeout:
        // Application.Start pumps a message loop that never returns, so the UI thread has to stay a
        // background thread for the run to terminate (see https://github.com/microsoft/testfx/pull/9904).
    }

    [TestMethod]
    public async Task WinUITestTarget_WhenApplicationConstructorThrows_FailsInsteadOfHanging()
    {
        const string AssetName = "WinUITestTargetFailureTests";

        // Regression test for the deadlock in UITestMethodAttribute.InitializeApplication: a failure while
        // bringing the application up used to be swallowed, leaving the TaskCompletionSource incomplete so
        // the blocking wait never returned and the run hung with no diagnostic at all. The same swallow hid
        // a failed Windows App SDK bootstrap in an unpackaged app; a throwing constructor is the
        // deterministic way to reach that catch block.
        using TestAsset testAsset = await GenerateAssetAsync(AssetName, WinUITestTargetSourceCode);
        string testHostDirectory = await BuildAssetAsync(AssetName, testAsset);
        var testHost = TestHost.LocateFrom(testHostDirectory, AssetName);

        using CancellationTokenSource cancellation = CreateTimeoutCancellation();
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: cancellation.Token);

        // The run has to finish and report a failed test rather than block forever, and the original
        // exception message has to reach the output so the cause is diagnosable. Asserting the message
        // (not just the exit code) is what proves the reflection wrapper is unwrapped: without that the
        // user would only see "Exception has been thrown by the target of an invocation".
        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains(ThrowingConstructorMessage);
    }

    private CancellationTokenSource CreateTimeoutCancellation()
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(ExecutionTimeout);
        return cancellation;
    }

    private static Task<TestAsset> GenerateAssetAsync(string assetName, string sourceCode)
        => TestAsset.GenerateAssetAsync(
            assetName,
            sourceCode
                .PatchCodeWithReplace("$AssetName$", assetName)
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$WindowsAppSdkVersion$", WindowsAppSdkVersion)
                .PatchCodeWithReplace("$WindowsSdkBuildToolsVersion$", WindowsSdkBuildToolsVersion)
                .PatchCodeWithReplace("$ThrowingConstructorMessage$", ThrowingConstructorMessage));

    /// <summary>
    /// Builds the asset and returns the directory holding the produced executable.
    /// </summary>
    private async Task<string> BuildAssetAsync(string assetName, TestAsset testAsset)
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;

        string project = Path.Combine(testAsset.TargetAssetPath, $"{assetName}.csproj");
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build \"{project}\" -c Release",
            workingDirectory: testAsset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            // MrtCore's PRI indexer warns that MSTest's localized satellite assemblies carry no en-US
            // default. It logs the PRI257/PRI263 codes inside the message text rather than as MSBuild
            // warning codes, so neither NoWarn nor MSBuildWarningsAsMessages can target them (both were
            // tried). The warning is benign for a test app and outside this repository's control, so drop
            // -warnaserror for this build.
            warnAsError: false,
            cancellationToken: cancellationToken);
        Assert.AreEqual(0, buildResult.ExitCode, $"Building the unpackaged WinUI asset failed.{Environment.NewLine}{buildResult}");

        string testHostDirectory = Path.Combine(testAsset.TargetAssetPath, "bin", "Release", TargetFramework);
        Assert.IsTrue(
            Directory.Exists(testHostDirectory),
            $"Expected the built unpackaged WinUI app under '{testHostDirectory}'.");

        return testHostDirectory;
    }

    /// <summary>
    /// The self-hosted shape: the WinUI app owns the entry point, starts the platform from
    /// <c>OnLaunched</c>, and publishes its own dispatcher queue for <c>[UITestMethod]</c>.
    /// </summary>
    private const string SelfHostedSourceCode = """
#file $AssetName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <UseWinUI>true</UseWinUI>
    <Nullable>enable</Nullable>

    <!-- The setting under test: run without MSIX package identity. -->
    <WindowsPackageType>None</WindowsPackageType>
    <EnableMsixTooling>false</EnableMsixTooling>

    <EnableMSTestRunner>true</EnableMSTestRunner>

    <!-- The WinUI app owns its entry point (generated from the ApplicationDefinition below). -->
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
  </PropertyGroup>

  <ItemGroup>
    <Page Remove="UnitTestApp.xaml" />
    <ApplicationDefinition Include="UnitTestApp.xaml" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="$WindowsSdkBuildToolsVersion$" />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="$WindowsAppSdkVersion$" />
    <!--
      MSTest.TestAdapter + MSTest.TestFramework rather than the MSTest metapackage: the metapackage also
      pulls Microsoft.Testing.Extensions.CodeCoverage, whose Microsoft.Testing.Platform floor is above the
      locally built '-dev' platform package, so restore would not resolve against the local packages.
    -->
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTestApp.xaml
<?xml version="1.0" encoding="utf-8"?>
<Application
    x:Class="$AssetName$.UnitTestApp"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:$AssetName$">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>

#file UnitTestApp.xaml.cs
using System;
using System.Linq;
using Microsoft.Testing.Platform.Builder;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace $AssetName$;

public partial class UnitTestApp : Application
{
    private Window? _window;

    public UnitTestApp() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window();
        _window.Activate();

        // This process has already called Application.Start, so [UITestMethod] must reuse this
        // dispatcher. Using WinUITestTarget here instead would start a second application.
        UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

        try
        {
            string[] cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(cliArgs);
            builder.AddSelfRegisteredExtensions(cliArgs);
            using ITestApplication app = await builder.BuildAsync();
            Environment.ExitCode = await app.RunAsync();
        }
        finally
        {
            // The app is the test host, so it has to shut itself down once the run is over.
            _window.Close();
            Exit();
        }
    }
}

#file UnitTest1.cs
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace $AssetName$;

[TestClass]
public class TestClass1
{
    [TestMethod]
    public void PlainTestMethod_Runs()
    {
    }

    [TestMethod]
    public void PlainTestMethod_CanCallWindowsAppSdkWinRT()
    {
        // Resolving a Windows App SDK WinRT API in a process with no package identity only works once the
        // bootstrapper has run. Without it this throws COMException (REGDB_E_CLASSNOTREG).
        string runtimeInfo = Microsoft.Windows.ApplicationModel.WindowsAppRuntime.RuntimeInfo.AsString;

        Assert.IsFalse(string.IsNullOrEmpty(runtimeInfo));
    }

    [UITestMethod]
    public void UITestMethod_RunsOnUIThread()
    {
        var grid = new Grid();

        Assert.AreEqual(0, grid.MinWidth);
    }
}
""";

    /// <summary>
    /// The WinUITestTarget shape: an ordinary test executable (the platform generates its entry point)
    /// that points <c>[UITestMethod]</c> at an <c>Application</c> the attribute has to start itself. The
    /// application's constructor throws, which is the deterministic way to exercise the failure path in
    /// <c>UITestMethodAttribute.InitializeApplication</c>.
    /// </summary>
    private const string WinUITestTargetSourceCode = """
#file $AssetName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <UseWinUI>true</UseWinUI>
    <Nullable>enable</Nullable>

    <!-- The setting under test: run without MSIX package identity. -->
    <WindowsPackageType>None</WindowsPackageType>
    <EnableMsixTooling>false</EnableMsixTooling>

    <!-- An ordinary test executable: the platform generates the entry point. -->
    <EnableMSTestRunner>true</EnableMSTestRunner>

  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="$WindowsSdkBuildToolsVersion$" />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="$WindowsAppSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

// UITestMethodAttribute.DispatcherQueue is deliberately left unset so the attribute goes through
// WinUITestTarget and starts the application itself.
[assembly: WinUITestTarget(typeof($AssetName$.ThrowingApp))]

namespace $AssetName$;

public class ThrowingApp : Application
{
    public ThrowingApp()
        => throw new System.InvalidOperationException("$ThrowingConstructorMessage$");
}

[TestClass]
public class TestClass1
{
    [UITestMethod]
    public void UITestMethod_CannotStartApplication()
    {
    }
}
""";
}
