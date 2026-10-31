// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.TestHostOrchestrator;
using Microsoft.Testing.Platform.Tools;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestApplicationCreationTests
{
    [TestMethod]
    [DataRow("builder-diagnostics-preferred", "preferred")]
    [DataRow("builder-diagnostics-legacy", "legacy")]
    public async Task CreateBuilder_DiagnosticsHonorsLayeredOverridesAndLegacyCompatibility(string mode, string expectedPrefix)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync(mode);
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("log-folder:environment", records);
        Assert.StartsWith(
            "log-name:" + expectedPrefix + "_",
            Assert.ContainsSingle(records.Where(record => record.StartsWith("log-name:", StringComparison.Ordinal))));
        Assert.Contains("log:Logging mode: synchronous", records);
        Assert.Contains("log:Logging level: Debug", records);
        Assert.Contains("log:TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID_17 'correlation-value'", records);
        Assert.Contains("log:TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID_17 'parent-value'", records);
        Assert.Contains("log:TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME_17 'start-value'", records);
        Assert.Contains("log-released:True", records);
        Assert.Contains("TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX", child.Output);
        Assert.Contains("TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE", child.Output);
    }

    [TestMethod]
    public async Task CreateBuilder_DynamicManifestFailurePreservesErrorAndFlushesDiagnosticLog()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildInCopyAsync("builder-dynamic-failure");
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("failure-type:System.InvalidOperationException", records);
        Assert.Contains(
            "invalid.testingplatformextensions.json",
            Assert.ContainsSingle(records.Where(record => record.StartsWith("failure-message:", StringComparison.Ordinal))));
        Assert.EndsWith(
            "invalid.testingplatformextensions.json'.",
            Assert.ContainsSingle(records.Where(record => record.StartsWith("log:Reading extension manifest '", StringComparison.Ordinal))));
        Assert.Contains("log-released:True", records);
    }

    [TestMethod]
    [DataRow("help", 0)]
    [DataRow("info", 0)]
    [DataRow("invalid-json", 5)]
    [DataRow("tool", 43)]
    [DataRow("tool-invalid-json", 43)]
    [DataRow("server-build", 0)]
    [DataRow("legacy-long", 0)]
    [DataRow("legacy-short", 0)]
    public async Task BuildAsync_InformationalInvalidAndToolModesPreserveExitAndLazyFramework(string mode, int expectedExit)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-mode-" + mode);
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("result:" + expectedExit.ToString(CultureInfo.InvariantCulture), records);
        Assert.Contains("framework-created:0", records);
        Assert.Contains("capability-initialized:1", records);
        Assert.Contains(mode.StartsWith("tool", StringComparison.Ordinal) ? "tool-called:1" : "tool-called:0", records);
    }

    [TestMethod]
    public async Task RunAsync_ExternalCancellationReachesLifetimeAndPreservesAbortedVerdict()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-mode-external-cancel");
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("result:3", records);
        Assert.Contains("before-calls:1", records);
        Assert.Contains("before-token-canceled:True", records);
        Assert.Contains("lifetime-cleaned:1", records);
    }

    [TestMethod]
    public async Task BuildAsync_LongTimeoutCompletesOneRealPublishedResult()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-mode-long-timeout");
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("result:0", records);
        Assert.Contains("framework-created:1", records);
        Assert.Contains("published:1", records);
        Assert.Contains("before-calls:1", records);
        Assert.Contains("after-calls:1", records);
        Assert.Contains("lifetime-cleaned:1", records);
    }

    [TestMethod]
    public async Task TestApplication_DelegatesRunAndDisposesInnerHost()
    {
        Mock<IHost> host = new();
        host.Setup(h => h.RunAsync()).ReturnsAsync(42);
        Mock<IDisposable> disposal = host.As<IDisposable>();
        TestApplication application = new(host.Object);

        Assert.AreEqual(42, await application.RunAsync());
        application.Dispose();

        host.Verify(h => h.RunAsync(), Times.Once);
        disposal.Verify(d => d.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task TestApplication_InnerFailureIsNotWrapped()
    {
        InvalidOperationException expected = new("inner host failure");
        Mock<IHost> host = new();
        host.Setup(h => h.RunAsync()).ThrowsAsync(expected);
        using TestApplication application = new(host.Object);

        Assert.AreSame(expected, await Assert.ThrowsExactlyAsync<InvalidOperationException>(application.RunAsync));
    }

    [TestMethod]
    public Task CreateBuilder_DebuggerWaitPrintsOwnedProcessIdentityBeforeBlocking()
        => SystemWrappersTests.ObserveOwnedDebuggerWaitAsync("builder-wait-debugger");

    [TestMethod]
    [DataRow("disabled")]
    [DataRow("parse-error")]
    public async Task CreateBuilder_DiagnosticDisableAndParserErrorDoNotCreateLogger(string kind)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-no-log-" + kind);
        Assert.AreEqual(0, child.ExitCode, child.Error);
        Assert.AreSequenceEqual(["log-count:0"], ReadRecords(child.Output));
    }

    private static string[] ReadRecords(string output)
        => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'))
            .Where(line => line.StartsWith("PROBE|", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["PROBE|".Length..])))
            .ToArray();

    internal static async Task RunBuilderChildAsync(string mode, Action<string> record)
    {
        string root = Path.Combine(Path.GetTempPath(), "core-builder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ClearInheritedSettings();
            if (mode == "builder-wait-debugger")
            {
                Environment.SetEnvironmentVariable("TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER", "1");
                await TestApplication.CreateBuilderAsync([]);
                Assert.Fail("Debugger wait unexpectedly returned without a debugger.");
            }
            else if (mode.StartsWith("builder-no-log-", StringComparison.Ordinal))
            {
                List<string> args = ["--diagnostic", "--diagnostic-output-directory", root];
                if (mode == "builder-no-log-parse-error")
                {
                    args.Add("@" + Path.Combine(root, "absent.rsp"));
                }

                ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([.. args]);
                await ((IAsyncCleanableExtension)builder).CleanupAsync();
                record("log-count:" + Directory.GetFiles(root, "*.diag", SearchOption.AllDirectories).Length.ToString(CultureInfo.InvariantCulture));
            }
            else if (mode.StartsWith("builder-diagnostics-", StringComparison.Ordinal) || mode == "builder-dynamic-failure")
            {
                await RunDiagnosticChildAsync(mode, root, record);
            }
            else if (mode.StartsWith("builder-routing-", StringComparison.Ordinal))
            {
                await RunRoutingChildAsync(mode["builder-routing-".Length..], root, record);
            }
            else
            {
                await RunModeChildAsync(mode["builder-mode-".Length..], root, record);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void ClearInheritedSettings()
    {
        foreach (string name in new[]
        {
            "TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER", "TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER",
            "TESTINGPLATFORM_DEADLINE", "TESTINGPLATFORM_DIAGNOSTIC_FILE_PREFIX",
            "TESTINGPLATFORM_DIAGNOSTIC_SYNCHRONOUS_WRITE", "TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY",
            "TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY", "TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX",
            "TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE",
        })
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC", "0");
    }

    private static async Task RunDiagnosticChildAsync(string mode, string root, Action<string> record)
    {
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC", "1");
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY", "debug");
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY", Path.Combine(root, "environment"));
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX", "legacy");
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE", "1");
        if (mode != "builder-diagnostics-legacy")
        {
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_FILE_PREFIX", "preferred");
            Environment.SetEnvironmentVariable("TESTINGPLATFORM_DIAGNOSTIC_SYNCHRONOUS_WRITE", "1");
        }

        Environment.SetEnvironmentVariable("TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID_17", "correlation-value");
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID_17", "parent-value");
        Environment.SetEnvironmentVariable("TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME_17", "start-value");
        List<string> args =
        [
            "--no-banner", "--results-directory", Path.Combine(root, "results"),
            "--diagnostic-output-directory", Path.Combine(root, "cli"),
            "--diagnostic-file-prefix", "cli", "--diagnostic-verbosity", "warning",
            "--internal-testhostcontroller-pid", "17",
        ];
        if (mode != "builder-diagnostics-legacy")
        {
            args.Add("--diagnostic");
        }

        if (mode == "builder-dynamic-failure")
        {
            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(typeof(TestApplicationCreationTests).Assembly.Location)!, "invalid.testingplatformextensions.json"),
                """{"extensions":42}""");
            args.Add("--enable-dynamic-extensions");
            try
            {
                await TestApplication.CreateBuilderAsync([.. args]);
                Assert.Fail("An invalid opted-in manifest must reject builder creation.");
            }
            catch (InvalidOperationException failure)
            {
                record("failure-type:" + failure.GetType().FullName);
                record("failure-message:" + failure.Message);
            }
        }
        else
        {
            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([.. args]);
            await ((IAsyncCleanableExtension)builder).CleanupAsync();
        }

        string log = Assert.ContainsSingle(Directory.GetFiles(root, "*.diag", SearchOption.AllDirectories));
        record("log-folder:" + Path.GetFileName(Path.GetDirectoryName(log)));
        record("log-name:" + Path.GetFileName(log));
        foreach (string line in File.ReadAllLines(log))
        {
            System.Text.RegularExpressions.Match match = Regex.Match(line, @"^\[[^\]]+\] (?<message>.*)$");
            if (!match.Success)
            {
                match = Regex.Match(line, @"^\S+ \S+ (?:TRACE|DEBUG|INFORMATION|WARNING|ERROR|CRITICAL) (?<message>.*)$");
            }

            if (match.Success)
            {
                record("log:" + match.Groups["message"].Value);
            }
        }

        string moved = log + ".closed";
        File.Move(log, moved);
        record("log-released:" + File.Exists(moved));
    }

    private static async Task RunModeChildAsync(
        string mode,
        string root,
        Action<string> record,
        string[]? extraArguments = null,
        Action<ITestApplicationBuilder>? configureBuilder = null)
    {
        string configuration = Path.Combine(root, "testconfig.json");
        bool invalidJson = mode is "invalid-json" or "tool-invalid-json";
        File.WriteAllText(configuration, invalidJson ? """{"commandLineOptions":true}""" : "{}");
        List<string> args = [];
        if (mode.StartsWith("tool", StringComparison.Ordinal))
        {
            args.Add("coverage-fixture-tool");
        }

        args.AddRange(["--no-banner", "--config-file", configuration, "--results-directory", Path.Combine(root, "results")]);
        if (mode is "help" or "legacy-long" or "legacy-short")
        {
            args.Add("--help");
        }
        else if (mode == "info")
        {
            args.Add("--info");
        }
        else if (mode == "server-build")
        {
            args.AddRange(["--server", "jsonrpc", "--client-port", "1"]);
        }
        else if (mode == "long-timeout")
        {
            args.AddRange(["--timeout", "1h"]);
        }

        args.AddRange(extraArguments ?? []);
        using CancellationTokenSource external = new();
        TestApplicationOptions options = new() { EnableTelemetry = false, CancellationToken = mode == "external-cancel" ? external.Token : default };
        options.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource = false;
        ITestApplicationBuilder builder;
        if (mode is "legacy-long" or "legacy-short")
        {
            args.Add(mode == "legacy-long" ? "--server" : "-server");
#pragma warning disable CS0618, MTP0002 // Verify the retained legacy public entry point.
            builder = await TestApplication.CreateServerModeBuilderAsync([.. args], options);
#pragma warning restore CS0618, MTP0002
        }
        else
        {
            builder = await TestApplication.CreateBuilderAsync([.. args], options);
        }

        int initialized = 0;
        int created = 0;
        int toolCalled = 0;
        int published = 0;
        int before = 0;
        int after = 0;
        int cleaned = 0;
        bool canceledBefore = false;
        Mock<ITestFrameworkCapabilities> capabilities = new();
        capabilities.SetupGet(c => c.Capabilities).Returns([]);
        capabilities.As<IAsyncInitializableExtension>().Setup(c => c.InitializeAsync()).Callback(() => initialized++).Returns(Task.CompletedTask);
        Mock<ITestFramework> framework = new();
        framework.SetupGet(f => f.Uid).Returns("coverage-fixture-framework");
        framework.SetupGet(f => f.Version).Returns("1.0");
        framework.SetupGet(f => f.DisplayName).Returns("Coverage fixture");
        framework.SetupGet(f => f.Description).Returns("Publishes one passing node");
        framework.Setup(f => f.IsEnabledAsync()).ReturnsAsync(true);
        Mock<IDataProducer> producer = framework.As<IDataProducer>();
        producer.SetupGet(p => p.DataTypesProduced).Returns([typeof(TestNodeUpdateMessage)]);
        framework.Setup(f => f.CreateTestSessionAsync(It.IsAny<CreateTestSessionContext>())).ReturnsAsync(new CreateTestSessionResult { IsSuccess = true });
        framework.Setup(f => f.CloseTestSessionAsync(It.IsAny<CloseTestSessionContext>())).ReturnsAsync(new CloseTestSessionResult { IsSuccess = true });
        framework.Setup(f => f.ExecuteRequestAsync(It.IsAny<ExecuteRequestContext>())).Returns<ExecuteRequestContext>(async context =>
        {
            TestExecutionRequest request = Assert.IsInstanceOfType<TestExecutionRequest>(context.Request);
            await context.MessageBus.PublishAsync(producer.Object, new TestNodeUpdateMessage(
                request.Session.SessionUid,
                new TestNode { Uid = "one-passing-node", DisplayName = "One passing node", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
            published++;
            context.Complete();
        });
        builder.RegisterTestFramework(_ => capabilities.Object, (_, _) =>
        {
            created++;
            return framework.Object;
        });
        Mock<ITestHostApplicationLifetime> lifetime = new();
        lifetime.SetupGet(l => l.Uid).Returns("coverage-fixture-lifetime");
        lifetime.SetupGet(l => l.Version).Returns("1.0");
        lifetime.Setup(l => l.IsEnabledAsync()).ReturnsAsync(true);
        lifetime.Setup(l => l.BeforeRunAsync(It.IsAny<CancellationToken>())).Callback<CancellationToken>(token =>
        {
            before++;
            if (mode == "external-cancel")
            {
                external.Cancel();
            }

            canceledBefore = token.IsCancellationRequested;
        }).Returns(Task.CompletedTask);
        lifetime.Setup(l => l.AfterRunAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Callback(() => after++).Returns(Task.CompletedTask);
        lifetime.As<IAsyncCleanableExtension>().Setup(l => l.CleanupAsync()).Callback(() => cleaned++).Returns(Task.CompletedTask);
        builder.TestHost.AddTestHostApplicationLifetime(_ => lifetime.Object);
        Mock<ITool> tool = new();
        tool.SetupGet(t => t.Uid).Returns("coverage-fixture-tool-uid");
        tool.SetupGet(t => t.Name).Returns("coverage-fixture-tool");
        tool.SetupGet(t => t.Version).Returns("1.0");
        tool.SetupGet(t => t.DisplayName).Returns("Coverage fixture tool");
        tool.SetupGet(t => t.Description).Returns("Returns forty-three");
        tool.Setup(t => t.IsEnabledAsync()).ReturnsAsync(true);
        tool.Setup(t => t.RunAsync(It.IsAny<CancellationToken>())).Callback(() => toolCalled++).ReturnsAsync(43);
        ((TestApplicationBuilder)builder).Tools.AddTool(_ => tool.Object);
        configureBuilder?.Invoke(builder);
        using ITestApplication application = await builder.BuildAsync();
        int result = mode is "server-build" or "build-only" ? 0 : await application.RunAsync();
        record("result:" + result.ToString(CultureInfo.InvariantCulture));
        record("framework-created:" + created.ToString(CultureInfo.InvariantCulture));
        record("capability-initialized:" + initialized.ToString(CultureInfo.InvariantCulture));
        record("tool-called:" + toolCalled.ToString(CultureInfo.InvariantCulture));
        record("published:" + published.ToString(CultureInfo.InvariantCulture));
        record("before-calls:" + before.ToString(CultureInfo.InvariantCulture));
        record("after-calls:" + after.ToString(CultureInfo.InvariantCulture));
        record("lifetime-cleaned:" + cleaned.ToString(CultureInfo.InvariantCulture));
        record("before-token-canceled:" + canceledBefore);
    }

    [TestMethod]
    [DataRow("orchestrator", 17)]
    [DataRow("controller-build", 0)]
    public async Task BuildAsync_RoutedOrchestratorAndControllerKeepFrameworkLazy(string mode, int expectedExit)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-routing-" + mode);
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("result:" + expectedExit.ToString(CultureInfo.InvariantCulture), records);
        Assert.Contains("framework-created:0", records);
        if (mode == "orchestrator")
        {
            Assert.Contains("orchestrator-before:1", records);
            Assert.Contains("orchestrator-after:1", records);
            Assert.Contains("orchestrator-disposed:1", records);
        }
        else
        {
            Assert.Contains("controller-role:True", records);
            Assert.Contains("provider-created:1", records);
        }
    }

    [TestMethod]
    [DataRow("controlled-console", 0, 1)]
    [DataRow("controlled-cancel", 3, 0)]
    [DataRow("controlled-server", 0, 0)]
    public async Task BuildAsync_ControlledHostReportsCompletionAndNoncooperativeCancellation(string mode, int exitCode, int completions)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-routing-" + mode);
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("result:" + exitCode.ToString(CultureInfo.InvariantCulture), records);
        Assert.Contains("pid-requests:1", records);
        Assert.Contains("completions:" + completions.ToString(CultureInfo.InvariantCulture), records);
        Assert.Contains("primary-disconnected:True", records);
    }

    [TestMethod]
    public async Task BuildAsync_ControlledFactoryFailureClosesBothOwnedConnections()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-routing-controlled-failure");
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.AreSequenceEqual(
            ["failure:owned filter factory", "pid-requests:1", "completions:0", "primary-disconnected:True"],
            records);
    }

    [TestMethod]
    public async Task BuildAsync_RegisteredConsoleFactoriesInitializeWithoutFrameworkConstruction()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-routing-factories");
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("result:0", records);
        Assert.Contains("framework-created:0", records);
        Assert.Contains("filter-created:1", records);
        Assert.Contains("invoker-created:1", records);
    }

    internal static async Task RunRoutingChildAsync(string mode, string root, Action<string> record)
    {
        if (mode.StartsWith("help-pipe-", StringComparison.Ordinal))
        {
            await RunHelpPipeRoutingChildAsync(mode == "help-pipe-compatible", root, record);
            return;
        }

        if (mode.StartsWith("controlled-", StringComparison.Ordinal))
        {
            await RunControlledRoutingChildAsync(mode, root, record);
            return;
        }

        if (mode == "orchestrator")
        {
            int before = 0;
            int after = 0;
            int disposed = 0;
            Mock<ITestHostExecutionOrchestrator> orchestrator = new();
            orchestrator.As<ITestHostControllerConnectionAuthorizationConsumer>();
            orchestrator.SetupGet(o => o.Uid).Returns("routing-orchestrator");
            orchestrator.Setup(o => o.IsEnabledAsync()).ReturnsAsync(true);
            orchestrator.Setup(o => o.OrchestrateTestHostExecutionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(17);
            Mock<ITestHostOrchestratorApplicationLifetime> lifetime = new();
            lifetime.SetupGet(l => l.Uid).Returns("routing-orchestrator-lifetime");
            lifetime.Setup(l => l.IsEnabledAsync()).ReturnsAsync(true);
            lifetime.Setup(l => l.BeforeRunAsync(It.IsAny<CancellationToken>())).Callback(() => before++).Returns(Task.CompletedTask);
            lifetime.Setup(l => l.AfterRunAsync(17, It.IsAny<CancellationToken>())).Callback(() => after++).Returns(Task.CompletedTask);
            lifetime.As<IDisposable>().Setup(l => l.Dispose()).Callback(() => disposed++);

            await RunModeChildAsync("orchestrator", root, record, configureBuilder: builder =>
            {
                builder.TestHostOrchestrator.AddTestHostOrchestrator(_ => orchestrator.Object);
                builder.TestHostOrchestrator.AddTestHostOrchestratorApplicationLifetime(_ => lifetime.Object);
            });

            record("orchestrator-before:" + before.ToString(CultureInfo.InvariantCulture));
            record("orchestrator-after:" + after.ToString(CultureInfo.InvariantCulture));
            record("orchestrator-disposed:" + disposed.ToString(CultureInfo.InvariantCulture));
        }
        else if (mode == "controller-build")
        {
            Microsoft.Testing.Platform.Services.ServiceProvider? captured = null;
            int created = 0;
            Mock<ITestHostEnvironmentVariableProvider> provider = new();
            provider.SetupGet(p => p.Uid).Returns("routing-controller-provider");
            provider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(true);

            await RunModeChildAsync("build-only", root, record, configureBuilder: builder =>
                builder.TestHostControllers.AddEnvironmentVariableProvider(services =>
                {
                    captured = (Microsoft.Testing.Platform.Services.ServiceProvider)services;
                    created++;
                    return provider.Object;
                }));

            Assert.IsNotNull(captured);
            record("controller-role:" + captured.GetTestHostControllerInfo().IsCurrentProcessTestHostController);
            record("provider-created:" + created.ToString(CultureInfo.InvariantCulture));
            provider.Verify(p => p.UpdateAsync(It.IsAny<IEnvironmentVariables>()), Times.Never);
        }
        else if (mode == "factories")
        {
            int filters = 0;
            int invokers = 0;
            Mock<ITestFrameworkInvoker> invoker = new();
            invoker.SetupGet(i => i.Uid).Returns("routing-invoker");
            invoker.Setup(i => i.IsEnabledAsync()).ReturnsAsync(true);

            await RunModeChildAsync("build-only", root, record, configureBuilder: builder =>
            {
                ((TestHostManager)builder.TestHost).AddTestExecutionFilterFactory(services =>
                {
                    filters++;
                    return new ConsoleTestExecutionFilterFactory(services.GetCommandLineOptions());
                });
                ((TestHostManager)builder.TestHost).AddTestFrameworkInvoker(_ =>
                {
                    invokers++;
                    return invoker.Object;
                });
            });

            record("filter-created:" + filters.ToString(CultureInfo.InvariantCulture));
            record("invoker-created:" + invokers.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            throw new InvalidOperationException("Unknown owned routing mode.");
        }
    }

    private static async Task RunControlledRoutingChildAsync(string mode, string root, Action<string> record)
    {
        using CancellationTokenSource lifetime = new();
        SystemEnvironment environment = new();
        int pids = 0;
        int completions = 0;
        NamedPipeServer primary = new(
            "routing-" + Guid.NewGuid().ToString("N"),
            request =>
            {
                if (request is TestHostProcessPIDRequest pid)
                {
                    Assert.AreEqual(environment.ProcessId, pid.PID);
                    Interlocked.Increment(ref pids);
                }
                else if (request is TestHostCompletedRequest completed)
                {
                    Assert.AreEqual(0, completed.ExitCode);
                    Assert.AreEqual(0, completed.UnfilteredExitCode);
                    Assert.IsEmpty(completed.SummaryArtifacts);
                    Interlocked.Increment(ref completions);
                }
                else
                {
                    Assert.Fail("Unexpected controlled-host request.");
                }

                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            },
            environment, new NopLogger(), new SystemTask(), lifetime.Token);
        primary.RegisterSerializer(new TestHostProcessPIDRequestSerializer(), typeof(TestHostProcessPIDRequest));
        primary.RegisterSerializer(new TestHostCompletedRequestSerializer(), typeof(TestHostCompletedRequest));
        primary.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        using TestHostControllerCancellationServer control = new(
            null, environment, new ChildNopLoggerFactory(), new SystemTask());
        control.Start();
        string primaryVariable = EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME + "_17";
        string controlVariable = EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CONTROLPIPENAME + "_17";
        Environment.SetEnvironmentVariable(primaryVariable, primary.PipeName.Name);
        Environment.SetEnvironmentVariable(controlVariable, control.PipeName);
        Task accepted = primary.WaitConnectionAsync(lifetime.Token);
        try
        {
            string[] arguments = ["--internal-testhostcontroller-pid", "17"];
            if (mode == "controlled-failure")
            {
                InvalidOperationException expected = new("owned filter factory");
                InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => RunModeChildAsync("build-only", root, record, arguments, builder =>
                        builder.AddTestExecutionFilterProvider(_ => throw expected)));
                Assert.AreSame(expected, actual);
                record("failure:" + actual.Message);
            }
#if NETCOREAPP
            else if (mode == "controlled-async-dispose")
            {
                await BuildAndDisposeControlledBackendAsync(root, record);
            }
#endif
            else
            {
                string actualMode = mode switch
                {
                    "controlled-cancel" => "external-cancel",
                    "controlled-server" => "server-build",
                    _ => "controlled-console",
                };
                await RunModeChildAsync(actualMode, root, record, arguments);
            }

            await accepted.TimeoutAfterAsync(TimeSpan.FromSeconds(30));
            Assert.AreEqual(1, pids);
            Assert.AreEqual(mode == "controlled-console" ? 1 : 0, completions);
            bool disconnected = await primary.WaitForDisconnectAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(disconnected);
            record("pid-requests:" + pids.ToString(CultureInfo.InvariantCulture));
            record("completions:" + completions.ToString(CultureInfo.InvariantCulture));
            record("primary-disconnected:" + disconnected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(primaryVariable, null);
            Environment.SetEnvironmentVariable(controlVariable, null);
            lifetime.Cancel();
            await DisposeHelper.DisposeAsync(primary);
        }
    }

    private sealed class ChildNopLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NopLogger();
    }

    [TestMethod]
    [DataRow(false, 12, 0)]
    [DataRow(true, 0, 1)]
    public async Task BuildAsync_HelpOverRealDotnetPipeHonorsPeerCompatibility(bool compatible, int exitCode, int optionMessages)
    {
        string mode = compatible ? "compatible" : "incompatible";
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-routing-help-pipe-" + mode);
        Assert.AreEqual(0, child.ExitCode, child.Error);
        string[] records = ReadRecords(child.Output);
        Assert.Contains("result:" + exitCode.ToString(CultureInfo.InvariantCulture), records);
        Assert.Contains("framework-created:0", records);
        Assert.Contains("help-handshakes:1", records);
        Assert.Contains("option-messages:" + optionMessages.ToString(CultureInfo.InvariantCulture), records);
    }

    private static async Task RunHelpPipeRoutingChildAsync(bool compatible, string root, Action<string> record)
    {
        int handshakes = 0;
        int options = 0;
        OwnedProtocolServerPeer primary = new(
            (id, payload) =>
            {
                if (id == 9)
                {
                    HandshakeMessage handshake = OwnedProtocolServerPeer.Deserialize<HandshakeMessage>(new HandshakeMessageSerializer(), payload);
                    Assert.AreEqual("TestHost", handshake.Properties![HandshakeMessagePropertyNames.HostType]);
                    Assert.AreEqual("help", handshake.Properties[HandshakeMessagePropertyNames.ExecutionMode]);
                    Interlocked.Increment(ref handshakes);
                    return (9, OwnedProtocolServerPeer.Serialize(new HandshakeMessageSerializer(), new HandshakeMessage(new()
                    {
                        [HandshakeMessagePropertyNames.SupportedProtocolVersions] = compatible ? "1.4.0" : "99.0.0",
                    })));
                }

                Assert.AreEqual(3, id);
                CommandLineOptionMessages message = OwnedProtocolServerPeer.Deserialize<CommandLineOptionMessages>(new CommandLineOptionMessagesSerializer(), payload);
                Assert.IsTrue(compatible, "An incompatible peer must not receive help option payloads.");
                Assert.IsNotNull(message.CommandLineOptionMessageList);
                CommandLineOptionMessage help = Assert.ContainsSingle(message.CommandLineOptionMessageList.Where(o => o.Name == "help"));
                Assert.IsTrue(help.IsBuiltIn ?? false);
                Assert.AreEqual(0, help.MinimumArity);
                Assert.AreEqual(0, help.MaximumArity);
                Interlocked.Increment(ref options);
                return (0, []);
            });
        try
        {
            await RunModeChildAsync(
                "help", root, record,
                ["--server", "dotnettestcli", "--dotnet-test-pipe", primary.PipeName]);
            Assert.AreEqual(1, handshakes);
            Assert.AreEqual(compatible ? 1 : 0, options);
            record("help-handshakes:" + handshakes.ToString(CultureInfo.InvariantCulture));
            record("option-messages:" + options.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            await primary.DisposeAsync();
        }
    }

#if NETCOREAPP
    [TestMethod]
    public async Task ControlledHost_AsyncDisposalClosesUnrunServerAndBothOwnedPipes()
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync("builder-routing-controlled-async-dispose");
        Assert.AreEqual(0, child.ExitCode, child.Error);
        Assert.AreSequenceEqual(
            ["result:0", "framework-created:0", "async-disposed:True", "pid-requests:1", "completions:0", "primary-disconnected:True"],
            ReadRecords(child.Output));
    }

    private static async Task BuildAndDisposeControlledBackendAsync(string root, Action<string> record)
    {
        string configuration = Path.Combine(root, "async-server.json");
        File.WriteAllText(configuration, "{}");
        string[] args =
        [
            "--no-banner", "--config-file", configuration,
            "--server", "jsonrpc", "--client-port", "1", "--internal-testhostcontroller-pid", "17",
        ];
        SystemEnvironment environment = new();
        SystemProcessHandler process = new();
        CurrentTestApplicationModuleInfo module = new(environment, process, args, args);
        TestHostBuilder builder = new(new SystemFileSystem(), new SystemRuntimeFeature(), environment, process, module);
        Mock<ITestFrameworkCapabilities> capabilities = new();
        capabilities.SetupGet(c => c.Capabilities).Returns([]);
        int created = 0;
        builder.TestFramework = new TestFrameworkManager(
            (_, _) =>
            {
                created++;
                throw new AssertFailedException("Building an unrun server must not construct its framework.");
            },
            _ => capabilities.Object);
        TestApplicationOptions options = new() { EnableTelemetry = false };
        options.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource = false;
        ApplicationLoggingState logging = new(LogLevel.Warning, CommandLineParser.Parse(args, environment));

        IHost host = await builder.BuildAsync(
            logging, options, Mock.Of<IUnhandledExceptionsHandler>(), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        TestHostControlledHost controlled = Assert.IsInstanceOfType<TestHostControlledHost>(host);
        await controlled.DisposeAsync();

        Assert.AreEqual(0, created);
        record("result:0");
        record("framework-created:" + created.ToString(CultureInfo.InvariantCulture));
        record("async-disposed:True");
    }
#endif
}
