// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for running MSTest tests on <c>browser-wasm</c> (issue
/// <see href="https://github.com/microsoft/testfx/issues/2196"/>).
///
/// <para>
/// <c>browser-wasm</c> shares the single-threaded execution model of <c>wasi-wasm</c> (no thread
/// pool: <c>Task.Run</c> continuations never execute and blocking waits throw
/// <see cref="PlatformNotSupportedException"/>), and the same platform/adapter fallbacks
/// (<c>RuntimeFeatureHelper.IsMultiThreaded</c> / <c>RuntimeContext.IsMultiThreaded</c>) already cover
/// both. What differs is the <b>host</b>: <c>wasi-wasm</c> runs headless under <c>wasmtime</c>, while
/// <c>browser-wasm</c> boots through a JS module that loads <c>dotnet.js</c>. Because
/// Microsoft.Testing.Platform never touches the DOM, the published bundle boots headlessly under
/// <c>node</c> via the same loader, which is what this test uses instead of a real browser.
/// </para>
///
/// <para>Three complementary assertions, mirroring <see cref="WasmExecutionTests"/>:</para>
/// <list type="number">
///   <item>
///     <see cref="BrowserWasmBuild_GeneratesTestingPlatformEntryPoint"/> builds for
///     <c>browser-wasm</c> and asserts the MTP entry point is generated. The repo bootstrap
///     installs the required <c>wasm-tools</c> workload (see <c>eng/restore-toolset</c>), so this
///     runs in CI; it is skipped (inconclusive) only when that workload is genuinely absent (e.g.
///     a local machine that has not run the bootstrap), and otherwise a build failure fails the test.
///   </item>
///   <item>
///     <see cref="BrowserWasmExecution_RunsTestsUnderNode"/> publishes a real MSTest project for
///     <c>browser-wasm</c> and boots it end-to-end under <c>node</c> via a small
///     <c>dotnet.js</c> runner, asserting the passing tests report success and the failing test is
///     reported as failed. Skipped (inconclusive) only when the <c>wasm-tools</c> workload or
///     <c>node</c> is unavailable; otherwise publish must succeed and the run must exit with
///     <c>AtLeastOneTestFailed</c>.
///   </item>
///   <item>
///     <see cref="BrowserWasmExecution_FrameworkWarningReachesNode"/> publishes a custom-framework
///     project that emits a warning and an error through <c>IOutputDevice</c>, and asserts both reach
///     the Node output via the <c>[JSImport]</c> console bindings without a JS-interop exception —
///     directly guarding the <c>BrowserOutputDevice.JSConsoleWarn</c> fix. Same skip conditions.
///   </item>
/// </list>
/// </summary>
[TestClass]
public sealed class BrowserWasmExecutionTests : AcceptanceTestBase<NopAssetFixture>
{
    // browser-wasm is only supported on the current .NET TFM in this repo (see samples/BrowserPlayground).
    private static readonly string TargetFramework = TargetFrameworks.NetCurrent;

    // Minimal MSTest project targeting browser-wasm, running on Microsoft.Testing.Platform via the
    // generated entry point. The bundled main.js is the WasmMainJSPath boot module (needed for a
    // browser-wasm build to produce a bundle); the node run below uses a runner staged at test time.
    // One passing and one failing test so the run exercises both the success summary and the
    // non-zero exit code on failure.
    private const string SourceCode = """
#file BrowserTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$BrowserRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <!-- Publishing browser-wasm must be self-contained so the mono browser-wasm runtime pack resolves. -->
    <SelfContained>true</SelfContained>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMSTestRunner>true</EnableMSTestRunner>

    <!--
        Force the locally built Microsoft.Testing.Platform dependency to win over the transitive
        -preview one, mirroring the other acceptance-test assets.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>

    <!-- JS boot module the wasm runtime loads first (see wwwroot/main.js in samples/BrowserPlayground). -->
    <WasmMainJSPath>main.js</WasmMainJSPath>

    <!--
        Use the pre-built dotnet.native.wasm (no emscripten relink) and keep the managed assemblies
        untrimmed: MSTest discovers tests reflectively and an untuned trim strips the test methods
        (resulting in 0 discovered). InvariantGlobalization is intentionally NOT set: on browser-wasm
        it would force WasmBuildNative=true (an emscripten relink), which we avoid here; the browser
        bundle ships ICU by default.
    -->
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>

    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file main.js
import { dotnet } from './_framework/dotnet.js';
const { runMain } = await dotnet.withApplicationArgumentsFromQuery().create();
const exitCode = await runMain();
globalThis.mtpExitCode = exitCode;

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class UnitTest1
{
    [TestMethod]
    public void PassingTest()
        => Assert.AreEqual(4, 2 + 2);

    [TestMethod]
    public void AnotherPassingTest()
        => Assert.IsTrue(true);

    [TestMethod]
    public void FailingTest()
        => Assert.Fail("Intentional failure to verify failures are reported under browser-wasm.");
}
""";

