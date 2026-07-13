// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new DummyFramework());

testApplicationBuilder.AddAppInsightsTelemetryProvider();

// The following extensions are intentionally NOT registered on browser-wasm:
// - AddTrxReportProvider: with --report-trx enabled, TrxDataConsumer creates a
//   TrxResultStreamingStore whose background writer calls ITask.RunLongRunning (and uses
//   BlockingCollection<T>), both unsupported on browser; the TRX lifecycle handlers are
//   themselves gated by OperatingSystem.IsBrowser() (see TrxReportExtensions). Registering it
//   would let a user pass --report-trx and hit PlatformNotSupportedException.
// - AddHangDumpProvider / AddCrashDumpProvider: dumps rely on System.Diagnostics.Process,
//   which is unsupported in the browser sandbox (see #8557).
// - AddAzureDevOpsProvider: its HttpClient sets AutomaticDecompression, which the browser
//   HttpClientHandler does not support.
// Threading is not an issue for the registered providers: the platform detects the
// single-threaded wasm runtime (RuntimeFeatureHelper.IsMultiThreaded == false) and runs the
// pipeline inline, exactly as it does on wasi-wasm.
using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();

internal sealed class DummyFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyFramework);

    public string Version => "1.0.0";

    public string DisplayName => "DummyFramework";

    public string Description => DisplayName;

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            DisplayName = "Test display name",
            Uid = "Uid1",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
        }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
