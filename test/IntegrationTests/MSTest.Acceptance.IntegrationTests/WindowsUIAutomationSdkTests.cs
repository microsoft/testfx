// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsUIAutomationSdkTests : AcceptanceTestBase<WindowsUIAutomationSdkTests.TestAssetFixture>
{
    private static readonly string[] DesktopTargetFrameworks =
        TargetFrameworks.Net.Select(tfm => $"{tfm}-windows").ToArray();

    public static IEnumerable<object[]> DesktopTargetFrameworksForDynamicData =>
        DesktopTargetFrameworks.Select(tfm => new object[] { tfm });

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task EnableWindowsUIAutomation_WhenUsingMSTestRunner_RunsDesktopTests(string tfm)
    {
        string pidFile = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.pid");
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=CharacterMapTests",
            environmentVariables: new()
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSTEST_UI_AUTOMATION_PID_FILE"] = pidFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        await AssertProcessExitedAsync(pidFile);
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task EnableWindowsUIAutomation_WhenUsingVSTest_RunsDesktopTests(string tfm)
    {
        string pidFile = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.pid");
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build -c Release {AssetFixture.VSTestProjectPath} --framework {tfm}",
            workingDirectory: AssetFixture.VSTestProjectPath,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);
        buildResult.AssertExitCodeIs(0);

        DotnetMuxerResult dotnetTestResult = await DotnetCli.RunAsync(
            $"test -c Release {AssetFixture.VSTestProjectPath} --framework {tfm} --no-build --no-restore --filter ClassName=CharacterMapTests",
            workingDirectory: AssetFixture.VSTestProjectPath,
            environmentVariables: new()
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSTEST_UI_AUTOMATION_PID_FILE"] = pidFile,
            },
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            suppressPreviewDotNetMessage: false,
            cancellationToken: TestContext.CancellationToken);

        dotnetTestResult.AssertExitCodeIs(0);
        dotnetTestResult.AssertOutputContains("Test run for ");
        dotnetTestResult.AssertOutputContains("VSTestWindowsUIAutomation.dll");
        dotnetTestResult.AssertOutputContains("Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2");
        await AssertProcessExitedAsync(pidFile);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task EnableWindowsUIAutomation_WhenTargetFrameworkIsNotWindows_FailsWithClearError()
    {
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {AssetFixture.InvalidTargetFrameworkProjectPath}",
            workingDirectory: AssetFixture.ProjectPath,
            failIfReturnValueIsNotZero: false,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        buildResult.AssertExitCodeIs(1);
        buildResult.AssertOutputContains(
            "MSTest.Windows.UIAutomation requires a Windows target framework (e.g. net8.0-windows). Current TargetFramework: 'net8.0'.");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task EnableWindowsUIAutomation_WhenTargetFrameworkUsesUppercase_IsAccepted()
    {
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {AssetFixture.UppercaseTargetFrameworkProjectPath}",
            workingDirectory: AssetFixture.ProjectPath,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        buildResult.AssertExitCodeIs(0);
        buildResult.AssertOutputDoesNotContain("requires a Windows target framework");
    }

    [TestMethod]
    public async Task EnableWindowsUIAutomation_WhenCrossTargetingFromNonWindows_BuildsSuccessfully()
    {
        DotnetMuxerResult propertyResult = await DotnetCli.RunAsync(
            $"msbuild {AssetFixture.CrossTargetingProjectPath} -getProperty:EnableWindowsTargeting -p:OS=Unix",
            workingDirectory: AssetFixture.ProjectPath,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);
        propertyResult.AssertExitCodeIs(0);
        propertyResult.AssertOutputContains("true");

        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {AssetFixture.CrossTargetingProjectPath} -p:OS=Unix",
            workingDirectory: AssetFixture.ProjectPath,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        buildResult.AssertExitCodeIs(0);
    }

    [TestMethod]
    public async Task EnableWindowsUIAutomation_WhenWindowsTargetingIsExplicitlyDisabled_PreservesValue()
    {
        DotnetMuxerResult propertyResult = await DotnetCli.RunAsync(
            $"msbuild {AssetFixture.CrossTargetingOptOutProjectPath} -getProperty:EnableWindowsTargeting -p:OS=Unix",
            workingDirectory: AssetFixture.ProjectPath,
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);

        propertyResult.AssertExitCodeIs(0);
        propertyResult.AssertOutputContains("false");
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task WindowSetup_WhenApplicationExitsBeforeDiscovery_ReportsClearFailure(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=EarlyExitTests",
            environmentVariables: new() { ["DOTNET_ROLL_FORWARD"] = "Major" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("exited with code 0 before a window was discovered.");
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task ApplicationTearDown_WhenApplicationNeverExposesWindow_TerminatesProcess(string tfm)
    {
        string pidFile = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.pid");
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=StartupTimeoutTests",
            environmentVariables: new()
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSTEST_UI_AUTOMATION_PID_FILE"] = pidFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("did not expose a window within 00:00:03.");

        await AssertProcessExitedAsync(pidFile);
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task WindowTest_WhenWindowDiscoveryIsCustomized_UsesOverride(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=CustomWindowDiscoveryTests",
            environmentVariables: new() { ["DOTNET_ROLL_FORWARD"] = "Major" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task ApplicationTest_WhenShutdownIsCustomized_UsesOverride(string tfm)
    {
        string shutdownMarker = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.shutdown");
        string pidFile = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.pid");
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=CustomShutdownTests",
            environmentVariables: new()
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSTEST_UI_AUTOMATION_SHUTDOWN_MARKER"] = shutdownMarker,
                ["MSTEST_UI_AUTOMATION_PID_FILE"] = pidFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        Assert.IsTrue(File.Exists(shutdownMarker), "Expected the custom shutdown hook to create its marker file.");
        await AssertProcessExitedAsync(pidFile);
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task WindowSetup_WhenTestIsCanceled_StopsDiscoveryAndTerminatesProcess(string tfm)
    {
        string pidFile = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.pid");
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=CancellationDuringWindowDiscoveryTests",
            environmentVariables: new()
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSTEST_UI_AUTOMATION_PID_FILE"] = pidFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains(
            "Test initialize method 'Microsoft.VisualStudio.TestTools.UnitTesting.Windows.UIAutomation.WindowTest.WindowSetup' was canceled");
        await AssertProcessExitedAsync(pidFile);
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task ApplicationTest_WhenDerivedCleanupFails_StillTerminatesProcess(string tfm)
    {
        string pidFile = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.pid");
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=DerivedCleanupFailureTests",
            environmentVariables: new()
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSTEST_UI_AUTOMATION_PID_FILE"] = pidFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Derived cleanup failed.");
        await AssertProcessExitedAsync(pidFile);
    }

    [TestMethod]
    [DynamicData(nameof(DesktopTargetFrameworksForDynamicData))]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows UI Automation is Windows-only.")]
    public async Task WindowTest_WhenLauncherExitsBeforeChildWindow_DiscoversAndStopsChild(string tfm)
    {
        string childPidFile = Path.Combine(AssetFixture.ProjectPath, $"{Guid.NewGuid():N}.pid");
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter ClassName=LauncherChildWindowTests",
            environmentVariables: new()
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSTEST_UI_AUTOMATION_CHILD_PID_FILE"] = childPidFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        await AssertProcessExitedAsync(childPidFile);
    }

    private async Task AssertProcessExitedAsync(string pidFile)
    {
        int pid = int.Parse(await File.ReadAllTextAsync(pidFile, TestContext.CancellationToken), CultureInfo.InvariantCulture);
        try
        {
            using var process = Process.GetProcessById(pid);
            Assert.IsTrue(process.WaitForExit(1000), $"Expected process {pid} to be terminated by test cleanup.");
        }
        catch (ArgumentException)
        {
            // The process has already exited and been removed from the operating system's process table.
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "WindowsUIAutomationSdk";

        private const string SourceCode = """
#file WindowsUIAutomationSdk.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableWindowsUIAutomation>true</EnableWindowsUIAutomation>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$(MicrosoftNETTestSdkVersion)" />
  </ItemGroup>
</Project>

#file InvalidTargetFramework/InvalidTargetFramework.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableWindowsUIAutomation>true</EnableWindowsUIAutomation>
  </PropertyGroup>
</Project>

#file UppercaseTargetFramework/UppercaseTargetFramework.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>NET8.0-WINDOWS</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableWindowsUIAutomation>true</EnableWindowsUIAutomation>
  </PropertyGroup>
</Project>

#file CrossTargeting/CrossTargeting.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableWindowsUIAutomation>true</EnableWindowsUIAutomation>
  </PropertyGroup>
</Project>

#file CrossTargetingOptOut/CrossTargetingOptOut.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableWindowsUIAutomation>true</EnableWindowsUIAutomation>
    <EnableWindowsTargeting>false</EnableWindowsTargeting>
  </PropertyGroup>
</Project>

#file VSTest/VSTestWindowsUIAutomation.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseVSTest>true</UseVSTest>
    <EnableWindowsUIAutomation>true</EnableWindowsUIAutomation>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="..\CharacterMapTests.cs" Link="CharacterMapTests.cs" />
  </ItemGroup>
</Project>

#file CharacterMapTests.cs
using System.Diagnostics;
using System.Globalization;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[STATestClass]
public class CharacterMapTests : WindowTest
{
    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "charmap.exe"));

    [TestMethod]
    public void CharacterMap_MainWindow_IsVisible()
    {
        string pidFile = Environment.GetEnvironmentVariable("MSTEST_UI_AUTOMATION_PID_FILE")
            ?? throw new InvalidOperationException("MSTEST_UI_AUTOMATION_PID_FILE must be set.");
        File.WriteAllText(pidFile, AppProcess.Id.ToString(CultureInfo.InvariantCulture));

        Assert.AreEqual(ControlType.Window, MainWindow.Current.ControlType,
            "Expected the main window element to be of control type Window.");
    }

    [TestMethod]
    public void CharacterMap_MainWindow_HasExpectedTitle()
    {
        string title = MainWindow.Current.Name;
        Assert.IsFalse(string.IsNullOrEmpty(title), "Window title should not be empty.");
    }
}

[STATestClass]
public class EarlyExitTests : WindowTest
{
    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", "/c exit 0");

    [TestMethod]
    public void TestMethod()
    {
    }
}

[STATestClass]
public class StartupTimeoutTests : WindowTest
{
    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -Command \"$PID | Set-Content -LiteralPath $env:MSTEST_UI_AUTOMATION_PID_FILE; Start-Sleep -Seconds 30\"");

    protected override TimeSpan WindowDiscoveryTimeout => TimeSpan.FromSeconds(3);

    [TestMethod]
    public void TestMethod()
    {
    }
}

[STATestClass]
public class CustomWindowDiscoveryTests : WindowTest
{
    private int _findWindowInvocationCount;

    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "charmap.exe"));

    protected override AutomationElement? FindWindow(Process applicationProcess)
    {
        _findWindowInvocationCount++;
        return base.FindWindow(applicationProcess);
    }

    [TestMethod]
    public void FindWindowOverrideWasUsed()
        => Assert.IsGreaterThan(0, _findWindowInvocationCount);
}

[STATestClass]
public class CustomShutdownTests : ApplicationTest
{
    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\"");

    protected override void StopApplication(Process applicationProcess)
    {
        string marker = Environment.GetEnvironmentVariable("MSTEST_UI_AUTOMATION_SHUTDOWN_MARKER")
            ?? throw new InvalidOperationException("MSTEST_UI_AUTOMATION_SHUTDOWN_MARKER must be set.");
        string pidFile = Environment.GetEnvironmentVariable("MSTEST_UI_AUTOMATION_PID_FILE")
            ?? throw new InvalidOperationException("MSTEST_UI_AUTOMATION_PID_FILE must be set.");
        File.WriteAllText(pidFile, applicationProcess.Id.ToString(CultureInfo.InvariantCulture));
        File.WriteAllText(marker, string.Empty);
        base.StopApplication(applicationProcess);
    }

    [TestMethod]
    public void TestMethod()
    {
    }
}

[STATestClass]
public class CancellationDuringWindowDiscoveryTests : WindowTest
{
    private bool _cancellationScheduled;

    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -Command \"$PID | Set-Content -LiteralPath $env:MSTEST_UI_AUTOMATION_PID_FILE; Start-Sleep -Seconds 30\"");

    protected override AutomationElement? FindWindow(Process applicationProcess)
    {
        if (!_cancellationScheduled)
        {
            TestContext.CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
            _cancellationScheduled = true;
        }

        return null;
    }

    protected override TimeSpan WindowDiscoveryTimeout => TimeSpan.FromSeconds(30);

    [TestMethod]
    public void TestMethod()
    {
    }
}

[STATestClass]
public class DerivedCleanupFailureTests : ApplicationTest
{
    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\"");

    [TestMethod]
    public void TestMethod()
    {
        string pidFile = Environment.GetEnvironmentVariable("MSTEST_UI_AUTOMATION_PID_FILE")
            ?? throw new InvalidOperationException("MSTEST_UI_AUTOMATION_PID_FILE must be set.");
        File.WriteAllText(pidFile, AppProcess.Id.ToString(CultureInfo.InvariantCulture));
    }

    [TestCleanup]
    public void FailingCleanup()
        => throw new InvalidOperationException("Derived cleanup failed.");
}

[STATestClass]
public class LauncherChildWindowTests : WindowTest
{
    private Process? _childProcess;

    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -Command \"$process = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\\charmap.exe') -PassThru; $process.Id | Set-Content -LiteralPath $env:MSTEST_UI_AUTOMATION_CHILD_PID_FILE\"");

    protected override AutomationElement? FindWindow(Process applicationProcess)
    {
        _childProcess ??= TryGetChildProcess();
        return _childProcess is null ? null : base.FindWindow(_childProcess);
    }

    protected override void StopApplication(Process applicationProcess)
    {
        _childProcess ??= TryGetChildProcess();
        if (_childProcess is not null)
        {
            try
            {
                base.StopApplication(_childProcess);
            }
            finally
            {
                _childProcess.Dispose();
            }
        }

        base.StopApplication(applicationProcess);
    }

    [TestMethod]
    public void ChildWindowIsDiscovered()
        => Assert.AreEqual(ControlType.Window, MainWindow.Current.ControlType);

    private static Process? TryGetChildProcess()
    {
        string pidFile = Environment.GetEnvironmentVariable("MSTEST_UI_AUTOMATION_CHILD_PID_FILE")
            ?? throw new InvalidOperationException("MSTEST_UI_AUTOMATION_CHILD_PID_FILE must be set.");
        if (!File.Exists(pidFile)
            || !int.TryParse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture, out int pid))
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return null;
        }
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

        public string InvalidTargetFrameworkProjectPath =>
            Path.Combine(ProjectPath, "InvalidTargetFramework", "InvalidTargetFramework.csproj");

        public string UppercaseTargetFrameworkProjectPath =>
            Path.Combine(ProjectPath, "UppercaseTargetFramework", "UppercaseTargetFramework.csproj");

        public string CrossTargetingProjectPath =>
            Path.Combine(ProjectPath, "CrossTargeting", "CrossTargeting.csproj");

        public string CrossTargetingOptOutProjectPath =>
            Path.Combine(ProjectPath, "CrossTargetingOptOut", "CrossTargetingOptOut.csproj");

        public string VSTestProjectPath => Path.Combine(ProjectPath, "VSTest");

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(DesktopTargetFrameworks)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
    }
}
