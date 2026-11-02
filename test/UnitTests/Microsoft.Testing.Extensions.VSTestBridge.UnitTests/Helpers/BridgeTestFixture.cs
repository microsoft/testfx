// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using TestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;

internal sealed class BridgeTestFixture
{
    public BridgeTestFixture(bool useFullyQualifiedNameAsUid = false, params ITestFrameworkCapability[] capabilities)
    {
        var frameworkCapabilities = new TestFrameworkCapabilities(capabilities);
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        LoggerFactory = loggerFactory.Object;

        MessageBus.Setup(x => x.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((producer, data) => PublishedMessages.Add((producer, data)))
            .Returns(Task.CompletedTask);
        OutputDevice.Setup(x => x.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("tests.dll");

        // Always take the mocked command-line file path, never inherited runsettings environment values.
        CommandLineOptions.Setup(x => x.TryGetOptionArgumentList("settings", out It.Ref<string[]?>.IsAny))
            .Returns((string _, out string[]? arguments) =>
            {
                arguments = ["tests.runsettings"];
                return true;
            });
        FileSystem.Setup(x => x.ExistFile("tests.runsettings")).Returns(true);
        FileSystem.Setup(x => x.ReadAllText("tests.runsettings")).Returns("""
            <RunSettings>
              <RunConfiguration>
                <DesignMode>false</DesignMode>
                <CollectSourceInformation>false</CollectSourceInformation>
                <ResultsDirectory>results</ResultsDirectory>
              </RunConfiguration>
            </RunSettings>
            """);

        var serviceProvider = new ServiceProvider();
        serviceProvider.AddServices(
        [
            Mock.Of<IConfiguration>(),
            CommandLineOptions.Object,
            LoggerFactory,
            FileSystem.Object,
            ClientInfo,
            OutputDevice.Object,
            ModuleInfo.Object,
            frameworkCapabilities,
            MessageBus.Object,
        ]);
        Framework = new StubFramework(serviceProvider, frameworkCapabilities, useFullyQualifiedNameAsUid);
    }

    public StubFramework Framework { get; }

    public TestSessionContext Session { get; } = new(new SessionUid("bridge-session"));

    public Mock<IMessageBus> MessageBus { get; } = new();

    public Mock<ICommandLineOptions> CommandLineOptions { get; } = new();

    public Mock<IFileSystem> FileSystem { get; } = new();

    public Mock<IOutputDevice> OutputDevice { get; } = new();

    public Mock<ITestApplicationModuleInfo> ModuleInfo { get; } = new();

    public ILoggerFactory LoggerFactory { get; }

    public IClientInfo ClientInfo { get; } = new ClientInfoService("bridge-unit-tests", "1.0", new ClientCapabilitiesService(DeclaredIsStateful: false));

    public List<(IDataProducer Producer, IData Data)> PublishedMessages { get; } = [];

    public FrameworkHandlerAdapter CreateFrameworkHandler(
        IFrameworkHandle? frameworkHandle = null,
        CancellationToken cancellationToken = default,
        string[]? assemblyPaths = null,
        bool isTrxEnabled = false)
        => new(Framework, Session, assemblyPaths ?? ["tests.dll"], ModuleInfo.Object, null,
            CommandLineOptions.Object, ClientInfo, MessageBus.Object, OutputDevice.Object,
            LoggerFactory, isTrxEnabled, cancellationToken, frameworkHandle);

    public TestCaseDiscoverySinkAdapter CreateDiscoverySink(
        ITestCaseDiscoverySink? discoverySink = null,
        CancellationToken cancellationToken = default,
        string[]? assemblyPaths = null)
        => new(Framework, Session, assemblyPaths ?? ["tests.dll"], ModuleInfo.Object, null,
            CommandLineOptions.Object, ClientInfo, MessageBus.Object, LoggerFactory,
            isTrxEnabled: false, cancellationToken, discoverySink);

    internal sealed class StubFramework(
        IServiceProvider serviceProvider,
        ITestFrameworkCapabilities capabilities,
        bool useFullyQualifiedNameAsUid = false) : VSTestBridgedTestFrameworkBase(serviceProvider, capabilities)
    {
        public override string Uid => "bridge";

        public override string Version => "1.0";

        public override string DisplayName => "Bridge tests";

        public override string Description => "In-memory bridge test framework.";

        public Func<TestExecutionRequest, IMessageBus, CancellationToken, Task> OnExecute { get; set; } = static (_, _, _) => Task.CompletedTask;

        protected internal override bool UseFullyQualifiedNameAsTestNodeUid => useFullyQualifiedNameAsUid;

        protected internal override void AddAdditionalProperties(TestNode testNode, TestCase testCase)
            => testNode.Properties.Add(new SerializableKeyValuePairStringProperty("bridge-test", testCase.FullyQualifiedName));

        public override Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public override Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
            => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

        public override Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
            => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

        protected override Task ExecuteRequestAsync(TestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
            => OnExecute(request, messageBus, cancellationToken);

        protected override Task DiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task RunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