    // node runner staged next to the published bundle. Boots the browser-wasm app under node (no DOM
    // required — Microsoft.Testing.Platform does not touch the DOM) and maps the .NET exit code onto
    // the node process exit code. Mirrors samples/BrowserPlayground/wwwroot/runtests.mjs.
    private const string NodeRunnerSource = """
import { dotnet } from './_framework/dotnet.js';
const { runMain } = await dotnet.withApplicationArguments(...process.argv.slice(2)).create();
const exitCode = await runMain();
// Set exitCode rather than calling process.exit(): process.exit() can terminate Node before
// redirected stdout/stderr has flushed, which would truncate the MTP summary this test asserts on.
process.exitCode = exitCode;
""";

    private const string DotnetTestHttpToken = "browser-wasm-http-transport-test-token";

    private const string DotnetTestHttpNodeRunnerSource = """
import { Worker } from 'node:worker_threads';

const scenario = '$Scenario$';
const token = '$Token$';
function gatewayMain() {
const http = require('node:http');
const { parentPort, workerData } = require('node:worker_threads');
const { scenario, token } = workerData;
const serializerIds = [];
let activeRequests = 0;
let maximumActiveRequests = 0;
let authorizedRequests = 0;
let validFrames = 0;
let cancelledRequests = 0;
let cancellationSafetyTimerExpirations = 0;

function createFrame(serializerId, body = Buffer.alloc(0)) {
    const frame = Buffer.alloc(8 + body.length);
    frame.writeInt32LE(4 + body.length, 0);
    frame.writeInt32LE(serializerId, 4);
    body.copy(frame, 8);
    return frame;
}

function createHandshakeFrame() {
    const version = Buffer.from('1.4.0', 'utf8');
    const body = Buffer.alloc(2 + 1 + 4 + version.length);
    body.writeUInt16LE(1, 0);
    body.writeUInt8(4, 2);
    body.writeInt32LE(version.length, 3);
    version.copy(body, 7);
    return createFrame(9, body);
}

const server = http.createServer(async (request, response) => {
    activeRequests++;
    maximumActiveRequests = Math.max(maximumActiveRequests, activeRequests);

    const chunks = [];
    for await (const chunk of request) {
        chunks.push(chunk);
    }

    const requestBody = Buffer.concat(chunks);
    const isAuthorized = request.headers.authorization === `Bearer ${token}`;
    if (isAuthorized) {
        authorizedRequests++;
    }

    const hasValidFrame =
        request.method === 'POST'
        && request.headers['content-type']?.startsWith('application/octet-stream')
        && requestBody.length >= 8
        && requestBody.readInt32LE(0) === requestBody.length - 4;
    if (hasValidFrame) {
        validFrames++;
        serializerIds.push(requestBody.readInt32LE(4));
    }

    if (!isAuthorized || !hasValidFrame || scenario === 'unauthorized') {
        activeRequests--;
        response.writeHead(401);
        response.end();
        return;
    }

    if (scenario === 'disconnect') {
        activeRequests--;
        request.socket.destroy();
        return;
    }

    const serializerId = requestBody.readInt32LE(4);
    if (scenario === 'cancel' && serializerId === 6) {
        let safetyTimerExpired = false;
        const safetyTimer = setTimeout(() => {
            safetyTimerExpired = true;
            cancellationSafetyTimerExpirations++;
            response.destroy();
        }, 30000);
        safetyTimer.unref();
        response.on('close', () => {
            clearTimeout(safetyTimer);
            if (!safetyTimerExpired) {
                cancelledRequests++;
            }

            activeRequests--;
        });

        return;
    }

    await new Promise(resolve => setTimeout(resolve, 20));
    const responseBody = serializerId === 9 ? createHandshakeFrame() : createFrame(0);
    response.writeHead(200, {
        'Content-Type': 'application/octet-stream',
        'Content-Length': responseBody.length,
    });
    response.end(responseBody);
    activeRequests--;
});

parentPort.on('message', message => {
    if (message !== 'close') {
        return;
    }

    server.close(() => {
        parentPort.postMessage({
            type: 'result',
            serializerIds,
            maximumActiveRequests,
            authorizedRequests,
            validFrames,
            cancelledRequests,
            cancellationSafetyTimerExpirations,
        });
        parentPort.close();
    });
    server.closeIdleConnections?.();
});

server.listen(0, '127.0.0.1', () => {
    parentPort.postMessage({ type: 'ready', port: server.address().port });
});
}

const gatewaySource = '(' + gatewayMain.toString() + ')();';
const gateway = new Worker(gatewaySource, {
    eval: true,
    workerData: { scenario, token },
});
const gatewayMessages = new Map();
const gatewayWaiters = new Map();
gateway.on('message', message => {
    const waiter = gatewayWaiters.get(message.type);
    if (waiter) {
        gatewayWaiters.delete(message.type);
        waiter(message);
    }
    else {
        gatewayMessages.set(message.type, message);
    }
});

function receiveGatewayMessage(type) {
    const message = gatewayMessages.get(type);
    if (message) {
        gatewayMessages.delete(type);
        return Promise.resolve(message);
    }

    return new Promise(resolve => gatewayWaiters.set(type, resolve));
}

const ready = await receiveGatewayMessage('ready');
const endpoint = 'http://127.0.0.1:' + ready.port + '/dotnettest';

// BrowserHttpHandler probes streaming-request support with new Request(""). Browsers resolve that
// against the document URL, while Node rejects it before the real fetch. Preserve browser semantics
// for that feature probe without replacing the product's HttpClient/fetch transport.
const NodeRequest = globalThis.Request;
globalThis.Request = class extends NodeRequest {
    constructor(input, init) {
        super(input === '' ? endpoint : input, init);
    }
};

const { dotnet } = await import('./_framework/dotnet.js');
const extraArguments = scenario === 'help'
    ? ['--help']
    : scenario === 'discover'
        ? ['--list-tests']
        : scenario === 'cancel'
        ? ['--timeout', '5s']
            : [];

let exitCode = 99;
let thrown = false;
try {
    const { runMain } = await dotnet
        .withApplicationArguments(
            '--server', 'dotnettestcli',
            '--dotnet-test-transport', 'http',
            '--dotnet-test-http-endpoint', endpoint,
            '--dotnet-test-http-token', token,
            ...extraArguments)
        .create();
    exitCode = await runMain();
}
catch (error) {
    thrown = true;
    console.error(error);
}

gateway.postMessage('close');
const gatewayResult = await receiveGatewayMessage('result');
await gateway.terminate();

const result = 'HTTP_GATEWAY_RESULT=' + JSON.stringify({
    scenario,
    exitCode,
    thrown,
    ...gatewayResult,
});
// A canceled BrowserHttpHandler operation can retain a Node runtime handle after managed Main returns.
// Flush the observation marker before explicitly terminating this acceptance-only process.
await new Promise(resolve => process.stdout.write(result + '\n', resolve));
// Let the worker's libuv handle finish closing before terminating the Node runtime, whose browser
// emulation can retain handles after managed Main returns (especially after a canceled fetch).
await new Promise(resolve => setTimeout(resolve, 100));
process.exit(exitCode);
""";

