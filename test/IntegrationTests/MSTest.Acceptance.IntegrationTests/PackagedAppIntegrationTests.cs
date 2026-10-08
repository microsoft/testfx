// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class PackagedAppIntegrationTests : AcceptanceTestBase<NopAssetFixture>
{
    public TestContext TestContext { get; set; }

    public static IEnumerable<(string Package, string Properties, bool Controller, bool Helper)> ConsumerContracts()
    {
        foreach (string package in new[] { "MSTest", "MSTest.TestAdapter" })
        {
            yield return (package, "<UseWinUI>true</UseWinUI>", true, true);
            yield return (package, "<UseUwp>true</UseUwp>", true, true);
            yield return (package, "<UseUwpTools>true</UseUwpTools>", true, true);
            yield return (package, "<UseUwp>true</UseUwp><UseUwpTools>false</UseUwpTools>", false, true);
            yield return (package, "<UseWinUI>true</UseWinUI><WindowsPackageType>None</WindowsPackageType>", false, true);
            yield return (package, "<UseWinUI>true</UseWinUI><UseVSTest>true</UseVSTest>", false, true);
            yield return (package, "<UseWinUI>true</UseWinUI><IsTestApplication>false</IsTestApplication>", false, true);
            yield return (package, "<UseWinUI>true</UseWinUI><OutputType>Library</OutputType>", false, true);
            yield return (package, "<UseWinUI>true</UseWinUI><EnableMSTestRunner>false</EnableMSTestRunner>", false, false);
            yield return (package, "<UseWinUI>true</UseWinUI><EnableMicrosoftTestingExtensionsPackagedApp>false</EnableMicrosoftTestingExtensionsPackagedApp>", false, true);
            yield return (package, "<UseWinUI>true</UseWinUI><OS>Unix</OS>", false, true);
            yield return (package, string.Empty, false, true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ConsumerContracts))]
    public async Task ExplicitConsumer_SelectsControllerOnlyForPackagedMtpApplication(
        string package, string properties, bool controller, bool helper)
    {
        using TestAsset asset = await GenerateConsumerAsync(package, properties);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -t:WriteContract",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, result.ToString());

        bool selectsController = controller && OperatingSystem.IsWindows();
        string[] lines = await File.ReadAllLinesAsync(Path.Combine(asset.TargetAssetPath, "contract.txt"), TestContext.CancellationToken);
        Assert.AreSequenceEqual(
            new[]
            {
                $"Controller={(selectsController ? "mstest-appmodel-controller.exe" : string.Empty)}",
                $"Extensions={(selectsController ? "msbuild;packagedapp;trx" : string.Empty)}",
                $"Helper={helper.ToString().ToLowerInvariant()}",
            },
            lines);
        AssertAlignedPackages(asset.TargetAssetPath);
    }

    [TestMethod]
    [DataRow("MSTest")]
    [DataRow("MSTest.TestAdapter")]
    public async Task ExplicitConsumer_PreservesCustomExecutableAndEnvironment(string package)
    {
        using TestAsset asset = await GenerateConsumerAsync(
            package,
            "<UseWinUI>true</UseWinUI><TestingPlatformExecutablePath>custom-controller.exe</TestingPlatformExecutablePath>");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -t:WriteContract",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, result.ToString());
        Assert.AreSequenceEqual(
            new[] { "Controller=custom-controller.exe", "Extensions=", "Helper=true" },
            await File.ReadAllLinesAsync(Path.Combine(asset.TargetAssetPath, "contract.txt"), TestContext.CancellationToken));
        result.AssertOutputContains("CallerEnvironment=preserved");
    }

    [TestMethod]
    [DynamicData(nameof(ConsumerContracts))]
    public async Task ComputeRunArguments_SelectsSidecarOnlyForPackagedMtpApplication(
        string package, string properties, bool controller, bool helper)
    {
        _ = helper;
        using TestAsset asset = await GenerateConsumerAsync(package, properties);
        string executable = Path.Combine(asset.TargetAssetPath, "bin", "Debug", "net8.0", "PackagedConsumer.exe");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -t:WriteRunContract -p:TestingPlatformPackagedAppTargetPath=\"{executable}\"",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, result.ToString());
        string[] lines = await File.ReadAllLinesAsync(Path.Combine(asset.TargetAssetPath, "run-contract.txt"), TestContext.CancellationToken);
        bool selectsController = controller && OperatingSystem.IsWindows();
        string expectedCommand = properties.Contains("<OutputType>Library</OutputType>", StringComparison.Ordinal)
            ? string.Empty
            : OperatingSystem.IsWindows() ? "PackagedConsumer.exe" : "PackagedConsumer";
        Assert.AreEqual($"Command={(selectsController ? "mstest-appmodel-controller.exe" : expectedCommand)}", lines[0]);
<<<<<<< HEAD
        Assert.AreEqual("Arguments=", lines[1]);
        if (selectsController)
        {
            string bootstrapFile = (await File.ReadAllTextAsync(
                Path.Combine(asset.TargetAssetPath, "bootstrap-file-path.txt"), TestContext.CancellationToken)).Trim();
            Assert.AreSequenceEqual(
                ["--internal-packagedapp-controller-v1", executable, "msbuild;packagedapp;trx"],
                await File.ReadAllLinesAsync(bootstrapFile, TestContext.CancellationToken));
=======
        if (selectsController)
        {
            Assert.AreEqual(
                $"Arguments=--internal-packagedapp-controller-v1 \"{executable}\" \"msbuild;packagedapp;trx\"",
                lines[1].TrimEnd());
        }
        else
        {
            Assert.AreEqual("Arguments=", lines[1]);
>>>>>>> Fix native dotnet test routing for packaged applications
        }
    }

    [TestMethod]
    [DataRow("<RunCommand>custom-run.exe</RunCommand>", "custom-run.exe")]
    [DataRow("<TestingPlatformExecutablePath>custom-controller.exe</TestingPlatformExecutablePath>", "PackagedConsumer.exe")]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ComputeRunArguments_PreservesExplicitOverrides(string properties, string expectedCommand)
    {
        using TestAsset asset = await GenerateConsumerAsync("MSTest.TestAdapter", $"<UseWinUI>true</UseWinUI>{properties}");
        string executable = Path.Combine(asset.TargetAssetPath, "bin", "Debug", "net8.0", "PackagedConsumer.exe");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -t:WriteRunContract -p:TestingPlatformPackagedAppTargetPath=\"{executable}\"",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, result.ToString());
        string[] lines = await File.ReadAllLinesAsync(Path.Combine(asset.TargetAssetPath, "run-contract.txt"), TestContext.CancellationToken);
        Assert.AreEqual($"Command={expectedCommand}", lines[0]);
        Assert.DoesNotContain("--internal-packagedapp-controller", string.Join(Environment.NewLine, lines));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ComputeRunArguments_ResolvesRelativeTargetAndPreservesTailWithoutAppHost()
    {
        using TestAsset asset = await GenerateConsumerAsync(
            "MSTest.TestAdapter",
            """
            <UseWinUI>true</UseWinUI>
            <UseAppHost>false</UseAppHost>
            <TestingPlatformPackagedAppTargetPath>staged\host.exe</TestingPlatformPackagedAppTargetPath>
            <StartArguments>--results-directory &quot;a b&quot;</StartArguments>
            <TestingPlatformCommandLineArguments>--report-trx</TestingPlatformCommandLineArguments>
            """);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -t:WriteRunContract",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, result.ToString());
        Assert.AreSequenceEqual(
            new[]
            {
                "Command=mstest-appmodel-controller.exe",
<<<<<<< HEAD
                "Arguments= --results-directory \"a b\" --report-trx",
            },
            await File.ReadAllLinesAsync(Path.Combine(asset.TargetAssetPath, "run-contract.txt"), TestContext.CancellationToken));
        string bootstrapFile = (await File.ReadAllTextAsync(
            Path.Combine(asset.TargetAssetPath, "bootstrap-file-path.txt"), TestContext.CancellationToken)).Trim();
        Assert.AreSequenceEqual(
            ["--internal-packagedapp-controller-v1", Path.Combine(asset.TargetAssetPath, "staged", "host.exe"), "msbuild;packagedapp;trx"],
            await File.ReadAllLinesAsync(bootstrapFile, TestContext.CancellationToken));
=======
                $"Arguments=--internal-packagedapp-controller-v1 \"{Path.Combine(asset.TargetAssetPath, "staged", "host.exe")}\" \"msbuild;packagedapp;trx\"  --results-directory \"a b\" --report-trx",
            },
            await File.ReadAllLinesAsync(Path.Combine(asset.TargetAssetPath, "run-contract.txt"), TestContext.CancellationToken));
