// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

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

    private const string NodeWebSocketGatewayRunnerSource = """
import { createHash } from 'node:crypto';
import { createServer } from 'node:net';
import { dotnet } from './_framework/dotnet.js';

const token = 'browser-transport-secret';
let authenticated = false;
let transport = null;
let requestCount = 0;
let disconnected = false;

function websocketFrame(payload) {
    if (payload.length < 126) {
        return Buffer.concat([Buffer.from([0x82, payload.length]), payload]);
    }

    const header = Buffer.alloc(4);
    header[0] = 0x82;
    header[1] = 126;
    header.writeUInt16BE(payload.length, 2);
    return Buffer.concat([header, payload]);
}

function protocolFrame(serializerId, body = Buffer.alloc(0)) {
    const frame = Buffer.alloc(8 + body.length);
    frame.writeInt32LE(4 + body.length, 0);
    frame.writeInt32LE(serializerId, 4);
    body.copy(frame, 8);
    return frame;
}

function handshakeReply() {
    const version = Buffer.from('1.5.0');
    const body = Buffer.alloc(2 + 1 + 4 + version.length);
    body.writeUInt16LE(1, 0);
    body[2] = 4;
    body.writeInt32LE(version.length, 3);
    version.copy(body, 7);
    return protocolFrame(9, body);
}

function readHandshakeTransport(body) {
    let offset = 0;
    const count = body.readUInt16LE(offset);
    offset += 2;
    for (let i = 0; i < count; i++) {
        const key = body[offset++];
        const length = body.readInt32LE(offset);
        offset += 4;
        const value = body.subarray(offset, offset + length).toString('utf8');
        offset += length;
        if (key === 16) {
            return value;
        }
    }

    return null;
}

function consumeFrames(socket, state) {
    while (state.buffer.length >= 2) {
        const first = state.buffer[0];
        const second = state.buffer[1];
        let length = second & 0x7f;
        let offset = 2;
        if (length === 126) {
            if (state.buffer.length < 4) return;
            length = state.buffer.readUInt16BE(2);
            offset = 4;
        } else if (length === 127) {
            if (state.buffer.length < 10) return;
            length = Number(state.buffer.readBigUInt64BE(2));
            offset = 10;
        }

        const masked = (second & 0x80) !== 0;
        const maskLength = masked ? 4 : 0;
        if (state.buffer.length < offset + maskLength + length) return;

        let payload = Buffer.from(state.buffer.subarray(offset + maskLength, offset + maskLength + length));
        if (masked) {
            const mask = state.buffer.subarray(offset, offset + 4);
            for (let i = 0; i < payload.length; i++) payload[i] ^= mask[i % 4];
        }

        state.buffer = state.buffer.subarray(offset + maskLength + length);
        const opcode = first & 0x0f;
        if (opcode === 8) {
            disconnected = true;
            socket.end();
            continue;
        }

        if (opcode !== 2) continue;
        requestCount++;
        const serializerId = payload.readInt32LE(4);
        if (serializerId === 9) {
            transport = readHandshakeTransport(payload.subarray(8));
            socket.write(websocketFrame(Buffer.alloc(0)));
            socket.write(websocketFrame(handshakeReply()));
        } else {
            // All post-handshake request/reply messages emitted by this no-op passing framework expect
            // VoidResponse, matching FakeDotnetTestSdk's named-pipe harness.
            socket.write(websocketFrame(protocolFrame(0)));
        }
    }
}

const server = createServer(socket => {
    const state = { upgraded: false, buffer: Buffer.alloc(0) };
    socket.on('end', () => { disconnected = true; });
    socket.on('close', () => { disconnected = true; });
    socket.on('data', chunk => {
        state.buffer = Buffer.concat([state.buffer, chunk]);
        if (!state.upgraded) {
            const end = state.buffer.indexOf('\r\n\r\n');
            if (end < 0) return;
            const request = state.buffer.subarray(0, end).toString('utf8');
            authenticated = request.includes(`dotnetTestToken=${token}`);
            const key = /^Sec-WebSocket-Key:\s*(.+)$/mi.exec(request)?.[1]?.trim();
            const accept = createHash('sha1').update(key + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest('base64');
            socket.write(
                'HTTP/1.1 101 Switching Protocols\r\n' +
                'Upgrade: websocket\r\n' +
                'Connection: Upgrade\r\n' +
                `Sec-WebSocket-Accept: ${accept}\r\n\r\n`);
            state.buffer = state.buffer.subarray(end + 4);
            state.upgraded = true;
        }

        consumeFrames(socket, state);
    });
});

await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const port = server.address().port;
const { runMain } = await dotnet.withApplicationArguments(
    '--server', 'dotnettestcli',
    '--dotnet-test-transport', 'websocket',
    '--dotnet-test-websocket-endpoint', `ws://127.0.0.1:${port}/dotnettest`,
    '--dotnet-test-websocket-token', token).create();
const exitCode = await runMain();
await new Promise(resolve => setTimeout(resolve, 100));
server.close();

console.log(`BROWSER_WEBSOCKET_AUTHENTICATED=${authenticated}`);
console.log(`BROWSER_WEBSOCKET_TRANSPORT=${transport}`);
console.log(`BROWSER_WEBSOCKET_REQUESTS=${requestCount}`);
console.log(`BROWSER_WEBSOCKET_DISCONNECTED=${disconnected}`);
process.exitCode = exitCode;
""";

    private const string NodeStalledWebSocketRunnerSource = """
import { createServer } from 'node:net';
import { dotnet } from './_framework/dotnet.js';

const sockets = new Set();
const server = createServer(socket => {
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
    // Deliberately never complete the HTTP upgrade. BrowserWebSocketDuplexStream.ConnectAsync must still
    // observe its .NET cancellation token while the browser-native WebSocket remains in CONNECTING state.
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const port = server.address().port;
const { runMain } = await dotnet.withApplicationArguments('connect-cancel', `ws://127.0.0.1:${port}/stalled`).create();
const exitCode = await runMain();
for (const socket of sockets) socket.destroy();
server.close();
process.exitCode = exitCode;
""";

    private const string NodeWebSocketIoCancellationRunnerSource = """
import { createHash } from 'node:crypto';
import { createServer } from 'node:net';
import { dotnet } from './_framework/dotnet.js';

function websocketFrame(payload) {
    return Buffer.concat([Buffer.from([0x82, payload.length]), payload]);
}

const server = createServer(socket => {
    let buffer = Buffer.alloc(0);
    socket.on('data', chunk => {
        buffer = Buffer.concat([buffer, chunk]);
        const end = buffer.indexOf('\r\n\r\n');
        if (end < 0) return;
        const request = buffer.subarray(0, end).toString('utf8');
        const key = /^Sec-WebSocket-Key:\s*(.+)$/mi.exec(request)?.[1]?.trim();
        const accept = createHash('sha1').update(key + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest('base64');
        socket.write(
            'HTTP/1.1 101 Switching Protocols\r\n' +
            'Upgrade: websocket\r\n' +
            'Connection: Upgrade\r\n' +
            `Sec-WebSocket-Accept: ${accept}\r\n\r\n`);
        socket.removeAllListeners('data');
        setTimeout(() => socket.write(websocketFrame(Buffer.from('after-cancel'))), 750);
        setTimeout(() => socket.destroy(), 1500);
    });
});

await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const port = server.address().port;
const { runMain } = await dotnet.withApplicationArguments(`ws://127.0.0.1:${port}/cancellation`).create();
const exitCode = await runMain();
server.close();
process.exitCode = exitCode;
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

    private const string WebSocketCancellationSourceCode = """
