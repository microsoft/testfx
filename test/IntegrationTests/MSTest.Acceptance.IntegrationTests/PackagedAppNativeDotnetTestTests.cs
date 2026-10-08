// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

<<<<<<< HEAD
using System.Text.Json;

=======
>>>>>>> Fix native dotnet test routing for packaged applications
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[TestCategory("WindowsApplicationModel")]
[MemberCondition(
    typeof(AcceptanceTestBase),
    nameof(AcceptanceTestBase.IsWindowsApplicationModelTestEnvironment),
    IgnoreMessage = "Requires the dedicated preflight-gated Windows application-model test environment.")]
[OSCondition(OperatingSystems.Windows)]
[SupportedOSPlatform("windows")]
public sealed class PackagedAppNativeDotnetTestTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string SdkVersion = "10.0.401";
    private const string ExecutionIdEnvironmentVariable = "TESTINGPLATFORM_DOTNETTEST_EXECUTIONID";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("net8.0", false, false, "Passed")]
    [DataRow("net8.0", true, true, "Passed")]
    [DataRow("net10.0", false, true, "Passed")]
    [DataRow("net10.0", true, false, "Failed")]
    [DataRow("net10.0", true, true, "Retry")]
    public async Task NativeDotnetTest_ActivatesIdentityRequiredHostAndReturnsItsResults(
        string framework, bool noBuild, bool positional, string outcome)
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        string executionId = Guid.NewGuid().ToString("N");
        TestAsset asset = await GenerateAssetAsync(framework, identity, executionId, outcome);
        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            asset,
            identity,
            async () =>
            {
                string project = Path.Combine(asset.TargetAssetPath, "NativeSidecar.csproj");
                Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, executionId);
                if (noBuild)
                {
                    BoundedCommandLineResult build = await RunAsync(dotnet, $"build \"{project}\" -c Release -bl:build.binlog", asset, environment);
                    Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
                }

                BoundedCommandLineResult version = await RunAsync(dotnet, "--version", asset, environment);
                Assert.AreEqual(SdkVersion, version.StandardOutput.Trim());
                string results = Path.Combine(asset.TargetAssetPath, "results with spaces");
                string retry = outcome == "Retry" ? "--retry-failed-tests 2" : string.Empty;
                BoundedCommandLineResult result = await RunAsync(
                    dotnet,
                    $"test {(positional ? string.Empty : "--project ")}\"{project}\" -c Release {(noBuild ? "--no-build" : string.Empty)} " +
                    $"-bl:native.binlog --report-trx --report-trx-filename \"native report.trx\" --results-directory \"{results}\" " +
                    $"--filter \"Name=Identity|Name=Outcome\" {retry}",
                    asset,
                    environment);
                Assert.AreEqual(outcome == "Failed" ? 2 : 0, result.ExitCode, result.StandardOutput + result.ErrorOutput);
                Assert.Contains("total: 2", result.StandardOutput);
                Assert.Contains(outcome == "Failed" ? "failed: 1" : "failed: 0", result.StandardOutput);
                Assert.AreEqual(identity, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "identity.txt"), TestContext.CancellationToken));
                Assert.AreEqual(executionId, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "execution-id.txt"), TestContext.CancellationToken));

                XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
                string report = Path.Combine(results, "native_report.trx");
                Assert.IsTrue(File.Exists(report), result.StandardOutput + result.ErrorOutput);
                XElement[] tests = XDocument.Load(report).Descendants(ns + "UnitTestResult").ToArray();
                Assert.HasCount(outcome == "Retry" ? 1 : 2, tests);
                Assert.HasCount(outcome == "Failed" ? 1 : 0, tests.Where(test => (string?)test.Attribute("outcome") == "Failed"));
                if (outcome == "Retry")
                {
                    string[] attempts = Directory.GetFiles(Path.Combine(results, "Retries"), "native_report.trx", SearchOption.AllDirectories)
                        .Order(StringComparer.Ordinal).ToArray();
                    Assert.HasCount(2, attempts);
                    XElement[] firstAttempt = XDocument.Load(attempts[0]).Descendants(ns + "UnitTestResult").ToArray();
                    XElement[] secondAttempt = XDocument.Load(attempts[1]).Descendants(ns + "UnitTestResult").ToArray();
                    Assert.HasCount(2, firstAttempt);
                    Assert.HasCount(1, firstAttempt.Where(test => (string?)test.Attribute("outcome") == "Failed"));
                    Assert.HasCount(1, secondAttempt);
                    Assert.AreEqual("Passed", (string?)secondAttempt[0].Attribute("outcome"));
                }
            });
    }

    public static IEnumerable<(bool Native, string Arguments, string Outcome, string[] Selected, bool Launched, string? Error)> FilterCases()
    {
        foreach (bool native in new[] { false, true })
        {
            yield return (native, "--filter FullyQualifiedName~Identity", "Passed", ["Identity"], true, null);
            yield return (native, "--filter \"TestCategory=Fast Lane&(Name=Identity|Name=Missing)\"", "Passed", ["Identity"], true, null);
            yield return (native, "--filter \"TestCategory=Quoted \\\"Lane\\\"|Name=Missing\"", "Passed", ["Outcome"], true, null);
            yield return (native, "--filter Name=Outcome", "Failed", ["Outcome"], true, null);
            yield return (native, "--filter Name=Missing", "Passed", [], true, null);
            yield return (native, "--filter \"(Name=Identity\"", "Passed", [], true, null);
            yield return (native, "--filter", "Passed", [], false,
                "Option '--filter' from provider 'Windows application-model controller' (UID: ControllerTestFramework) expects at least 1 arguments");
            yield return (native, "--filter Name=Identity --filter Name=Outcome", "Passed", [], false,
                "Option '--filter' from provider 'Windows application-model controller' (UID: ControllerTestFramework) expects at most 1 arguments");
            yield return (native, "--unrelated-invalid-option", "Passed", [], false, "Unknown option '--unrelated-invalid-option'");
        }
    }

    [TestMethod]
    [DynamicData(nameof(FilterCases))]
    public async Task FrameworkFilter_ActivatesSelectedHostAndPreservesSelectionAndFailures(
        bool native, string arguments, string outcome, string[] selected, bool launched, string? error)
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        string executionId = Guid.NewGuid().ToString("N");
        string platformArguments = "--report-trx --report-trx-filename \"filtered report.trx\" " +
            "--results-directory \"$(MSBuildProjectDirectory)\\filtered results\" " +
            "--diagnostic --diagnostic-output-directory \"$(MSBuildProjectDirectory)\\diagnostics\" " + arguments;
        TestAsset asset = await GenerateAssetAsync(
            native ? "net10.0" : "net8.0", identity, executionId, outcome,
            commandLineArguments: native ? string.Empty : platformArguments);
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, executionId);
        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            asset,
            identity,
            async () =>
            {
                BoundedCommandLineResult version = await RunAsync(dotnet, "--version", asset, environment);
                Assert.AreEqual(SdkVersion, version.StandardOutput.Trim());
                BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
                Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
                string results = Path.Combine(asset.TargetAssetPath, "filtered results");
                string diagnostics = Path.Combine(asset.TargetAssetPath, "diagnostics");
                BoundedCommandLineResult result = await RunAsync(
                    dotnet,
                    native
                        ? $"test NativeSidecar.csproj -c Release --no-build -bl:filtered.binlog " +
                            $"--report-trx --report-trx-filename \"filtered report.trx\" --results-directory \"{results}\" " +
                            $"--diagnostic --diagnostic-output-directory \"{diagnostics}\" {arguments}"
                        : "msbuild NativeSidecar.csproj -t:InvokeTestingPlatform -p:Configuration=Release -bl:filtered.binlog",
                    asset, environment);
                string output = result.StandardOutput + result.ErrorOutput;
                if (selected.Length > 0)
                {
                    Assert.AreEqual(outcome == "Failed" ? native ? 2 : 1 : 0, result.ExitCode, output);
                    XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
                    XElement[] tests = XDocument.Load(Path.Combine(results, "filtered_report.trx")).Descendants(ns + "UnitTestResult").ToArray();
                    Assert.AreSequenceEqual(selected.Order(StringComparer.Ordinal), tests.Select(test => (string)test.Attribute("testName")!).Order(StringComparer.Ordinal));
                    Assert.AreSequenceEqual(Enumerable.Repeat(outcome, selected.Length), tests.Select(test => (string)test.Attribute("outcome")!));
                }
                else
                {
                    Assert.AreNotEqual(0, result.ExitCode, output);
                    Assert.IsFalse(File.Exists(Path.Combine(asset.TargetAssetPath, "execution-id.txt")));
                    Assert.IsFalse(File.Exists(Path.Combine(asset.TargetAssetPath, "first-attempt.txt")));
                    if (error is not null)
                    {
                        Assert.Contains(error, output);
                    }
                    else if (arguments.Contains("Missing", StringComparison.Ordinal))
                    {
                        Assert.AreEqual(native ? 8 : 1, result.ExitCode, output);
                        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
                        Assert.IsEmpty(XDocument.Load(Path.Combine(results, "filtered_report.trx")).Descendants(ns + "UnitTestResult"));
                    }
                    else
                    {
                        Assert.AreEqual(native ? 8 : 1, result.ExitCode, output);
                        if (native)
                        {
                            // SDK 10 reports the host's error count but does not display its filter parse message.
                            Assert.Contains(
                                """
                                Test run summary: Zero tests ran
                                  error: 1
                                  total: 0
                                  failed: 0
                                  succeeded: 0
                                  skipped: 0
                                """.ReplaceLineEndings(),
                                output.ReplaceLineEndings());
                        }
                        else
                        {
                            string[] logs = Directory.GetFiles(diagnostics, "*.diag", SearchOption.AllDirectories);
                            string diagnosticOutput = string.Join(Environment.NewLine, logs.Select(File.ReadAllText));
                            Assert.Contains("Incorrect format for TestCaseFilter", diagnosticOutput);
                            Assert.DoesNotContain("Unknown option '--filter'", diagnosticOutput);
                        }
                    }
                }

                string marker = Path.Combine(asset.TargetAssetPath, "identity.txt");
                Assert.AreEqual(launched, File.Exists(marker), output);
                if (launched)
                {
                    Assert.AreEqual(identity, await File.ReadAllTextAsync(marker, TestContext.CancellationToken));
                }
            });
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("--list-tests")]
    public async Task NativeDotnetTest_HelpAndDiscoveryDescribeActivatedHost(string option)
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        TestAsset asset = await GenerateAssetAsync("net8.0", identity, Guid.NewGuid().ToString("N"), "Passed");
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, Guid.NewGuid().ToString("N"));
        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            asset,
            identity,
            async () =>
            {
                BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
                Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
                BoundedCommandLineResult result = await RunAsync(dotnet, $"test --project NativeSidecar.csproj -c Release --no-build {option} -bl:native.binlog", asset, environment);
                Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.ErrorOutput);
                Assert.AreEqual(identity, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "identity.txt"), TestContext.CancellationToken));
                Assert.DoesNotContain("Windows application-model controller", result.StandardOutput);
                if (option == "--help")
                {
                    Assert.Contains("--filter ", result.StandardOutput);
                }
                else
                {
                    Assert.Contains(
                        """
                          Identity
                          Outcome
                        Discovered 2 tests.
                        """.ReplaceLineEndings(),
                        result.StandardOutput.ReplaceLineEndings());
                }
            });
    }

    private static string GetDotnet10()
    {
        string dotnet = Environment.GetEnvironmentVariable("TESTFX_DOTNET_10_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        Assert.IsTrue(
            File.Exists(Path.Combine(Path.GetDirectoryName(dotnet)!, "sdk", SdkVersion, "dotnet.dll")),
            $"Requires .NET SDK {SdkVersion}; set TESTFX_DOTNET_10_PATH to its dotnet executable.");

        using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
        Assert.IsTrue(
            key?.GetValue("AllowDevelopmentWithoutDevLicense") is 1,
            "Requires Windows Developer Mode for unsigned packaged activation; no WinUI or Visual Studio workload is needed.");

        return dotnet;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NativeDotnetTest_DiscoveryCannotFallbackWithDisabledOrAppContainerLauncher(bool appContainer)
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        TestAsset asset = await GenerateAssetAsync("net8.0", identity, Guid.NewGuid().ToString("N"), "Passed", appContainer);
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, Guid.NewGuid().ToString("N"));
        if (!appContainer)
        {
            environment["TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER"] = "never";
        }

        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            asset,
            identity,
            async () =>
            {
                BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
                Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
                BoundedCommandLineResult result = await RunAsync(
                    dotnet, "test --project NativeSidecar.csproj -c Release --no-build --list-tests -bl:native.binlog", asset, environment);
                Assert.AreEqual(4, result.ExitCode, result.StandardOutput + result.ErrorOutput);
                Assert.Contains(
                    appContainer ? "Use the InvokeTestingPlatform MSBuild target" : "require an enabled packaged-app launcher",
                    result.StandardOutput + result.ErrorOutput);
                Assert.IsFalse(File.Exists(Path.Combine(asset.TargetAssetPath, "identity.txt")));
            });
    }

    [TestMethod]
    public async Task NativeDotnetTest_DisablingRoutingReproducesIdentityRequiredStartupFailure()
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        TestAsset asset = await GenerateAssetAsync("net8.0", identity, Guid.NewGuid().ToString("N"), "Passed");
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, Guid.NewGuid().ToString("N"));
        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            asset,
            identity,
            async () =>
            {
                BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
                Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
                BoundedCommandLineResult result = await RunAsync(
                    dotnet,
                    "test --project NativeSidecar.csproj -c Release --no-build -p:EnableMicrosoftTestingExtensionsPackagedApp=false -bl:direct.binlog",
                    asset, environment);
                Assert.AreNotEqual(0, result.ExitCode);
                Assert.Contains("Startup.Initialize", result.StandardOutput + result.ErrorOutput);
                Assert.Contains("Zero tests ran", result.StandardOutput);
                Assert.IsFalse(File.Exists(Path.Combine(asset.TargetAssetPath, "identity.txt")));
            });
    }

    [TestMethod]
    public async Task DotnetRun_AlsoActivatesIdentityRequiredHost()
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        string executionId = Guid.NewGuid().ToString("N");
        TestAsset asset = await GenerateAssetAsync("net8.0", identity, executionId, "Passed");
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, executionId);
        await WindowsApplicationModelTestTools.ExecuteWithPackageCleanupAsync(
            asset,
            identity,
            async () =>
            {
                BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
                Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
                string results = Path.Combine(asset.TargetAssetPath, "results");
                BoundedCommandLineResult result = await RunAsync(
                    dotnet,
                    $"run --project NativeSidecar.csproj -c Release --no-build -- --report-trx --report-trx-filename run.trx --results-directory \"{results}\"",
                    asset, environment);
                Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.ErrorOutput);
                Assert.AreEqual(identity, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "identity.txt"), TestContext.CancellationToken));
                XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
                Assert.HasCount(2, XDocument.Load(Path.Combine(results, "run.trx")).Descendants(ns + "UnitTestResult"));
            });
    }