>>>>>>> Fix native dotnet test routing for packaged applications
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ComputeRunArguments_NoAppHostWithoutStagedExecutableFailsExplicitly()
    {
        using TestAsset asset = await GenerateConsumerAsync("MSTest.TestAdapter", "<UseWinUI>true</UseWinUI><UseAppHost>false</UseAppHost>");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -t:WriteRunContract",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, result.ExitCode);
        result.AssertOutputContains("Enable UseAppHost or set TestingPlatformPackagedAppTargetPath");
    }

    [TestMethod]
<<<<<<< HEAD
    [OSCondition(OperatingSystems.Windows)]
    public async Task ComputeRunArguments_StagedControllerIsIncrementalAndCleaned()
    {
        using TestAsset asset = await GenerateConsumerAsync("MSTest.TestAdapter", "<UseWinUI>true</UseWinUI>");
        string executable = Path.Combine(asset.TargetAssetPath, "staged", "host.exe");
        string command = $"build \"{asset.TargetAssetPath}\" -t:WriteRunContract -p:TestingPlatformPackagedAppTargetPath=\"{executable}\"";
        DotnetMuxerResult first = await DotnetCli.RunAsync(command, cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, first.ExitCode, first.ToString());
        string bootstrapFile = (await File.ReadAllTextAsync(
            Path.Combine(asset.TargetAssetPath, "bootstrap-file-path.txt"), TestContext.CancellationToken)).Trim();
        string controller = Path.Combine(Path.GetDirectoryName(bootstrapFile)!, "mstest-appmodel-controller.exe");
        DateTime bootstrapTimestamp = File.GetLastWriteTimeUtc(bootstrapFile);
        DateTime controllerTimestamp = File.GetLastWriteTimeUtc(controller);
        DotnetMuxerResult second = await DotnetCli.RunAsync(command, cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, second.ExitCode, second.ToString());
        Assert.AreEqual(bootstrapTimestamp, File.GetLastWriteTimeUtc(bootstrapFile));
        Assert.AreEqual(controllerTimestamp, File.GetLastWriteTimeUtc(controller));
        DotnetMuxerResult clean = await DotnetCli.RunAsync(
            $"clean \"{asset.TargetAssetPath}\"", cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, clean.ExitCode, clean.ToString());
        Assert.IsFalse(File.Exists(bootstrapFile));
        Assert.IsFalse(File.Exists(controller));
    }

    [TestMethod]