    // The runsettings XML (declaring an <EnvironmentVariables> section) that the run-settings validation
    // test supplies to the browser-wasm host. It is passed as *content* via the
    // TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS environment variable rather than as a file, so the
    // test does not depend on the wasm virtual-filesystem resolving a --settings file path.
    private const string RunSettingsWithEnvironmentVariablesXml =
        "<RunSettings><RunConfiguration><EnvironmentVariables><MTP_BROWSER_RUNSETTINGS_MARKER>1</MTP_BROWSER_RUNSETTINGS_MARKER></EnvironmentVariables></RunConfiguration></RunSettings>";

    // Node runner that supplies the above runsettings through the content runsettings environment variable
    // (TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS) via the dotnet.js host builder's
    // withEnvironmentVariable API. This exercises the environment-variable runsettings resolution path (not
    // just --settings), which flows through the same RunSettingsProviderHelper.TryLoadRunSettingsAsync used
    // by RunSettingsCommandLineOptionsProviderBase.ValidateCommandLineOptionsAsync.
    private const string NodeRunnerWithRunSettingsContentEnvVarSource = """
import { dotnet } from './_framework/dotnet.js';
const runSettings = '$RunSettingsXml$';
const { runMain } = await dotnet
    .withEnvironmentVariable('TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS', runSettings)
    .withApplicationArguments()
    .create();
const exitCode = await runMain();
process.exitCode = exitCode;
""";

