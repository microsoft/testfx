// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new DummyFramework());

testApplicationBuilder.AddTrxReportProvider();
testApplicationBuilder.AddAppInsightsTelemetryProvider();
testApplicationBuilder.AddCrashDumpProvider();
testApplicationBuilder.AddHangDumpProvider();
testApplicationBuilder.AddAzureDevOpsProvider();
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
        }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
