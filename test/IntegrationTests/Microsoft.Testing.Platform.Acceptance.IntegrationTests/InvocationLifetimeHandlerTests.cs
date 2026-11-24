// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class InvocationLifetimeHandlerTests : AcceptanceTestBase<InvocationLifetimeHandlerTests.TestAssetFixture>
{
    private const string AssetName = "InvocationLifetimeHandler";
    private const string Test = "test:{0}:value-A:value-B:no-marker:{1}";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task ConsoleHost_RunsSetupBeforeTheTestsAndCleanupAfterThemInReverseOrder(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new() { ["LOG_FILE"] = logFile },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                string.Format(CultureInfo.InvariantCulture, Test, owner, "none"),
                $"completed:B:{owner}:0:False",
                $"completed:A:{owner}:0:False",
            ],
            log,
            testHostResult.ToString());
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task TestHostController_RunsTheHandlersInTheControllerAndExposesTheirVariablesToTheTestHost(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["LOG_FILE"] = logFile,
                ["USE_CONTROLLER"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        string child = GetPid(log[2]);
        Assert.AreNotEqual(owner, child, testHostResult.ToString());
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                string.Format(CultureInfo.InvariantCulture, Test, child, "none"),
                $"completed:B:{owner}:0:False",
                $"completed:A:{owner}:0:False",
            ],
            log,
            testHostResult.ToString());
    }

    [TestMethod]
    public async Task TestHostController_TheTestHostIsAParticipantEvenWithoutTheOwnerMarker()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["DROP_OWNER_MARKER"] = "1",
                ["LOG_FILE"] = logFile,
                ["USE_CONTROLLER"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        string child = GetPid(log[2]);
        Assert.AreNotEqual(owner, child, testHostResult.ToString());
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                string.Format(CultureInfo.InvariantCulture, Test, child, "none"),
                $"completed:B:{owner}:0:False",
                $"completed:A:{owner}:0:False",
            ],
            log,
            testHostResult.ToString());
    }

    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [TestMethod]
    public async Task Retry_RunsTheHandlersOnceAroundAllTheAttempts(bool useController, bool dropOwnerEnvironment)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--retry-failed-tests 2",
            new()
            {
                ["FAIL_FIRST_ATTEMPT"] = "1",
                ["DROP_RETRY_OWNER_ENVIRONMENT"] = dropOwnerEnvironment ? "1" : "0",
                ["LOG_FILE"] = logFile,
                ["USE_CONTROLLER"] = useController ? "1" : "0",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        string[] log = ReadLog(logFile, testHostResult.ToString());
        Assert.HasCount(6, log, string.Join(Environment.NewLine, log));
        string owner = GetPid(log[0]);
        string firstAttempt = GetPid(log[2]);
        string secondAttempt = GetPid(log[3]);
        string values = dropOwnerEnvironment ? "<null>:<null>" : "value-A:value-B";
        Assert.AreNotEqual(owner, firstAttempt, testHostResult.ToString());
        Assert.AreNotEqual(owner, secondAttempt, testHostResult.ToString());
        Assert.AreNotEqual(firstAttempt, secondAttempt, testHostResult.ToString());
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                $"test:{firstAttempt}:{values}:no-marker:1",
                $"test:{secondAttempt}:{values}:no-marker:2",
                $"completed:B:{owner}:0:False",
                $"completed:A:{owner}:0:False",
            ],
            log,
            testHostResult.ToString());
    }

    [TestMethod]
    public async Task FailedTests_AreReportedToTheCleanup()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["FAIL"] = "1",
                ["LOG_FILE"] = logFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                string.Format(CultureInfo.InvariantCulture, Test, owner, "none"),
                $"completed:B:{owner}:2:False",
                $"completed:A:{owner}:2:False",
            ],
            log,
            testHostResult.ToString());
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SetupFailure_SkipsTheTestsAndCleansUpTheEnteredHandlers(bool useController)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["LOG_FILE"] = logFile,
                ["THROW_IN_SETUP"] = "B",
                ["USE_CONTROLLER"] = useController ? "1" : "0",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.GenericFailure);
        testHostResult.AssertOutputContains("Invocation lifetime handler 'HandlerB' failed before the tests ran, so no test ran");
        testHostResult.AssertOutputContains("Setup failure from B");
        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                $"completed:B:{owner}:1:False",
                $"completed:A:{owner}:1:False",
            ],
            log,
            testHostResult.ToString());
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task CleanupFailure_FailsTheInvocationAndStillCleansUpTheOtherHandlers(bool useController)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["LOG_FILE"] = logFile,
                ["THROW_IN_CLEANUP"] = "B",
                ["USE_CONTROLLER"] = useController ? "1" : "0",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.GenericFailure);
        testHostResult.AssertOutputContains("Invocation lifetime handler 'HandlerB' failed after the tests ran");
        testHostResult.AssertOutputContains("Cleanup failure from B");
        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        string testProcess = GetPid(log[2]);
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                string.Format(CultureInfo.InvariantCulture, Test, testProcess, "none"),
                $"completed:B:{owner}:0:False",
                $"completed:A:{owner}:0:False",
            ],
            log,
            testHostResult.ToString());
    }

    [TestMethod]
    public async Task TestHostController_SetupTimeoutCancelsBlockedHandlerAndReportsCanceledCleanup()
        => await AssertOwnerSetupTimeoutAsync("--timeout 5s", useController: true);

    [TestMethod]
    public async Task RetryOrchestrator_SetupTimeoutCancelsBlockedHandlerAndReportsCanceledCleanup()
        => await AssertOwnerSetupTimeoutAsync("--retry-failed-tests 2 --timeout 5s", useController: false);

    [TestMethod]
    public async Task Timeout_ReportsTheCanceledInvocationToTheCleanup()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--timeout 5s",
            new()
            {
                ["BLOCK"] = "1",
                ["LOG_FILE"] = logFile,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestSessionAborted);

        // The timeout starts while the application is built, so on a slow machine it can expire before the first
        // handler, the second handler, or the test starts. No handler entered means nothing to clean up and no log.
        // Every handler that was entered must still be cleaned up as canceled.
        if (!File.Exists(logFile))
        {
            return;
        }

        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        string[] entered = [.. log.TakeWhile(line => line.StartsWith("starting:", StringComparison.Ordinal)).Select(line => line.Split(':')[1])];
        List<string> expected = [.. entered.Select(name => $"starting:{name}:{owner}")];
        if (log.Length > entered.Length && log[entered.Length].StartsWith("test:", StringComparison.Ordinal))
        {
            expected.Add(string.Format(CultureInfo.InvariantCulture, Test, owner, "none"));
        }

        expected.AddRange(Enumerable.Reverse(entered).Select(name => $"completed:{name}:{owner}:3:True"));
        AssertLog([.. expected], log, testHostResult.ToString());
    }

    [DataRow("--list-tests")]
    [DataRow("--help")]
    [DataRow("--info")]
    [TestMethod]
    public async Task CommandsThatDoNotRunTests_DoNotRunTheHandlers(string command)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            command,
            new() { ["LOG_FILE"] = logFile },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        Assert.IsFalse(File.Exists(logFile), testHostResult.ToString());
    }

    [DataRow("exec")]
    [DataRow("run")]
    [DataRow("test")]
    [TestMethod]
    public async Task DotnetCommands_RunTheHandlersOnce(string command)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);
        string arguments = command switch
        {
            "exec" => $"exec \"{Path.Combine(testHost.DirectoryName, AssetName + ".dll")}\"",
            "run" => $"run --project \"{AssetFixture.TargetAssetPath}\" --no-build -c Release -f {TargetFrameworks.NetCurrent}",
            _ => $"test --project \"{AssetFixture.TargetAssetPath}\" --no-build -c Release -f {TargetFrameworks.NetCurrent}",
        };

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            arguments,
            workingDirectory: AssetFixture.TargetAssetPath,
            environmentVariables: new() { ["LOG_FILE"] = logFile },
            failIfReturnValueIsNotZero: false,
            warnAsError: command == "test",
            suppressPreviewDotNetMessage: command == "test",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        string[] log = ReadLog(logFile, result.ToString());
        string owner = GetPid(log[0]);
        AssertLog(
            [
                $"starting:A:{owner}",
                $"starting:B:{owner}",
                string.Format(CultureInfo.InvariantCulture, Test, owner, "none"),
                $"completed:B:{owner}:0:False",
                $"completed:A:{owner}:0:False",
            ],
            log,
            result.ToString());
    }

    private static string GetLogFile(TestInfrastructure.TestHost testHost)
        => Path.Combine(testHost.DirectoryName, $"{Guid.NewGuid():N}.log");

    private async Task AssertOwnerSetupTimeoutAsync(string arguments, bool useController)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string logFile = GetLogFile(testHost);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            arguments,
            new()
            {
                ["BLOCK_SETUP"] = "A",
                ["LOG_FILE"] = logFile,
                ["USE_CONTROLLER"] = useController ? "1" : "0",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestSessionAborted);
        string[] log = ReadLog(logFile, testHostResult.ToString());
        string owner = GetPid(log[0]);
        AssertLog(
            [
                $"starting:A:{owner}",
                $"waiting:A:{owner}",
                $"completed:A:{owner}:3:True",
            ],
            log,
            testHostResult.ToString());
    }

    private static string[] ReadLog(string logFile, string output)
    {
        Assert.IsTrue(File.Exists(logFile), output);
        string[] log = File.ReadAllLines(logFile);
        Assert.IsNotEmpty(log, output);
        return log;
    }

    private static string GetPid(string line)
    {
        string[] parts = line.Split(':');
        return parts[0] == "test" ? parts[1] : parts[2];
    }

    private static void AssertLog(string[] expected, string[] actual, string output)
        => Assert.AreEqual(string.Join(Environment.NewLine, expected), string.Join(Environment.NewLine, actual), output);

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file InvocationLifetimeHandler.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
#pragma warning disable TPEXP // The invocation lifetime API is experimental.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.InvocationLifetime;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Requests;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // AUMID activation preserves the retry arguments, but not the owner's process environment.
        if (Environment.GetEnvironmentVariable("DROP_RETRY_OWNER_ENVIRONMENT") == "1"
            && Array.IndexOf(args, "--internal-retry-pipename") >= 0)
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER", null);
            Environment.SetEnvironmentVariable("INVOCATION_VALUE_A", null);
            Environment.SetEnvironmentVariable("INVOCATION_VALUE_B", null);
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
        builder.AddRetryProvider();
        builder.TestHostControllers.AddEnvironmentVariableProvider(_ => new OptionalController());
        IInvocationLifetimeManager invocationLifetime = ((IInvocationLifetimeApplicationBuilder)builder).InvocationLifetime;
        invocationLifetime.AddInvocationLifetimeHandler(_ => new LoggingHandler("A"));
        invocationLifetime.AddInvocationLifetimeHandler(_ => new LoggingHandler("B"));
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