#file BrowserWebSocketCancellationProject.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <RuntimeIdentifier>$BrowserRid$</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <SelfContained>true</SelfContained>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
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
globalThis.mtpExitCode = await runMain();

#file Program.cs
using System.Reflection;
using Microsoft.Testing.Platform.Builder;

Type streamType = typeof(TestApplication).Assembly.GetType("Microsoft.Testing.Platform.IPC.BrowserWebSocketDuplexStream", throwOnError: true)!;
MethodInfo connectAsync = streamType.GetMethod("ConnectAsync", BindingFlags.Public | BindingFlags.Static)!;
if (args[0] == "connect-cancel")
{
    using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(250));
    Task connectTask = (Task)connectAsync.Invoke(null, [new Uri(args[1]), cancellationTokenSource.Token])!;
    try
    {
        await connectTask;
        Console.Error.WriteLine("Browser WebSocket connection unexpectedly completed.");
        return 1;
    }
    catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
    {
        Console.WriteLine("BROWSER_WEBSOCKET_CONNECT_CANCELLED=true");
        return 0;
    }
}

Task successfulConnectTask = (Task)connectAsync.Invoke(null, [new Uri(args[0]), CancellationToken.None])!;
await successfulConnectTask;
using Stream stream = (Stream)successfulConnectTask.GetType().GetProperty("Result")!.GetValue(successfulConnectTask)!;

byte[] buffer = new byte[32];
Task<int> zeroCountRead = stream.ReadAsync(buffer, 0, 0, CancellationToken.None);
if (await Task.WhenAny(zeroCountRead, Task.Delay(100)) != zeroCountRead || await zeroCountRead != 0)
{
    Console.Error.WriteLine("Browser WebSocket zero-count read did not complete immediately.");
    return 4;
}
Console.WriteLine("BROWSER_WEBSOCKET_ZERO_COUNT_READ=0");

using (CancellationTokenSource readCancellation = new(TimeSpan.FromMilliseconds(100)))
{
    try
    {
        await stream.ReadAsync(buffer, 0, buffer.Length, readCancellation.Token);
        Console.Error.WriteLine("Browser WebSocket read unexpectedly completed before cancellation.");
        return 2;
    }
    catch (OperationCanceledException) when (readCancellation.IsCancellationRequested)
    {
        Console.WriteLine("BROWSER_WEBSOCKET_READ_CANCELLED=true");
    }
}