    // Minimal browser-wasm MSTest project with a single passing test. The <EnvironmentVariables> runsettings
    // are injected at runtime via the environment variable (see the runner above), so no runsettings file is
    // bundled.
    private const string RunSettingsSourceCode = """
#file BrowserRunSettingsProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$BrowserRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <SelfContained>true</SelfContained>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <WasmMainJSPath>main.js</WasmMainJSPath>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file main.js
import { dotnet } from './_framework/dotnet.js';
const { runMain } = await dotnet.withApplicationArgumentsFromQuery().create();
const exitCode = await runMain();
globalThis.mtpExitCode = exitCode;

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class UnitTest1
{
    [TestMethod]
    public void PassingTest()
        => Assert.IsTrue(true);
}
""";

    // Deterministic strings the warning asset emits and the test asserts reach Node output.
    private const string WarningText = "browser-wasm framework warning marker";
    private const string ErrorText = "browser-wasm framework error marker";

    // A minimal custom-framework browser-wasm project (no MSTest) that emits a warning and an error
    // through IOutputDevice, then reports a single passing test. Unlike the MSTest asset it hosts MTP
    // via its own Program.Main (like samples/BrowserPlayground), so the warning path through
    // BrowserOutputDevice.JSConsoleWarn is exercised end-to-end.
    private const string WarningSourceCode = """
#file BrowserWarningProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$BrowserRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <SelfContained>true</SelfContained>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <WasmMainJSPath>main.js</WasmMainJSPath>
    <WasmBuildNative>false</WasmBuildNative>
    <PublishTrimmed>false</PublishTrimmed>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file main.js
import { dotnet } from './_framework/dotnet.js';
const { runMain } = await dotnet.withApplicationArgumentsFromQuery().create();
const exitCode = await runMain();
globalThis.mtpExitCode = exitCode;

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(
    _ => new TestFrameworkCapabilities(),
    (_, serviceProvider) => new WarningFramework(serviceProvider.GetOutputDevice()));
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

internal sealed class WarningFramework : ITestFramework, IDataProducer, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;

    public WarningFramework(IOutputDevice outputDevice) => _outputDevice = outputDevice;

    public string Uid => nameof(WarningFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(WarningFramework);
    public string Description => nameof(WarningFramework);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData("$WarningText$"), CancellationToken.None);
        await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData("$ErrorText$"), CancellationToken.None);
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            DisplayName = "PassingNode",
            Uid = "Uid1",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
        }));
        context.Complete();
    }
}
""";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BrowserWasmBuild_GeneratesTestingPlatformEntryPoint()
    {
        using TestAsset generator = await GenerateBrowserWasmAssetAsync();

        // Only a missing 'wasm-tools' workload is an acceptable skip; any other build failure (compiler
        // error, a broken generated MTP entry point) is a real regression and must fail the test.
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build {generator.TargetAssetPath} -f {TargetFramework} -r {WasmRuntime.BrowserRid} -c Release",
            // Trimming/wasm builds can emit non-actionable warnings; we only assert on the build
            // succeeding and the entry point being generated, not on a warning-clean build.
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        if (buildResult.ExitCode != 0)
        {
            Assert.IsTrue(
                WasmRuntime.IsMissingWasmToolsWorkload(buildResult),
                $"'dotnet build -r browser-wasm' failed for an unexpected reason (not a missing 'wasm-tools' workload).{Environment.NewLine}{buildResult}");
            Assert.Inconclusive(
                $"Skipping browser-wasm build: the 'wasm-tools' workload is not installed.{Environment.NewLine}{buildResult}");
            return;
        }

        // The MTP MSBuild task writes the generated entry point into the intermediate output folder.
        // Its presence proves the browser-wasm build produced the Microsoft.Testing.Platform host
        // entry point (the plumbing that browser-wasm execution ultimately relies on).
        string[] generatedEntryPoints = Directory.GetFiles(
            Path.Combine(generator.TargetAssetPath, "obj"),
            "MicrosoftTestingPlatformEntryPoint.cs",
            SearchOption.AllDirectories);

        Assert.IsNotEmpty(
            generatedEntryPoints,
            $"Expected a generated 'MicrosoftTestingPlatformEntryPoint.cs' under '{Path.Combine(generator.TargetAssetPath, "obj")}'.");
    }

