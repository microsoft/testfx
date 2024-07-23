// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Hosts;

internal class TestFrameworkBuilderData(ServiceProvider serviceProvider, ITestExecutionRequestFactory testExecutionRequestFactory,
    ITestFrameworkInvoker testExecutionRequestInvoker, ITestExecutionFilterFactory testExecutionFilterFactory,
    IPlatformOutputDevice platformOutputDisplayService, IEnumerable<IDataConsumer> serverPerCallConsumers,
    TestFrameworkManager testFrameworkManager, TestHostManager testSessionManager, MessageBusProxy messageBusProxy,
    bool isForDiscoveryRequest,
    NamedPipeClient? dotnetTestPipeClient = null)
{
    public ServiceProvider ServiceProvider { get; } = serviceProvider;

    public ITestExecutionRequestFactory TestExecutionRequestFactory { get; } = testExecutionRequestFactory;

    public ITestFrameworkInvoker TestExecutionRequestInvoker { get; } = testExecutionRequestInvoker;

    public ITestExecutionFilterFactory TestExecutionFilterFactory { get; } = testExecutionFilterFactory;

    public IPlatformOutputDevice PlatformOutputDisplayService { get; } = platformOutputDisplayService;

    public IEnumerable<IDataConsumer> ServerPerCallConsumers { get; } = serverPerCallConsumers;

    public TestFrameworkManager TestFrameworkManager { get; } = testFrameworkManager;

    public TestHostManager TestSessionManager { get; } = testSessionManager;

    public MessageBusProxy MessageBusProxy { get; } = messageBusProxy;

    public bool IsForDiscoveryRequest { get; } = isForDiscoveryRequest;

    public NamedPipeClient? DotnetTestPipeClient { get; } = dotnetTestPipeClient;
}
