// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class HangDumpTests : AcceptanceTestBase<HangDumpTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HangDump_DefaultSetting_CreateDump(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), tfm);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories);
        Assert.ContainsSingle(dumpFiles, $"Expected single dump file. Found: {Environment.NewLine}{string.Join(Environment.NewLine, dumpFiles)}{Environment.NewLine}{testHostResult}");
    }

    [TestMethod]
    public async Task HangDump_WithDotnetTest_CreateDump()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);

        DotnetMuxerResult testResult = await DotnetCli.RunAsync(
            $"test --project \"{AssetFixture.TargetAssetPath}\" --no-build -c Release -f {TargetFrameworks.NetCurrent} --hangdump --hangdump-timeout 8s --results-directory \"{resultDirectory}\"",
            environmentVariables: new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            },
            workingDirectory: AssetFixture.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        testResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories);
        Assert.ContainsSingle(dumpFiles, $"Expected single dump file. Found: {Environment.NewLine}{string.Join(Environment.NewLine, dumpFiles)}{Environment.NewLine}{testResult}");
    }

    [TestMethod]
    public async Task HangDump_WithDotnetTest_NoHangButOverallTimeGreaterThanTimeout_ShouldPass()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);

        DotnetMuxerResult testResult = await DotnetCli.RunAsync(
            $"test --project \"{AssetFixture.TargetAssetPath}\" --no-build -c Release -f {TargetFrameworks.NetCurrent} --hangdump --hangdump-timeout 7s --results-directory \"{resultDirectory}\"",
            environmentVariables: new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "5000" },
                { "SLEEPTIMEMS2", "5000" },
            },
            workingDirectory: AssetFixture.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        testResult.AssertExitCodeIs(ExitCode.Success);
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories);
        Assert.IsEmpty(dumpFiles);
    }

    [TestMethod]
    public async Task HangDump_CustomFileName_CreateDump()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-filename myhungdumpfile_%p.dmp --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            }, cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "myhungdumpfile_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
    }

    [TestMethod]
    public async Task HangDump_PathWithSpaces_CreateDump()
    {
        string resultDir = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);
        string resultDirectory = Path.Combine(resultDir, "directory with spaces");
        Directory.CreateDirectory(resultDirectory);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"""--hangdump --hangdump-timeout 8s --hangdump-filename myhungdumpfile_%p.dmp --results-directory "{resultDirectory}" """,
            new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "myhungdumpfile_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{TargetFrameworks.NetCurrent}'\n{testHostResult}'");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HangDump_TemplateFileName_CreateDump(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), tfm);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-filename {{pname}}_{{pid}}_{{tfm}}_{{time}}_hang.dmp --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                ["SLEEPTIMEMS1"] = "4000",
                ["SLEEPTIMEMS2"] = "600000",
            },
            cancellationToken: TestContext.CancellationToken);
        AssertTemplateHangDumpCompleted(testHostResult);

        // Verify the dump file uses the template format
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "*_hang.dmp", SearchOption.AllDirectories);
        string dumpFile = Assert.ContainsSingle(
            dumpFiles,
            $"Expected single dump file in '{resultDirectory}'\n{testHostResult}'");
        string fileName = Path.GetFileNameWithoutExtension(dumpFile);

        // File should match pattern: {pname}_{pid}_{tfm}_{time}_hang
        // where {tfm} is e.g. net10.0, net8.0, net462
        // and {time} is yyyy-MM-dd_HH-mm-ss.fffffff
        Assert.MatchesRegex(@"^.+_\d+_net[\w.]+_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.\d{7}_hang$", fileName,
            $"File name should match '{{pname}}_{{pid}}_{{tfm}}_{{time}}_hang' pattern. Actual: {fileName}");

        // Verify the TFM segment matches the expected target framework
        Assert.Contains($"_{tfm}_", fileName, $"File name should contain the TFM '{tfm}'. Actual: {fileName}");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task HangDump_TemplateFileNameWithSubdirectory_CreateDump(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), tfm);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-filename {{asm}}/{{pname}}_{{pid}}_hang.dmp --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                ["SLEEPTIMEMS1"] = "4000",
                ["SLEEPTIMEMS2"] = "600000",
            },
            cancellationToken: TestContext.CancellationToken);
        AssertTemplateHangDumpCompleted(testHostResult);

        // Verify the dump file was created inside a subdirectory named after the assembly
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "*_hang.dmp", SearchOption.AllDirectories);
        string dumpFile = Assert.ContainsSingle(
            dumpFiles,
            $"Expected single dump file in '{resultDirectory}'\n{testHostResult}'");

        // The dump file should be in a subdirectory named after the assembly
        string? parentDir = Path.GetDirectoryName(dumpFile);
        Assert.IsNotNull(parentDir);
        Assert.AreNotEqual(resultDirectory, parentDir, "Dump file should be in a subdirectory created from the {asm} placeholder");
        Assert.AreEqual("HangDump", Path.GetFileName(parentDir),
            $"Subdirectory should be named after the assembly ('HangDump'). Actual: {Path.GetFileName(parentDir)}");
    }

    private static void AssertTemplateHangDumpCompleted(TestHostResult testHostResult)
    {
        testHostResult.AssertOutputContains("Hang dump timeout");

        // These template-focused tests only need to prove that hang dump triggered and the file was created.
        // On macOS, createdump can finish after the test host reports success, so the process may still exit with 0.
        Assert.IsTrue(
            testHostResult.ExitCode is (int)ExitCode.Success or (int)ExitCode.TestHostProcessExitedNonGracefully,
            $"Expected hang dump template scenarios to exit with {(int)ExitCode.Success} or {(int)ExitCode.TestHostProcessExitedNonGracefully}, but got {testHostResult.ExitCode}.{Environment.NewLine}{testHostResult}");
    }

    [TestMethod]
    public async Task HangDump_TemplateWithPathTraversal_RejectsAndFails()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-filename ../../outside/{{pname}}_hang.dmp --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                ["SLEEPTIMEMS1"] = "4000",
                ["SLEEPTIMEMS2"] = "20000",
            },
            cancellationToken: TestContext.CancellationToken);

        // The path-traversal guard should cause a non-graceful exit and no dump file should be created outside the results directory.
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        Assert.Contains("outside the results directory", testHostResult.StandardOutput);
    }

    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    [DataRow("None")]
    [TestMethod]
    public async Task HangDump_Formats_CreateDump(string format)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), format);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-type {format} --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        string? dumpFile = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        if (format != "None")
        {
            Assert.IsNotNull(dumpFile, $"Dump file not found '{format}'\n{testHostResult}'");
        }
        else
        {
            Assert.IsNull(dumpFile, $"Dump file was incorrectly created for None dump type.\n{testHostResult}'");
        }
    }

    [TestMethod]
    public async Task HangDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --hangdump-type invalid --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "600000" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("""
            Option '--hangdump-type' has invalid arguments: 'invalid' is not a valid dump type.
            Valid options are 'Mini', 'Heap', 'Triage', 'None' (only available in .NET 6+) and 'Full'
            """);
    }

    [TestMethod]
    public async Task HangDump_WithForegroundThreadAfterSessionFinish_CreateDump()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), TargetFrameworks.NetCurrent);
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "HangDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--hangdump --hangdump-timeout 8s --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "SLEEPTIMEMS1", "4000" },
                { "SLEEPTIMEMS2", "4000" },
                { "SPAWN_FOREGROUND_THREAD", "true" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string[] dumpFiles = Directory.GetFiles(resultDirectory, "HangDump*.dmp", SearchOption.AllDirectories);
        Assert.ContainsSingle(dumpFiles, $"Expected single dump file. Found: {Environment.NewLine}{string.Join(Environment.NewLine, dumpFiles)}{Environment.NewLine}{testHostResult}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetName = "AssetFixture";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        private const string Sources = """
#file HangDump.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        builder.AddHangDumpProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
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

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS1")!, CultureInfo.InvariantCulture));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        Thread.Sleep(int.Parse(Environment.GetEnvironmentVariable("SLEEPTIMEMS2")!, CultureInfo.InvariantCulture));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test2",
            DisplayName = "Test2",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        // Spawn a foreground thread that continues running after the test session finishes
        // to verify that hang dump triggers even after session end
        if (Environment.GetEnvironmentVariable("SPAWN_FOREGROUND_THREAD") == "true")
        {
            Thread foregroundThread = new Thread(() =>
            {
                Thread.Sleep(600000); // Sleep for 10 minutes to trigger hang dump
            });
            foregroundThread.IsBackground = false; // Foreground thread to prevent process exit
            foregroundThread.Start();
        }

        context.Complete();
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