    [TestMethod]
    public async Task BrowserWasmExecution_DotnetTestHttpTransportExercisesLiveGateway()
    {
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmAssetAsync();
        DotnetMuxerResult publishResult = await WasmRuntime.PublishForBrowserAsync(
            generator.TargetAssetPath, TargetFramework, TestContext.CancellationToken);
        if (publishResult.ExitCode != 0)
        {
            Assert.IsTrue(
                WasmRuntime.IsMissingWasmToolsWorkload(publishResult),
                $"'dotnet publish -r browser-wasm' failed for an unexpected reason (not a missing 'wasm-tools' workload).{Environment.NewLine}{publishResult}");
            Assert.Inconclusive(
                $"Skipping browser-wasm execution: the 'wasm-tools' workload is not installed.{Environment.NewLine}{publishResult}");
        }

        string appBundle = WasmRuntime.GetBrowserAppBundlePath(generator.TargetAssetPath, TargetFramework);
        Assert.IsTrue(Directory.Exists(appBundle), $"Expected the browser-wasm AppBundle directory at '{appBundle}'.");

        HttpGatewayScenarioResult help = await RunDotnetTestHttpScenarioAsync(node, appBundle, "help");
        Assert.AreEqual((int)ExitCode.Success, help.ProcessExitCode, help.CombinedOutput);
        AssertHttpGatewaySuccess(help, expectedMessageSerializerId: 3);

        HttpGatewayScenarioResult discovery = await RunDotnetTestHttpScenarioAsync(node, appBundle, "discover");
        Assert.AreEqual((int)ExitCode.Success, discovery.ProcessExitCode, discovery.CombinedOutput);
        AssertHttpGatewaySuccess(discovery, expectedMessageSerializerId: 5);

        HttpGatewayScenarioResult run = await RunDotnetTestHttpScenarioAsync(node, appBundle, "run");
        Assert.AreEqual((int)ExitCode.AtLeastOneTestFailed, run.ProcessExitCode, run.CombinedOutput);
        AssertHttpGatewaySuccess(run, expectedMessageSerializerId: 6);
        Assert.Contains(8, run.SerializerIds, $"Expected test-session traffic after the handshake.{Environment.NewLine}{run.CombinedOutput}");

        HttpGatewayScenarioResult unauthorized = await RunDotnetTestHttpScenarioAsync(node, appBundle, "unauthorized");
        Assert.AreNotEqual((int)ExitCode.Success, unauthorized.ProcessExitCode, unauthorized.CombinedOutput);
        Assert.AreEqual(1, unauthorized.AuthorizedRequests, unauthorized.CombinedOutput);
        Assert.AreEqual(1, unauthorized.ValidFrames, unauthorized.CombinedOutput);
        Assert.Contains("401", unauthorized.CombinedOutput);

        HttpGatewayScenarioResult disconnect = await RunDotnetTestHttpScenarioAsync(node, appBundle, "disconnect");
        Assert.AreNotEqual((int)ExitCode.Success, disconnect.ProcessExitCode, disconnect.CombinedOutput);
        Assert.AreEqual(1, disconnect.AuthorizedRequests, disconnect.CombinedOutput);
        Assert.AreEqual(1, disconnect.ValidFrames, disconnect.CombinedOutput);

        HttpGatewayScenarioResult cancellation = await RunDotnetTestHttpScenarioAsync(node, appBundle, "cancel");
        Assert.AreNotEqual((int)ExitCode.Success, cancellation.ProcessExitCode, cancellation.CombinedOutput);
        Assert.AreEqual(1, cancellation.CancelledRequests, cancellation.CombinedOutput);
        Assert.AreEqual(0, cancellation.CancellationSafetyTimerExpirations, cancellation.CombinedOutput);
        Assert.IsGreaterThanOrEqualTo(2, cancellation.ValidFrames, cancellation.CombinedOutput);
        Assert.AreEqual(1, cancellation.MaximumActiveRequests, cancellation.CombinedOutput);
    }