internal static class Log
{
    public static string ProcessId { get; } = Process.GetCurrentProcess().Id.ToString();

    public static void Write(string line)
    {
        string? logFile = Environment.GetEnvironmentVariable("LOG_FILE");
        if (!string.IsNullOrEmpty(logFile))
        {
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
    }
}

internal sealed class LoggingHandler(string name) : IInvocationLifetimeHandler
{
    public string Uid => "Handler" + name;

    public string Version => "1.0.0";

    public string DisplayName => Uid;

    public string Description => Uid;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task OnInvocationStartingAsync(InvocationStartingContext context, CancellationToken cancellationToken)
    {
        Log.Write($"starting:{name}:{Log.ProcessId}");
        context.SetEnvironmentVariable("INVOCATION_VALUE_" + name, "value-" + name);
        if (Environment.GetEnvironmentVariable("THROW_IN_SETUP") == name)
        {
            throw new InvalidOperationException("Setup failure from " + name);
        }

        if (Environment.GetEnvironmentVariable("BLOCK_SETUP") == name)
        {
            Log.Write($"waiting:{name}:{Log.ProcessId}");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    public Task OnInvocationCompletedAsync(InvocationCompletedContext context, CancellationToken cancellationToken)
    {
        Log.Write($"completed:{name}:{Log.ProcessId}:{context.ExitCode}:{context.IsCanceled}");
        if (Environment.GetEnvironmentVariable("THROW_IN_CLEANUP") == name)
        {
            throw new InvalidOperationException("Cleanup failure from " + name);
        }

        return Task.CompletedTask;
    }
}

internal sealed class OptionalController : ITestHostEnvironmentVariableProvider
{
    public string Uid => nameof(OptionalController);

    public string Version => "1.0.0";

    public string DisplayName => nameof(OptionalController);

    public string Description => nameof(OptionalController);

    public Task<bool> IsEnabledAsync() => Task.FromResult(Environment.GetEnvironmentVariable("USE_CONTROLLER") == "1");

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        // Simulates a test host that does not inherit the environment of the controller, for example an
        // AUMID-activated packaged application: the owner marker does not reach it.
        if (Environment.GetEnvironmentVariable("DROP_OWNER_MARKER") == "1")
        {
            environmentVariables.SetVariable(new EnvironmentVariable("TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER", string.Empty, isSecret: false, isLocked: false));
        }

        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => ValidationResult.ValidTask;
}

internal sealed class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "1.0.0";

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
        if (context.Request is not RunTestExecutionRequest)
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
            {
                Uid = "Test1",
                DisplayName = "Test1",
                Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
            }));
            context.Complete();
            return;
        }

        string? attempt = Environment.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER");
        string marker = Environment.GetEnvironmentVariable("TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER") is null ? "no-marker" : "marker";
        Log.Write($"test:{Log.ProcessId}:{Environment.GetEnvironmentVariable("INVOCATION_VALUE_A") ?? "<null>"}:{Environment.GetEnvironmentVariable("INVOCATION_VALUE_B") ?? "<null>"}:{marker}:{attempt ?? "none"}");

        if (Environment.GetEnvironmentVariable("BLOCK") == "1"
            && context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30)))
        {
            context.Complete();
            return;
        }

        bool fail = Environment.GetEnvironmentVariable("FAIL") == "1"
            || (Environment.GetEnvironmentVariable("FAIL_FIRST_ATTEMPT") == "1" && attempt is null or "1");
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(fail ? new FailedTestNodeStateProperty() : new PassedTestNodeStateProperty()),
        }));

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources
            .PatchTargetFrameworks(TargetFrameworks.All)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
