// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestHostLauncherTests : AcceptanceTestBase<TestHostLauncherTests.TestAssetFixture>
{
    private const string AssetName = "TestHostLauncher";

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CustomLauncher_IsUsedToStartTestHost_AndRunSucceeds(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // The custom launcher actually starts the test host, so the dummy test must run and the
        // overall run must succeed.
        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Marker proves the platform delegated the launch to our ITestHostLauncher rather than
        // doing the default Process.Start itself.
        Assert.AreEqual("TestHostLauncher.LaunchTestHostAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "LaunchTestHostAsync.txt")));
        Assert.AreEqual(testHost.DirectoryName, File.ReadAllText(Path.Combine(testHost.DirectoryName, "WorkingDirectory.txt")));
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public Task CustomLauncher_ControllerInProcessModeOverridesChildDefault(string tfm)
        => AssertTrxModeAcrossLauncherAsync(tfm, "in-process", "out-of-process", controllerReportsTrx: false);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public Task CustomLauncher_ControllerInProcessModeOverridesUnspecifiedChildMode(string tfm)
        => AssertTrxModeAcrossLauncherAsync(tfm, "in-process", null, controllerReportsTrx: false);

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public Task CustomLauncher_ControllerOutOfProcessModeOverridesChildDefault(string tfm)
        => AssertTrxModeAcrossLauncherAsync(tfm, "out-of-process", "in-process", controllerReportsTrx: true);

    private async Task AssertTrxModeAcrossLauncherAsync(string tfm, string controllerMode, string? childMode, bool controllerReportsTrx)
    {
        var originalHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        using TempDirectory controller = new();
        using TempDirectory child = new();
        TestInfrastructure.TestHost controllerHost = await CloneTestHostAsync(originalHost, controller, AssetName);
        TestInfrastructure.TestHost childHost = await CloneTestHostAsync(originalHost, child, AssetName);
        await File.WriteAllTextAsync(
            Path.Combine(controllerHost.DirectoryName, $"{AssetName}.testconfig.json"),
            $$"""
            {
              "commandLineOptionDefaults": {
                "trx-mode": "{{controllerMode}}"
              }
            }
            """,
            TestContext.CancellationToken);
        string childConfiguration = childMode is null
            ? "{}"
            : $$"""
              {
                "commandLineOptionDefaults": {
                  "trx-mode": "{{childMode}}"
                }
              }
              """;
        await File.WriteAllTextAsync(
            Path.Combine(childHost.DirectoryName, $"{AssetName}.testconfig.json"),
            childConfiguration,
            TestContext.CancellationToken);

        string resultsDirectory = Path.Combine(controller.Path, "results");
        // A missing TRX handshake must fail rather than leave a controller running after this test.
        // Two minutes allows loaded CI agents ample time for startup and normal report generation.
        TestHostResult result = await controllerHost.ExecuteAsync(
            $"--report-trx --report-trx-filename launcher.trx --results-directory \"{resultsDirectory}\" --timeout 2m",
            new()
            {
                ["TRX_LAUNCHER_CHILD_DIRECTORY"] = childHost.DirectoryName,
                ["TRACE_TRX_STARTUP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.HasCount(2, Regex.Matches(result.StandardOutput, @"CUSTOM_TRX_STARTUP:(\d+)"), result.StandardOutput);
        Assert.AreEqual(controllerReportsTrx, result.StandardOutput.Contains("Out of process file artifacts produced:", StringComparison.Ordinal), result.StandardOutput);
        Assert.AreEqual("TestHostLauncher.LaunchTestHostAsync", File.ReadAllText(Path.Combine(controllerHost.DirectoryName, "LaunchTestHostAsync.txt")));
        var report = XDocument.Load(Path.Combine(resultsDirectory, "launcher.trx"));
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement testResult = Assert.ContainsSingle(report.Descendants(ns + "UnitTestResult"));
        Assert.AreEqual("Test1", testResult.Attribute("testName")?.Value);
        Assert.AreEqual("Passed", testResult.Attribute("outcome")?.Value);
        XElement definition = Assert.ContainsSingle(report.Descendants(ns + "UnitTest"));
        Assert.AreEqual(definition.Attribute("id")?.Value, testResult.Attribute("testId")?.Value);
        XElement counters = report.Descendants(ns + "Counters").Single();
        Assert.AreEqual("1", counters.Attribute("total")?.Value);
        Assert.AreEqual("1", counters.Attribute("passed")?.Value);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file TestHostLauncher.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <!-- The test host launcher extension point remains experimental. -->
    <NoWarn>$(NoWarn);TPEXP</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("TRACE_TRX_STARTUP") == "1")
        {
            Console.WriteLine($"CUSTOM_TRX_STARTUP:{Environment.ProcessId}");
        }

        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(new TrxCapability()), (_,__) => new DummyTestFramework());
        testApplicationBuilder.TestHostControllers.AddTestHostLauncher(_ => new TestHostLauncher());
        testApplicationBuilder.AddTrxReportProvider();
        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
    }
}

public sealed class TestHostLauncher : ITestHostLauncher
{
    public string Uid => nameof(TestHostLauncher);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        // Write the marker next to the test host executable (context.FileName) so its location is
        // deterministic regardless of the controller process' current working directory. The test
        // reads it from testHost.DirectoryName, which is the same folder.
        string markerPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(context.FileName)!, "LaunchTestHostAsync.txt");
        System.IO.File.WriteAllText(markerPath, "TestHostLauncher.LaunchTestHostAsync");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(context.FileName)!, "WorkingDirectory.txt"), context.WorkingDirectory);

        string childDirectory = Environment.GetEnvironmentVariable("TRX_LAUNCHER_CHILD_DIRECTORY")
            ?? System.IO.Path.GetDirectoryName(context.FileName)!;
        var startInfo = new ProcessStartInfo(System.IO.Path.Combine(childDirectory, System.IO.Path.GetFileName(context.FileName)))
        {
            UseShellExecute = false,
        };

        foreach (string argument in context.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (KeyValuePair<string, string?> environmentVariable in context.EnvironmentVariables)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        if (context.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = context.WorkingDirectory;
        }

        Process process = Process.Start(startInfo)!;
        return Task.FromResult<ITestHostHandle>(new ProcessTestHostHandle(process));
    }
}

public sealed class TrxCapability : ITrxReportCapability
{
    public bool IsSupported => true;

    public void Enable()
    {
    }
}

public sealed class ProcessTestHostHandle : ITestHostHandle
{
    private readonly Process _process;

    public ProcessTestHostHandle(Process process) => _process = process;

    public string? Identifier => _process.Id.ToString();

    public int ExitCode => _process.ExitCode;

    public bool HasExited => _process.HasExited;

    public Task WaitForExitAsync(CancellationToken cancellationToken) => _process.WaitForExitAsync(cancellationToken);

    public void Terminate() => _process.Kill();

    public void Dispose() => _process.Dispose();
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(
                new PassedTestNodeStateProperty(),
                new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "Test1", 0, Array.Empty<string>(), string.Empty)),
        }));

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.Net)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