    [TestMethod]
    public async Task BrowserWasmExecution_RunsTestsUnderNode()
    {
        // Gate the runtime invocation on 'node'; the publish step below handles the 'wasm-tools'
        // workload skip. With the workload and node present, publish and execution must succeed as
        // asserted, so browser-specific publish or run regressions are not silently swallowed.
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmAssetAsync();

        (int exitCode, string output, string error, string combined) = await PublishAndRunUnderNodeAsync(generator, node);

        // A PlatformNotSupportedException (the single-threaded blocking-wait failure mode) would
        // indicate a regression in the wasm fallbacks.
        Assert.IsFalse(
            error.Contains("PlatformNotSupportedException", StringComparison.Ordinal)
                || output.Contains("PlatformNotSupportedException", StringComparison.Ordinal),
            $"Microsoft.Testing.Platform hit an unexpected PlatformNotSupportedException under browser-wasm.{Environment.NewLine}{combined}");

        // The run summary proves tests actually executed: the two passing tests succeeded and the
        // intentional failure was reported. Assert on the combined output: on browser-wasm the
        // failed-run summary is routed through BrowserOutputDevice.ConsoleError -> console.error
        // (stderr), unlike wasi where everything lands on stdout.
        Assert.IsTrue(
            combined.Contains("succeeded: 2", StringComparison.Ordinal),
            $"Expected 2 succeeded tests in the browser-wasm run summary.{Environment.NewLine}{combined}");
        Assert.IsTrue(
            combined.Contains("failed: 1", StringComparison.Ordinal),
            $"Expected 1 failed test in the browser-wasm run summary.{Environment.NewLine}{combined}");

        foreach (string testName in new[] { "PassingTest", "AnotherPassingTest", "FailingTest" })
        {
            Assert.IsTrue(
                combined.Contains($"running {testName}", StringComparison.Ordinal),
                $"Expected browser-wasm progress output to identify '{testName}' before it ran.{Environment.NewLine}{combined}");
        }

        // Assert the exact "at least one test failed" exit code (2). A generic non-zero check would
        // also pass for a post-run crash or other MTP failure; requiring AtLeastOneTestFailed keeps
        // those from masquerading as the expected failing-test outcome.
        Assert.AreEqual(
            (int)ExitCode.AtLeastOneTestFailed,
            exitCode,
            $"Expected exit code {(int)ExitCode.AtLeastOneTestFailed} (AtLeastOneTestFailed) because one test fails.{Environment.NewLine}{combined}");
    }

    [TestMethod]
    public async Task BrowserWasmExecution_FrameworkWarningReachesNode()
    {
        // Guards the BrowserOutputDevice.JSConsoleWarn binding fix: the other execution test never
        // emits a WarningMessageOutputDeviceData, so it would still pass if the console.warn interop
        // were broken. Here a custom framework emits a deterministic warning (and error) that must
        // reach the Node output via [JSImport] without a JS interop / PlatformNotSupportedException.
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmWarningAssetAsync();

        (int exitCode, string output, string error, string combined) = await PublishAndRunUnderNodeAsync(generator, node);

        // The warning routes through BrowserOutputDevice.ConsoleWarn -> JSConsoleWarn ->
        // globalThis.console.warn (stderr under Node); the error routes through console.error. Both
        // going through the wasm JS interop must not throw, and the text must actually surface.
        Assert.IsFalse(
            error.Contains("PlatformNotSupportedException", StringComparison.Ordinal)
                || output.Contains("PlatformNotSupportedException", StringComparison.Ordinal),
            $"Microsoft.Testing.Platform hit an unexpected PlatformNotSupportedException under browser-wasm.{Environment.NewLine}{combined}");

        Assert.IsTrue(
            combined.Contains(WarningText, StringComparison.Ordinal),
            $"Expected the framework warning to reach the Node output via console.warn interop.{Environment.NewLine}{combined}");
        Assert.IsTrue(
            combined.Contains(ErrorText, StringComparison.Ordinal),
            $"Expected the framework error to reach the Node output via console.error interop.{Environment.NewLine}{combined}");

        // The framework reports a single passing test and no failures, so the run succeeds.
        Assert.AreEqual(
            (int)ExitCode.Success,
            exitCode,
            $"Expected exit code {(int)ExitCode.Success} (Success) for the warning asset.{Environment.NewLine}{combined}");
    }