<<<<<<< HEAD
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public async Task DotnetRun_PreservesLaunchProfileArgumentPrecedence(bool noBuild, bool explicitArguments, bool noLaunchProfile)
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        string executionId = Guid.NewGuid().ToString("N");
        using TestAsset asset = await GenerateAssetAsync("net8.0", identity, executionId, "Passed");
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, executionId);
        string profileResults = Path.Combine(asset.TargetAssetPath, "profile results");
        string explicitResults = Path.Combine(asset.TargetAssetPath, "explicit results");
        string profiles = Path.Combine(asset.TargetAssetPath, "Properties");
        Directory.CreateDirectory(profiles);
        await File.WriteAllTextAsync(
            Path.Combine(profiles, "launchSettings.json"),
            JsonSerializer.Serialize(new
            {
                profiles = new
                {
                    Threshold = new
                    {
                        commandName = "Project",
                        commandLineArgs = $"--minimum-expected-tests 3 --report-trx --report-trx-filename \"profile report.trx\" --results-directory \"{profileResults}\"",
                    },
                },
            }),
            TestContext.CancellationToken);
        try
        {
            if (noBuild)
            {
                BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
                Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
            }

            string tail = explicitArguments
                ? $"-- --minimum-expected-tests 1 --report-trx --report-trx-filename \"explicit report.trx\" --results-directory \"{explicitResults}\""
                : string.Empty;
            BoundedCommandLineResult result = await RunAsync(
                dotnet,
                $"run --project NativeSidecar.csproj -c Release {(noBuild ? "--no-build" : "-bl:run-build.binlog")} " +
                $"{(noLaunchProfile ? "--no-launch-profile" : "--launch-profile Threshold")} {tail}",
                asset,
                environment);
            Assert.AreEqual(!explicitArguments && !noLaunchProfile ? 9 : 0, result.ExitCode, result.StandardOutput + result.ErrorOutput);
            Assert.AreEqual(identity, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "identity.txt"), TestContext.CancellationToken));
            Assert.AreEqual(executionId, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "execution-id.txt"), TestContext.CancellationToken));
            Assert.AreEqual(!explicitArguments && !noLaunchProfile, File.Exists(Path.Combine(profileResults, "profile_report.trx")));
            Assert.AreEqual(explicitArguments, File.Exists(Path.Combine(explicitResults, "explicit_report.trx")));
            if (explicitArguments || !noLaunchProfile)
            {
                string report = explicitArguments
                    ? Path.Combine(explicitResults, "explicit_report.trx")
                    : Path.Combine(profileResults, "profile_report.trx");
                XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
                Assert.HasCount(2, XDocument.Load(report).Descendants(ns + "UnitTestResult"));
            }
        }
        finally
        {
            await RemovePackageAsync(identity);
        }
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("--list-tests")]
    [DataRow("--list-tests json")]
    [DataRow("@informational.rsp")]
    public async Task DotnetRun_HelpAndDiscoveryDescribeActivatedHost(string option)
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        using TestAsset asset = await GenerateAssetAsync("net8.0", identity, Guid.NewGuid().ToString("N"), "Passed");
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(Path.Combine(asset.TargetAssetPath, "informational.rsp"), "--list-tests", TestContext.CancellationToken);
        try
        {
            BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
            Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
            BoundedCommandLineResult result = await RunAsync(
                dotnet, $"run --project NativeSidecar.csproj -c Release --no-build --no-launch-profile -- {option}", asset, environment);
            Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.ErrorOutput);
            Assert.AreEqual(identity, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "identity.txt"), TestContext.CancellationToken));
            Assert.IsFalse(File.Exists(Path.Combine(asset.TargetAssetPath, "first-attempt.txt")));
            Assert.DoesNotContain("mstest-appmodel-controller.exe", result.StandardOutput);
            if (option == "--help")
            {
                // The process helper removes empty stdout lines; the unit test checks their formatting.
                Assert.Contains(
                    """
                    Usage NativeSidecar.exe [option providers] [extension option providers]
                    Execute a .NET Test Application.
                    """.ReplaceLineEndings(),
                    result.StandardOutput.ReplaceLineEndings());
                Assert.Contains("--filter", result.StandardOutput);
            }
            else if (option == "--list-tests json")
            {
                using var document = JsonDocument.Parse(result.StandardOutput);
                Assert.AreEqual(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
                Assert.AreSequenceEqual(
                    ["Identity", "Outcome"],
                    document.RootElement.GetProperty("tests").EnumerateArray()
                        .Select(test => test.GetProperty("displayName").GetString()));
            }
            else
            {
                Assert.Contains(
                    """
                      Identity
                      Outcome
                    Discovered 2 test(s) in assembly
                    """.ReplaceLineEndings(),
                    result.StandardOutput.ReplaceLineEndings());
            }
        }
        finally
        {
            await RemovePackageAsync(identity);
        }
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("--list-tests")]
    public async Task DotnetRun_InformationalZeroExitWithoutHandshakeIsFailure(string option)
    {
        string dotnet = GetDotnet10();
        string identity = $"MSTest.Native.{Guid.NewGuid():N}";
        using TestAsset asset = await GenerateAssetAsync("net8.0", identity, Guid.NewGuid().ToString("N"), "EarlyExit");
        Dictionary<string, string?> environment = GetNativeEnvironment(dotnet, Guid.NewGuid().ToString("N"));
        try
        {
            BoundedCommandLineResult build = await RunAsync(dotnet, "build -c Release -bl:build.binlog", asset, environment);
            Assert.AreEqual(0, build.ExitCode, build.StandardOutput + build.ErrorOutput);
            BoundedCommandLineResult result = await RunAsync(
                dotnet, $"run --project NativeSidecar.csproj -c Release --no-build --no-launch-profile -- {option}", asset, environment);
            Assert.AreEqual(4, result.ExitCode, result.StandardOutput + result.ErrorOutput);
            Assert.AreEqual(identity, await File.ReadAllTextAsync(Path.Combine(asset.TargetAssetPath, "identity.txt"), TestContext.CancellationToken));
            Assert.Contains("No complete informational result was received.", result.ErrorOutput);
            Assert.IsFalse(File.Exists(Path.Combine(asset.TargetAssetPath, "first-attempt.txt")));
        }
        finally
        {
            await RemovePackageAsync(identity);
        }
    }