int read = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
Console.WriteLine($"BROWSER_WEBSOCKET_MESSAGE_AFTER_CANCEL={System.Text.Encoding.UTF8.GetString(buffer, 0, read)}");

using CancellationTokenSource writeCancellation = new();
writeCancellation.Cancel();
try
{
    await stream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3, writeCancellation.Token);
    Console.Error.WriteLine("Browser WebSocket write unexpectedly ignored pre-cancellation.");
    return 3;
}
catch (OperationCanceledException)
{
    Console.WriteLine("BROWSER_WEBSOCKET_WRITE_CANCELLED=true");
}

return 0;
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
    public async Task BrowserWasmExecution_DotnetTestWebSocketTransport_RunsProtocolSession()
    {
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmWarningAssetAsync();

        (int exitCode, _, _, string combined) = await PublishAndRunUnderNodeAsync(generator, node, NodeWebSocketGatewayRunnerSource, enableWebSocket: true);

        Assert.AreEqual(
            (int)ExitCode.Success,
            exitCode,
            $"Expected the browser-wasm WebSocket protocol run to succeed.{Environment.NewLine}{combined}");
        Assert.Contains("BROWSER_WEBSOCKET_AUTHENTICATED=true", combined);
        Assert.Contains("BROWSER_WEBSOCKET_TRANSPORT=WebSocket", combined);
        Assert.IsTrue(
            TryReadMarkerInt(combined, "BROWSER_WEBSOCKET_REQUESTS=", out int requestCount) && requestCount > 1,
            $"Expected the gateway to receive the handshake plus protocol traffic.{Environment.NewLine}{combined}");
        Assert.Contains("BROWSER_WEBSOCKET_DISCONNECTED=true", combined);
    }

    [TestMethod]
    public async Task BrowserWasmExecution_DotnetTestWebSocketConnect_HonorsCancellationWhileUpgradeIsStalled()
    {
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmWebSocketCancellationAssetAsync();

        (int exitCode, _, _, string combined) = await PublishAndRunUnderNodeAsync(generator, node, NodeStalledWebSocketRunnerSource, enableWebSocket: true);

        Assert.AreEqual(
            (int)ExitCode.Success,
            exitCode,
            $"Expected a stalled browser WebSocket upgrade to be cancelled cleanly.{Environment.NewLine}{combined}");
        Assert.Contains("BROWSER_WEBSOCKET_CONNECT_CANCELLED=true", combined);
    }

    [TestMethod]
    public async Task BrowserWasmExecution_DotnetTestWebSocketReadAndWrite_HonorCancellationWithoutLosingNextMessage()
    {
        string? node = WasmRuntime.LocateNode();
        if (node is null)
        {
            Assert.Inconclusive(WasmRuntime.NodeUnavailableMessage);
            return;
        }

        using TestAsset generator = await GenerateBrowserWasmWebSocketCancellationAssetAsync();

        (int exitCode, _, _, string combined) = await PublishAndRunUnderNodeAsync(generator, node, NodeWebSocketIoCancellationRunnerSource, enableWebSocket: true);

        Assert.AreEqual(
            (int)ExitCode.Success,
            exitCode,
            $"Expected browser WebSocket read/write cancellation to complete cleanly.{Environment.NewLine}{combined}");
        Assert.Contains("BROWSER_WEBSOCKET_READ_CANCELLED=true", combined);
        Assert.Contains("BROWSER_WEBSOCKET_MESSAGE_AFTER_CANCEL=after-cancel", combined);
        Assert.Contains("BROWSER_WEBSOCKET_WRITE_CANCELLED=true", combined);
        Assert.Contains("BROWSER_WEBSOCKET_ZERO_COUNT_READ=0", combined);
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
    private async Task<(int ExitCode, string Output, string Error, string Combined)> PublishAndRunUnderNodeAsync(
        TestAsset generator,
        string node,
        string runnerSource = NodeRunnerSource,
        bool enableWebSocket = false)
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

        return await WasmRuntime.RunUnderNodeAsync(node, appBundle, runnerSource, TestContext.CancellationToken, enableWebSocket);
    }

    private static bool TryReadMarkerInt(string output, string marker, out int value)
    {
        int markerIndex = output.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            value = 0;
            return false;
        }

        int valueStart = markerIndex + marker.Length;
        int valueEnd = output.IndexOfAny(['\r', '\n'], valueStart);
        string text = valueEnd < 0 ? output[valueStart..] : output[valueStart..valueEnd];
        return int.TryParse(text, CultureInfo.InvariantCulture, out value);
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

    private Task<TestAsset> GenerateBrowserWasmWebSocketCancellationAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "BrowserWebSocketCancellationProject",
            WebSocketCancellationSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$BrowserRid$", WasmRuntime.BrowserRid)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

    private Task<TestAsset> GenerateBrowserWasmRunSettingsAssetAsync()
        => TestAsset.GenerateAssetAsync(
            "BrowserRunSettingsProject",
            RunSettingsSourceCode
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$BrowserRid$", WasmRuntime.BrowserRid)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
}