    [TestMethod]
    public async Task BrowserWasmExecution_RunSettingsEnvironmentVariables_FailsWithClearDiagnostic()
    {
        // Guards RunSettingsCommandLineOptionsProviderBase.ValidateCommandLineOptionsAsync: a runsettings
        // <EnvironmentVariables> section can't be applied on browser (it needs a test-host-controller
        // process restart), so the run must fail with the localized diagnostic rather than silently
        // ignoring the variables. The runsettings are supplied through the content environment variable
        // (TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS), which flows through the same
        // RunSettingsProviderHelper.TryLoadRunSettingsAsync resolution the validation uses — so it also
        // covers the environment-variable resolution path, not only --settings.
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmRunSettingsAssetAsync();

        DotnetMuxerResult publishResult = await WasmRuntime.PublishForBrowserAsync(
            generator.TargetAssetPath, TargetFramework, TestContext.CancellationToken);
        if (publishResult.ExitCode != 0)
        {
            Assert.IsTrue(
                WasmRuntime.IsMissingWasmToolsWorkload(publishResult),
                $"'dotnet publish -r browser-wasm' failed for an unexpected reason (not a missing 'wasm-tools' workload).{Environment.NewLine}{publishResult}");
            Assert.Inconclusive(
                $"Skipping browser-wasm execution: the 'wasm-tools' workload is not installed.{Environment.NewLine}{publishResult}");
        }

        string appBundle = WasmRuntime.GetBrowserAppBundlePath(generator.TargetAssetPath, TargetFramework);
        Assert.IsTrue(
            Directory.Exists(appBundle),
            $"Expected the browser-wasm AppBundle directory at '{appBundle}'.");

        string runner = NodeRunnerWithRunSettingsContentEnvVarSource.PatchCodeWithReplace("$RunSettingsXml$", RunSettingsWithEnvironmentVariablesXml);
        (int exitCode, _, _, string combined) = await WasmRuntime.RunUnderNodeAsync(
            node, appBundle, runner, TestContext.CancellationToken);

        // A PlatformNotSupportedException would mean the section slipped past validation into the
        // controller registration path (the regression this diagnostic replaces).
        Assert.IsFalse(
            combined.Contains("PlatformNotSupportedException", StringComparison.Ordinal),
            $"Expected a clean command-line validation failure, not a PlatformNotSupportedException.{Environment.NewLine}{combined}");

        // The localized diagnostic is surfaced (matched on the stable, non-localized substrings).
        Assert.IsTrue(
            combined.Contains("EnvironmentVariables", StringComparison.Ordinal)
                && combined.Contains("browser", StringComparison.OrdinalIgnoreCase),
            $"Expected the browser <EnvironmentVariables> unsupported diagnostic in the output.{Environment.NewLine}{combined}");

        // Command-line validation failures exit with InvalidCommandLine (5); the run is rejected before any
        // test executes.
        Assert.AreEqual(
            (int)ExitCode.InvalidCommandLine,
            exitCode,
            $"Expected exit code {(int)ExitCode.InvalidCommandLine} (InvalidCommandLine).{Environment.NewLine}{combined}");
    }

    // Publishes the generated browser-wasm asset, staging + booting it under Node. Only a missing
    // 'wasm-tools' workload is an acceptable skip (Inconclusive); any other publish failure is a real
    // regression and fails the test. Returns the process exit code plus captured stdout/stderr.
    private async Task<(int ExitCode, string Output, string Error, string Combined)> PublishAndRunUnderNodeAsync(TestAsset generator, string node)
    {
        DotnetMuxerResult publishResult = await WasmRuntime.PublishForBrowserAsync(
            generator.TargetAssetPath, TargetFramework, TestContext.CancellationToken);
        if (publishResult.ExitCode != 0)
        {
            Assert.IsTrue(
                WasmRuntime.IsMissingWasmToolsWorkload(publishResult),
                $"'dotnet publish -r browser-wasm' failed for an unexpected reason (not a missing 'wasm-tools' workload).{Environment.NewLine}{publishResult}");
            Assert.Inconclusive(
                $"Skipping browser-wasm execution: the 'wasm-tools' workload is not installed.{Environment.NewLine}{publishResult}");
        }

        string appBundle = WasmRuntime.GetBrowserAppBundlePath(generator.TargetAssetPath, TargetFramework);
        Assert.IsTrue(
            Directory.Exists(appBundle),
            $"Expected the browser-wasm AppBundle directory at '{appBundle}'.");

        return await WasmRuntime.RunUnderNodeAsync(node, appBundle, NodeRunnerSource, TestContext.CancellationToken);
    }