=======
>>>>>>> Fix native dotnet test routing for packaged applications
    private Task<BoundedCommandLineResult> RunAsync(
        string dotnet, string arguments, TestAsset asset, IDictionary<string, string?> environment)
        => RunWindowsApplicationModelCommandAsync(
            $"\"{dotnet}\" {arguments}",
            asset.TargetAssetPath,
            TestContext.CancellationToken,
            environment,
            cleanEnvironment: true);

    private static Dictionary<string, string?> GetNativeEnvironment(string dotnet, string executionId)
    {
        Dictionary<string, string?> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            [ExecutionIdEnvironmentVariable] = executionId,
        };
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string name = entry.Key.ToString()!;
            if (!WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(name, StringComparer.OrdinalIgnoreCase)
                && !name.StartsWith("TESTINGPLATFORM_", StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("MSTEST_APPMODEL_", StringComparison.OrdinalIgnoreCase)
                && name != "TRXNAMEDPIPENAME")
            {
                environment[name] = entry.Value?.ToString();
            }
        }

        environment["DOTNET_ROOT"] = Path.GetDirectoryName(dotnet);
        environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        environment["MSBUILDUSESERVER"] = "0";
        return environment;
    }

    private static async Task<TestAsset> GenerateAssetAsync(
        string framework, string identity, string executionId, string outcome, bool appContainer = false, string commandLineArguments = "")
    {
        TestAsset asset = await TestAsset.GenerateAssetAsync(
            "NativeSidecar",
            $$"""
            #file global.json
            {
              "sdk": { "version": "{{SdkVersion}}", "rollForward": "disable" },
              "msbuild-sdks": { "MSTest.Sdk": "{{MSTestVersion}}" },
              "test": { "runner": "Microsoft.Testing.Platform" }
            }
            #file NativeSidecar.csproj
            <Project Sdk="MSTest.Sdk">
              <PropertyGroup>
                <TargetFramework>{{framework}}-windows10.0.19041.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <UseWinUI>true</UseWinUI>
                <TestingExtensionsProfile>None</TestingExtensionsProfile>
                <EnableMicrosoftTestingExtensionsTrxReport>true</EnableMicrosoftTestingExtensionsTrxReport>
                <EnableMicrosoftTestingExtensionsRetry>true</EnableMicrosoftTestingExtensionsRetry>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <NoWarn>$(NoWarn);NU1507</NoWarn>
<<<<<<< HEAD
<<<<<<< HEAD
                {{(outcome == "EarlyExit" ? "<GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>" : string.Empty)}}
=======
>>>>>>> Fix native dotnet test routing for packaged applications
=======
                <TestingPlatformCommandLineArguments>{{System.Security.SecurityElement.Escape(commandLineArguments)}}</TestingPlatformCommandLineArguments>
                <TestingPlatformCaptureOutput>false</TestingPlatformCaptureOutput>
>>>>>>> Forward MSTest filters through the packaged-app sidecar
              </PropertyGroup>
              <ItemGroup>
                <None Update="AppxManifest.xml" CopyToOutputDirectory="PreserveNewest" />
                <None Update="Logo.png" CopyToOutputDirectory="PreserveNewest" />
              </ItemGroup>
            </Project>
            #file Tests.cs
            using System.Runtime.CompilerServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using Windows.ApplicationModel;

            public static class Startup
            {
                [ModuleInitializer]
                public static void Initialize()
                {
                    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
                    File.WriteAllText(Path.Combine(root, "identity.txt"), Package.Current.Id.Name);
                }
            }

            [TestClass]
            public sealed class NativeTests
            {
                private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

                [TestMethod]
                [TestCategory("Fast Lane")]
                public void Identity()
                {
                    Assert.AreEqual("{{identity}}", Package.Current.Id.Name);
                    string? execution = Environment.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_EXECUTIONID");
                    Assert.AreEqual("{{executionId}}", execution);
                    File.WriteAllText(Path.Combine(Root, "execution-id.txt"), execution!);
                }

                [TestMethod]
                [TestCategory("Quoted \"Lane\"")]
                public void Outcome()
                {
                    string marker = Path.Combine(Root, "first-attempt.txt");
                    bool fail = "{{outcome}}" == "Failed" || ("{{outcome}}" == "Retry" && !File.Exists(marker));
                    File.WriteAllText(marker, "executed");
                    Assert.IsFalse(fail, "expected host failure");
                }
            }
<<<<<<< HEAD
            #file Main.cs
            {{(outcome == "EarlyExit" ? """
            public static class Program
            {
                public static int Main(string[] args)
                {
                    return 0;
                }
            }
            """ : string.Empty)}}
=======
>>>>>>> Fix native dotnet test routing for packaged applications
            #file AppxManifest.xml
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                     xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
                     xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
                     IgnorableNamespaces="uap uap10 rescap">
              <Identity Name="{{identity}}" Publisher="CN=MSTestAcceptance" Version="1.0.0.0" ProcessorArchitecture="neutral" />
              <Properties><DisplayName>Sidecar tests</DisplayName><PublisherDisplayName>MSTest</PublisherDisplayName><Logo>Logo.png</Logo></Properties>
              <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
              <Resources><Resource Language="en-US" /></Resources>
              <Applications>
                <Application Id="App" Executable="NativeSidecar.exe" EntryPoint="Windows.FullTrustApplication" {{(appContainer ? "uap10:TrustLevel=\"appContainer\"" : string.Empty)}}>
                  <uap:VisualElements DisplayName="Sidecar tests" Description="Sidecar tests" BackgroundColor="transparent"
                                      Square150x150Logo="Logo.png" Square44x44Logo="Logo.png" AppListEntry="none" />
                </Application>
              </Applications>
              <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
            </Package>
            """);
        await File.WriteAllBytesAsync(
            Path.Combine(asset.TargetAssetPath, "Logo.png"),
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a5WQAAAAASUVORK5CYII="));
        return asset;
    }
}
