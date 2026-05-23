// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class CrashDumpTests : AcceptanceTestBase<CrashDumpTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CrashDump_DefaultSetting_CreateDump(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string? dumpFile = Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories).SingleOrDefault();
        Assert.IsNotNull(dumpFile, $"Dump file not found '{tfm}'\n{testHostResult}'");
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CrashDump_TesthostAndChildBothCrash_CollectsAllDumps(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", tfm);

        // This test exercises multi-process dump collection, not dump completeness. Use a Mini dump so
        // both crashing processes can finish writing within the child-process timeout on slower machines.
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --crashdump-type Mini --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "CRASHDUMP_SPAWN_CHILD_THAT_CRASHES", "1" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        // Both the testhost and its child process crash with FailFast and must produce a dump each.
        // Without the fix for https://github.com/microsoft/testfx/issues/4186, only the dump matching
        // the testhost's PID was reported as an artifact and the child dump was silently dropped.
        //
        // Filter by exact extension after the wildcard enumeration to defend against Windows' legacy
        // 8.3 short-name matching where the search pattern `CrashDump_*.dmp` can also match files
        // whose extension merely starts with `.dmp` (for example `CrashDump_xxx.dmp.crashreport.json`).
        string[] dumpFiles = [.. Directory
            .GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f).Equals(".dmp", StringComparison.OrdinalIgnoreCase))];
        Assert.HasCount(2, dumpFiles, $"Expected dumps for both the testhost and the child process '{tfm}'.\n{testHostResult}");

        // Both dumps must also be reported as out-of-process file artifacts so they show up to the user.
        testHostResult.AssertOutputContains("Out of process file artifacts produced:");
        foreach (string dumpFile in dumpFiles)
        {
            testHostResult.AssertOutputContains(Path.GetFileName(dumpFile));
        }
    }

    [TestMethod]
    public async Task CrashDump_CustomDumpName_CreateDump()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "customdumpname.dmp", SearchOption.AllDirectories), $"Dump file not found\n{testHostResult}");
    }

    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    [TestMethod]
    public async Task CrashDump_Formats_CreateDump(string format)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crashdump-type {format} --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        string dumpFile = Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories), $"Dump file not found '{format}'\n{testHostResult}");
        File.Delete(dumpFile);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report generation is not supported on Windows (dotnet/runtime#80191)")]
    public async Task CrashDump_WithCrashReport_CreateDumpAndCrashReport()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crash-report --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories), $"Dump file not found\n{testHostResult}");
        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp.crashreport.json", SearchOption.AllDirectories), $"Crash report file not found\n{testHostResult}");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report generation is not supported on Windows (dotnet/runtime#80191)")]
    public async Task CrashReport_DefaultSetting_CreatesOnlyCrashReport()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crash-report --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        Assert.IsEmpty(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp", SearchOption.AllDirectories), $"Unexpected dump file found\n{testHostResult}");
        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "CrashDump_*.dmp.crashreport.json", SearchOption.AllDirectories), $"Crash report file not found\n{testHostResult}");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report generation is not supported on Windows (dotnet/runtime#80191)")]
    public async Task CrashReport_WithCustomDumpFilename_CreatesOnlyCrashReport()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crash-report --crashdump-filename customdumpname.dmp --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        Assert.IsEmpty(Directory.GetFiles(resultDirectory, "customdumpname.dmp", SearchOption.AllDirectories), $"Unexpected dump file found\n{testHostResult}");
        Assert.ContainsSingle(Directory.GetFiles(resultDirectory, "customdumpname.dmp.crashreport.json", SearchOption.AllDirectories), $"Crash report file not found\n{testHostResult}");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates Windows-specific error for --crash-report")]
    public async Task CrashReport_OnWindows_FailsWithValidationError()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crash-report --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--crash-report' is not supported on Windows");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Skipped on non-Windows platforms because this validates Windows-specific rejection of --crashdump --crash-report")]
    public async Task CrashDump_WithCrashReport_OnWindows_FailsWithValidationError()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump --crash-report --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--crash-report' is not supported on Windows");
    }

    [TestMethod]
    public async Task CrashDump_InvalidFormat_ShouldFail()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--crashdump  --crashdump-type invalid --results-directory {resultDirectory}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--crashdump-type' has invalid arguments: 'invalid' is not a valid dump type. Valid options are 'Mini', 'Heap', 'Triage' and 'Full'");
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CrashDump_DefaultSetting_GeneratesSequenceFileListingRunningTests(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "CRASHDUMP_PUBLISH_INPROGRESS_TESTS", "MyTest1;MyTest2" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        string sequenceFile = Assert.ContainsSingle(
            Directory.GetFiles(resultDirectory, "*.sequence.log", SearchOption.AllDirectories),
            $"Crash sequence file not found '{tfm}'\n{testHostResult}");
        string content = await File.ReadAllTextAsync(sequenceFile, TestContext.CancellationToken);
        Assert.Contains("STARTED", content);
        Assert.Contains("MyTest1", content);
        Assert.Contains("MyTest2", content);

        // The friendly summary printed by the controller lists each running test by display name.
        testHostResult.AssertOutputContains("The following tests were still running when the test host crashed");
        testHostResult.AssertOutputContains("MyTest1");
        testHostResult.AssertOutputContains("MyTest2");
    }

    [TestMethod]
    public async Task CrashDump_SequenceOff_DoesNotGenerateSequenceFile()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --crash-sequence off --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "CRASHDUMP_PUBLISH_INPROGRESS_TESTS", "MyTest1" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        Assert.IsEmpty(
            Directory.GetFiles(resultDirectory, "*.sequence.log", SearchOption.AllDirectories),
            $"No sequence file expected when --crash-sequence off is set\n{testHostResult}");
        testHostResult.AssertOutputDoesNotContain("The following tests were still running when the test host crashed");
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CrashDump_GracefulExit_DeletesSequenceFile(string tfm)
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --results-directory {resultDirectory}",
            new Dictionary<string, string?>
            {
                { "CRASHDUMP_PUBLISH_INPROGRESS_TESTS", "MyTest1" },
                { "CRASHDUMP_EXIT_GRACEFULLY", "1" },
            },
            cancellationToken: TestContext.CancellationToken);

        // Even though tests were "started", the testhost exited cleanly so the sequence file has no
        // diagnostic value and must be cleaned up to avoid polluting the results directory.
        Assert.IsEmpty(
            Directory.GetFiles(resultDirectory, "*.sequence.log", SearchOption.AllDirectories),
            $"No sequence file expected on graceful exit '{tfm}'\n{testHostResult}");
    }

    [TestMethod]
    public async Task CrashSequence_InvalidArgument_ShouldFail()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --crash-sequence maybe --results-directory {resultDirectory}",
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("--crash-sequence expects a single parameter");
    }

    [TestMethod]
    public async Task CrashSequence_WithoutCrashDumpOrCrashReport_ShouldFail()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, "CrashDump", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crash-sequence on --results-directory {resultDirectory}",
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Add --crashdump or --crash-report");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetName = "CrashDumpFixture";

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        public string TargetAssetPath => GetAssetPath(AssetName);

        private const string Sources = """
#file CrashDump.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        // When invoked as a child process spawned by the testhost, just crash so we produce
        // an additional dump in the same directory using the dump env vars inherited from the parent.
        if (args.Length > 0 && args[0] == "--child-crash")
        {
            Environment.FailFast("ChildCrashDump");
#if NETFRAMEWORK
            // Environment.FailFast lacks the [DoesNotReturn] annotation on .NET Framework, so the
            // compiler still requires the method to return on all paths. On .NET 6+ this branch is
            // omitted to avoid an "unreachable code" warning.
            return 0;
#endif
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        builder.AddCrashDumpProvider();
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

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // Optionally publish a fake "in-progress" test node before crashing so the crash-sequence
        // extension has something to record. We do not publish a terminal state (passed/failed/...)
        // for these so the controller-side handler can verify them as "tests running at the time
        // of the crash".
        string? tests = Environment.GetEnvironmentVariable("CRASHDUMP_PUBLISH_INPROGRESS_TESTS");
        if (!string.IsNullOrEmpty(tests) && context.Request is Microsoft.Testing.Platform.Requests.RunTestExecutionRequest runRequest)
        {
            foreach (string testName in tests.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var node = new TestNode
                {
                    Uid = new TestNodeUid(testName),
                    DisplayName = testName,
                };
                node.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(runRequest.Session.SessionUid, node));
            }

            // MessageBus.PublishAsync only enqueues; the async consumer pipeline may not have run
            // yet. Give the CrashDumpSequenceLogger a brief opportunity to drain and flush before
            // we crash so the sequence file actually contains the STARTED entries this test asserts
            // on. This is a test-asset workaround for what is, in production, a best-effort feature
            // (the message bus is intentionally asynchronous for throughput reasons).
            await Task.Delay(2000);
        }

        // Optionally spawn a child process that also crashes (and produces its own dump) so we can
        // exercise the crashdump extension's ability to collect dumps from child processes.
        if (Environment.GetEnvironmentVariable("CRASHDUMP_SPAWN_CHILD_THAT_CRASHES") == "1")
        {
            // Prefer Environment.ProcessPath (available since .NET 6) over Process.MainModule.FileName
            // so we avoid loading the process module and any platform-specific failure modes that come
            // with it. Fall back to MainModule for older runtimes.
#if NET6_0_OR_GREATER
            string? path = Environment.ProcessPath;
#else
            string? path = null;
#endif
            if (string.IsNullOrEmpty(path))
            {
                using Process self = Process.GetCurrentProcess();
                path = self.MainModule!.FileName!;
            }

            string fileName = Path.GetFileName(path);
            bool isDotnetMuxer = string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase);

            var psi = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
            };
#if NET6_0_OR_GREATER
            // Use ArgumentList instead of a single argument string so the runtime quotes/escapes each
            // argument correctly across Windows and Unix; this also makes the test asset robust to
            // paths that contain spaces or special characters.
            if (isDotnetMuxer)
            {
                psi.ArgumentList.Add("exec");
                psi.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
            }

            psi.ArgumentList.Add("--child-crash");
#else
            // .NET Framework does not have ProcessStartInfo.ArgumentList; fall back to a manually
            // quoted argument string. This branch only compiles for net462; the test that exercises
            // child-process crashes does not run on .NET Framework, but the asset must still compile
            // for every TFM listed in `TargetFrameworks.All`.
            if (isDotnetMuxer)
            {
                psi.Arguments = "exec \"" + Assembly.GetEntryAssembly()!.Location + "\" --child-crash";
            }
            else
            {
                psi.Arguments = "--child-crash";
            }
#endif

            using Process child = Process.Start(psi)!;

            // Wait for the child to fully exit (with a bounded timeout to avoid hanging the test run)
            // so its crash dump is written before we crash too.
            if (!child.WaitForExit(60_000))
            {
                try
                {
                    child.Kill();
                }
                catch
                {
                    // Best effort: process may have just exited.
                }

                throw new InvalidOperationException("Child process did not exit within the expected timeout (60s).");
            }
        }

        // Optionally exit gracefully instead of crashing so we can assert that the sequence file is
        // cleaned up when no crash occurs.
        if (Environment.GetEnvironmentVariable("CRASHDUMP_EXIT_GRACEFULLY") == "1")
        {
            context.Complete();
            return;
        }

        Environment.FailFast("CrashDump");
        context.Complete();
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