    private async Task<HttpGatewayScenarioResult> RunDotnetTestHttpScenarioAsync(string node, string appBundle, string scenario)
    {
        string runner = DotnetTestHttpNodeRunnerSource
            .PatchCodeWithReplace("$Scenario$", scenario)
            .PatchCodeWithReplace("$Token$", DotnetTestHttpToken);
        (int processExitCode, _, _, string combined) = await WasmRuntime.RunUnderNodeAsync(
            node, appBundle, runner, TestContext.CancellationToken);

        Assert.DoesNotContain(DotnetTestHttpToken, combined, "The per-run bearer token must never be written to host diagnostics.");
        const string marker = "HTTP_GATEWAY_RESULT=";
        int markerIndex = combined.LastIndexOf(marker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, markerIndex, $"The Node HTTP gateway did not report its observations.{Environment.NewLine}{combined}");
        int jsonStart = markerIndex + marker.Length;
        int jsonEnd = combined.IndexOfAny(['\r', '\n'], jsonStart);
        string json = jsonEnd < 0 ? combined[jsonStart..] : combined[jsonStart..jsonEnd];

        using var document = System.Text.Json.JsonDocument.Parse(json);
        System.Text.Json.JsonElement root = document.RootElement;
        return new HttpGatewayScenarioResult(
            processExitCode,
            combined,
            [.. root.GetProperty("serializerIds").EnumerateArray().Select(element => element.GetInt32())],
            root.GetProperty("maximumActiveRequests").GetInt32(),
            root.GetProperty("authorizedRequests").GetInt32(),
            root.GetProperty("validFrames").GetInt32(),
            root.GetProperty("cancelledRequests").GetInt32(),
            root.GetProperty("cancellationSafetyTimerExpirations").GetInt32());
    }

    private static void AssertHttpGatewaySuccess(HttpGatewayScenarioResult result, int expectedMessageSerializerId)
    {
        Assert.IsNotEmpty(result.SerializerIds, result.CombinedOutput);
        Assert.AreEqual(9, result.SerializerIds[0], $"The protocol handshake must be the first HTTP request.{Environment.NewLine}{result.CombinedOutput}");
        Assert.Contains(expectedMessageSerializerId, result.SerializerIds, result.CombinedOutput);
        Assert.AreEqual(result.SerializerIds.Count, result.AuthorizedRequests, result.CombinedOutput);
        Assert.AreEqual(result.SerializerIds.Count, result.ValidFrames, result.CombinedOutput);
        Assert.AreEqual(1, result.MaximumActiveRequests, result.CombinedOutput);
    }

    private Task<TestAsset> GenerateBrowserWasmAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "BrowserTestProject",
            SourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$BrowserRid$", WasmRuntime.BrowserRid)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

    private Task<TestAsset> GenerateBrowserWasmWarningAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "BrowserWarningProject",
            WarningSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$BrowserRid$", WasmRuntime.BrowserRid)
                .PatchCodeWithReplace("$WarningText$", WarningText)
                .PatchCodeWithReplace("$ErrorText$", ErrorText)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

    private sealed record HttpGatewayScenarioResult(
        int ProcessExitCode,
        string CombinedOutput,
        IReadOnlyList<int> SerializerIds,
        int MaximumActiveRequests,
        int AuthorizedRequests,
        int ValidFrames,
        int CancelledRequests,
        int CancellationSafetyTimerExpirations);

    private Task<TestAsset> GenerateBrowserWasmRunSettingsAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "BrowserRunSettingsProject",
            RunSettingsSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$BrowserRid$", WasmRuntime.BrowserRid)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
}