=======
>>>>>>> Fix native dotnet test routing for packaged applications
    [DataRow("MSTest")]
    [DataRow("MSTest.TestAdapter")]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExplicitConsumer_CustomExecutableRunsWithoutControllerEnvironment(string package)
    {
        using TestAsset asset = await GenerateConsumerAsync(package, string.Empty, "Custom");
        _ = await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", cancellationToken: TestContext.CancellationToken);
        string executable = Path.Combine(asset.TargetAssetPath, "bin", "Release", "net8.0", "PackagedConsumer.exe");
        string resultsDirectory = Path.Combine(asset.TargetAssetPath, "results");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild \"{asset.TargetAssetPath}\" -t:InvokeTestingPlatform -p:Configuration=Release -p:UseWinUI=true " +
            $"-p:TestingPlatformExecutablePath=\"{executable}\" " +
            $"-p:TestingPlatformCommandLineArguments=\"--report-trx --report-trx-filename custom.trx --results-directory {resultsDirectory}\"",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode, result.ToString());
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement test = Assert.ContainsSingle(XDocument.Load(Path.Combine(resultsDirectory, "custom.trx")).Descendants(ns + "UnitTestResult"));
        Assert.AreEqual("Passed", (string?)test.Attribute("outcome"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExplicitConsumer_MissingPayloadFailsWithoutFallback()
    {
        using TestAsset asset = await GenerateConsumerAsync("MSTest.TestAdapter", "<UseWinUI>true</UseWinUI>");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -t:WriteContract -p:_TestingPlatformPackagedAppPackageRoot=\"{asset.TargetAssetPath}{Path.DirectorySeparatorChar}missing{Path.DirectorySeparatorChar}\"",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, result.ExitCode);
        result.AssertOutputContains("The packaged-app controller payload is missing or incomplete.");
        Assert.IsFalse(File.Exists(Path.Combine(asset.TargetAssetPath, "contract.txt")));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExplicitConsumer_IncompatiblePlatformCannotSilentlyRun()
    {
        using TestAsset asset = await GenerateConsumerAsync(
            "MSTest.TestAdapter",
            "<UseWinUI>true</UseWinUI>",
            extraReferences: "<PackageReference Include=\"Microsoft.Testing.Platform.MSBuild\" Version=\"[1.0.0]\" />");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -c Release",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, result.ExitCode);
        result.AssertOutputContains("The packaged-app controller requires version-aligned Microsoft.Testing.Platform.MSBuild assets.");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExplicitConsumer_SuppressedLauncherCannotReturnSuccess()
    {
        using TestAsset asset = await GenerateConsumerAsync("MSTest.TestAdapter", string.Empty);
        _ = await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", cancellationToken: TestContext.CancellationToken);
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild \"{asset.TargetAssetPath}\" -t:InvokeTestingPlatform -p:Configuration=Release -p:UseWinUI=true -p:TestingPlatformCaptureOutput=false",
            environmentVariables: new Dictionary<string, string?> { ["TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER"] = "never" },
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, result.ExitCode);
        result.AssertOutputContains("The packaged application test host was not launched.");
    }

    [TestMethod]
    [DataRow("Passed", 0)]
    [DataRow("Failed", 1)]
    [DataRow("Mixed", 1)]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExplicitConsumer_MultiTargetRunsAggregateExitCodes(string outcome, int expectedExitCode)
    {
        using TestAsset asset = await GenerateConsumerAsync("MSTest.TestAdapter", string.Empty, outcome, multiTarget: true);
        _ = await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", cancellationToken: TestContext.CancellationToken);
        string resultsDirectory = Path.Combine(asset.TargetAssetPath, "results");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild \"{asset.TargetAssetPath}\" -t:DispatchToInnerBuildsWithMTPTestTarget -p:Configuration=Release -p:UseWinUI=true " +
            $"-p:TestingPlatformCommandLineArguments=\"--report-trx --results-directory {resultsDirectory}\"",
            environmentVariables: new Dictionary<string, string?> { ["TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER"] = "always" },
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(expectedExitCode, result.ExitCode, result.ToString());
        string[] reports = Directory.GetFiles(resultsDirectory, "*.trx");
        Assert.HasCount(2, reports);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        foreach (string report in reports)
        {
            XElement test = Assert.ContainsSingle(XDocument.Load(report).Descendants(ns + "UnitTestResult"));
            string expectedOutcome = outcome == "Mixed"
                ? Path.GetFileName(report).Contains("net9.0", StringComparison.Ordinal) ? "Failed" : "Passed"
                : outcome;
            Assert.AreEqual(expectedOutcome, (string?)test.Attribute("outcome"), report);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExplicitConsumer_ControllerTimeoutCancelsHostWithoutFalseSuccess()
    {
        using TestAsset asset = await GenerateConsumerAsync("MSTest.TestAdapter", string.Empty, "Cancellation");
        _ = await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", cancellationToken: TestContext.CancellationToken);
        string marker = Path.Combine(asset.TargetAssetPath, "cancellation.txt");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild \"{asset.TargetAssetPath}\" -t:InvokeTestingPlatform -p:Configuration=Release -p:UseWinUI=true " +
            "-p:TestingPlatformCommandLineArguments=\"--timeout 30s\"",
            environmentVariables: new Dictionary<string, string?>
            {
                ["TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER"] = "always",
                ["CANCELLATION_MARKER"] = marker,
            },
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsTrue(File.Exists(marker), result.ToString());
        Assert.AreEqual("True", await File.ReadAllTextAsync(marker, TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("MSTest", "Passed", 0)]
    [DataRow("MSTest.TestAdapter", "Passed", 0)]
    [DataRow("MSTest", "Failed", 1)]
    [DataRow("MSTest.TestAdapter", "Failed", 1)]
    [DataRow("MSTest.TestAdapter", "Empty", 0)]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExplicitConsumer_ControllerRunsShippingHostAndReconcilesResults(
        string package, string outcome, int expectedFailed)
    {
        // A forced loose layout exercises the shipping sidecar's process contract without OS registration.
        // Real WinUI/UWP activation is covered by the preflight-gated Windows application-model tests.
        using TestAsset asset = await GenerateConsumerAsync(package, string.Empty, outcome);
        DotnetMuxerResult build = await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -c Release",
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, build.ExitCode, build.ToString());
        AssertAlignedPackages(asset.TargetAssetPath);

        string resultsDirectory = Path.Combine(asset.TargetAssetPath, "results");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild \"{asset.TargetAssetPath}\" -t:InvokeTestingPlatform -p:Configuration=Release -p:UseWinUI=true " +
            $"-p:TestingPlatformCommandLineArguments=\"--report-trx --report-trx-filename integration.trx --results-directory {resultsDirectory}\"",
            environmentVariables: new Dictionary<string, string?> { ["TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER"] = "always" },
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(expectedFailed == 0 && outcome != "Empty" ? 0 : 1, result.ExitCode, result.ToString());

        string trxPath = Path.Combine(resultsDirectory, "integration.trx");
        Assert.IsTrue(File.Exists(trxPath), result.ToString());
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var trx = XDocument.Load(trxPath);
        XElement[] results = trx.Descendants(ns + "UnitTestResult").ToArray();
        Assert.HasCount(outcome == "Empty" ? 0 : 1, results, trxPath);
        if (outcome != "Empty")
        {
            Assert.AreEqual(outcome, (string?)results[0].Attribute("outcome"), trxPath);
        }
    }

    private static void AssertAlignedPackages(string assetPath)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(assetPath, "obj", "project.assets.json")));
        foreach (string package in new[] { "Microsoft.Testing.Extensions.PackagedApp.MSBuild", "Microsoft.Testing.Extensions.PackagedApp", "Microsoft.Testing.Platform.MSBuild", "Microsoft.Testing.Platform" })
        {
            Assert.IsTrue(assets.RootElement.GetProperty("libraries").TryGetProperty($"{package}/{MicrosoftTestingPlatformVersion}", out _), $"The consumer did not resolve {package}/{MicrosoftTestingPlatformVersion}.");
        }
    }

    private static Task<TestAsset> GenerateConsumerAsync(
        string package, string properties, string outcome = "Passed", string extraReferences = "", bool multiTarget = false)
    {
        string references = package == "MSTest"
            ? $"<PackageReference Include=\"MSTest\" Version=\"{MSTestVersion}\" />"
            : $"<PackageReference Include=\"MSTest.TestAdapter\" Version=\"{MSTestVersion}\" /><PackageReference Include=\"MSTest.TestFramework\" Version=\"{MSTestVersion}\" />";
        string test = outcome == "Empty"
            ? string.Empty
            : $$"""
                [TestClass]
                public sealed class ConsumerTests
                {
                    [TestMethod]
                    public void Test() => Assert.{{(outcome == "Failed" ? "Fail(\"expected failure\")" : "AreEqual(4, 2 + 2)")}};
                }
                """;
        if (outcome == "Cancellation")
        {
            test = """
                [TestClass]
                public sealed class ConsumerTests
                {
                    public TestContext TestContext { get; set; }

                    [TestMethod]
                    public async System.Threading.Tasks.Task Test()
                    {
                        try
                        {
                            await System.Threading.Tasks.Task.Delay(System.Threading.Timeout.Infinite, TestContext.CancellationToken);
                        }
                        finally
                        {
                            System.IO.File.WriteAllText(System.Environment.GetEnvironmentVariable("CANCELLATION_MARKER"), TestContext.CancellationToken.IsCancellationRequested.ToString());
                        }
                    }
                }
                """;
        }
        else if (outcome == "Custom")
        {
            test = """
                [TestClass]
                public sealed class ConsumerTests
                {
                    [TestMethod]
                    public void Test()
                    {
                        Assert.IsNull(System.Environment.GetEnvironmentVariable("TESTINGPLATFORM_PACKAGEDAPP_TARGET"));
                        Assert.IsNull(System.Environment.GetEnvironmentVariable("MSTEST_APPMODEL_CONTROLLER_EXTENSIONS"));
                        Assert.AreEqual("preserved", System.Environment.GetEnvironmentVariable("CALLER_ENVIRONMENT"));
                    }
                }
                """;
        }
        else if (outcome == "Mixed")
        {
            test = """
                [TestClass]
                public sealed class ConsumerTests
                {
                    [TestMethod]
                    public void Test()
                    {
                #if NET9_0
                        Assert.Fail("expected failure only in the net9 module");
                #else
                        Assert.AreEqual(4, 2 + 2);
                #endif
                    }
                }
                """;
        }

        return TestAsset.GenerateAssetAsync(
            "PackagedConsumer",
            $$"""
            #file PackagedConsumer.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {{(multiTarget ? "<TargetFrameworks>net8.0;net9.0</TargetFrameworks>" : "<TargetFramework>net8.0</TargetFramework>")}}
                <OutputType>Exe</OutputType>
                <EnableMSTestRunner>true</EnableMSTestRunner>
                <EnableMicrosoftTestingExtensionsTrxReport>true</EnableMicrosoftTestingExtensionsTrxReport>
                <NoWarn>$(NoWarn);NU1507</NoWarn>
                {{properties}}
              </PropertyGroup>
              <ItemGroup>
                {{references}}
                {{extraReferences}}
                <PackageReference Include="Microsoft.Testing.Extensions.PackagedApp.MSBuild" Version="{{MicrosoftTestingPlatformVersion}}" />
                <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="{{MicrosoftTestingPlatformVersion}}" />
                <TestingPlatformEnvironmentVariable Include="CALLER_ENVIRONMENT" Value="preserved" />
              </ItemGroup>
              <Target Name="WriteContract" DependsOnTargets="_CalculateGenerateTestingPlatformEntryPoint">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)\contract.txt" Overwrite="true"
                                  Lines="Controller=$([System.IO.Path]::GetFileName('$(TestingPlatformExecutablePath)'))" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)\contract.txt"
                                  Lines="$([MSBuild]::Escape('Extensions=$(_TestingPlatformPackagedAppControllerExtensions)'))" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)\contract.txt"
                                  Lines="Helper=$(GenerateTestingPlatformApplicationHelper)" />
                <Message Importance="high" Text="CallerEnvironment=%(TestingPlatformEnvironmentVariable.Value)" />
              </Target>
              <Target Name="WriteRunContract" DependsOnTargets="_CalculateGenerateTestingPlatformEntryPoint;ComputeRunArguments">
                <WriteLinesToFile File="$(MSBuildProjectDirectory)\run-contract.txt" Overwrite="true"
                                  Lines="Command=$([System.IO.Path]::GetFileName('$(RunCommand)'))" />
                <WriteLinesToFile File="$(MSBuildProjectDirectory)\run-contract.txt"
                                  Lines="$([MSBuild]::Escape('Arguments=$(RunArguments)'))" />
<<<<<<< HEAD
                <WriteLinesToFile File="$(MSBuildProjectDirectory)\bootstrap-file-path.txt" Overwrite="true"
                                  Lines="$(_TestingPlatformPackagedAppBootstrapFile)" />
=======
>>>>>>> Fix native dotnet test routing for packaged applications
              </Target>
            </Project>
            #file Tests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            {{test}}
            """);
    }
}
