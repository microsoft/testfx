// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Messages;
#if NETCOREAPP
using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

using MSTest.Acceptance.IntegrationTests.Messages.V100;

#endif
using Microsoft.Extensions.AI;
using Microsoft.Testing.Extensions.AzFoundry;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.TestHost;

namespace Playground;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Opt-out telemetry
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        if (Environment.GetEnvironmentVariable("TESTSERVERMODE") != "1")
        {
#if NETCOREAPP
            // To attach to the children
            // Microsoft.Testing.TestInfrastructure.DebuggerUtility.AttachCurrentProcessToParentVSProcess();
#endif

            ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

            // Test MSTest
            // testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

            // Add Chat client factory
            testApplicationBuilder.AddChatClientFactory(_ => new AzureOpenAIChatClientFactory());

            // Test a custom local test framework
            testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, s) => new DummyAdapter(s));

            // Custom test host controller extension
            // testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(s => new OutOfProc(s.GetMessageBus()));

            // Enable Trx
            // testApplicationBuilder.AddTrxReportProvider();

            // Enable Telemetry
            // testApplicationBuilder.AddAppInsightsTelemetryProvider();
            using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
            return await testApplication.RunAsync();
        }
        else
        {
#if NETFRAMEWORK
            throw new NotSupportedException("Server mode is not supported on .NET Framework");
#else
            Environment.SetEnvironmentVariable("TESTSERVERMODE", "0");
            using TestingPlatformClient client = await TestingPlatformClientFactory.StartAsServerAndConnectToTheClientAsync(Environment.ProcessPath!);

            await client.InitializeAsync();
            List<TestNodeUpdate> testNodeUpdates = [];
            ResponseListener discoveryResponse = await client.DiscoverTestsAsync(Guid.NewGuid(), node =>
            {
                testNodeUpdates.AddRange(node);
                return Task.CompletedTask;
            });
            await discoveryResponse.WaitCompletionAsync();

            ResponseListener runRequest = await client.RunTestsAsync(Guid.NewGuid(), [.. testNodeUpdates.Select(x => x.Node)], _ => Task.CompletedTask);
            await runRequest.WaitCompletionAsync();

            await client.ExitAsync();

            return 0;
#endif
        }
    }
}

internal sealed class DummyAdapter : ITestFramework, IDataProducer
{
    private readonly IServiceProvider _serviceProvider;

    public DummyAdapter(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public string Uid => nameof(DummyAdapter);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        try
        {
            using IChatClient? chatClient = _serviceProvider.GetChatClient();
            if (chatClient != null)
            {
                ChatResponse response = await chatClient.GetResponseAsync(chatMessage: "Hello, world!", cancellationToken: context.CancellationToken);
            }

            MyService.DoSomething();
        }
        catch (Exception e)
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(new SessionUid("1"), new Microsoft.Testing.Platform.Extensions.Messages.TestNode
            {
                Uid = "2",
                DisplayName = "Blah",
                Properties = new PropertyBag(new FailedTestNodeStateProperty(e)),
            }));
        }

        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

public sealed class MyService
{
    public static void DoSomething()
    {
        try
        {
            InnerDoSomething();
        }
        catch (Exception e)
        {
            throw new WrappedException("Service failed!", e);
        }
    }

    private static void InnerDoSomething() => throw new InvalidOperationException("Error code 488");
}

public sealed class WrappedException(string message, Exception innerException) : Exception(message, innerException);

public sealed class OutOfProc : ITestHostProcessLifetimeHandler, IDataProducer
{
    private readonly IMessageBus _messageBus;

    public string Uid
        => nameof(OutOfProc);

    public string Version
        => "1.0.0";

    public string DisplayName
        => nameof(OutOfProc);

    public string Description
        => nameof(OutOfProc);

    public Type[] DataTypesProduced
        => [typeof(FileArtifact)];

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(true);

    public OutOfProc(IMessageBus messageBus)
        => _messageBus = messageBus;

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
        => await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(@"C:\sampleFile"), "Sample", "sample description"));

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
        => Task.CompletedTask;
}
