// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Hosts;

#pragma warning disable TPEXP // Command-line parse models are experimental.

[TestClass]
public sealed class ToolsHostTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_SuccessReturnsToolExitAndPropagatesCancellationToken()
    {
        using CancellationTokenSource cancellation = new();
        Mock<ITool> tool = CreateTool("sample");
        tool.Setup(candidate => candidate.RunAsync(cancellation.Token)).ReturnsAsync(43);
        HostFixture fixture = CreateHost("sample", [tool.Object], cancellation.Token);

        Assert.AreEqual(43, await fixture.Host.RunAsync());
        Assert.IsEmpty(fixture.Errors);
        tool.Verify(candidate => candidate.RunAsync(cancellation.Token), Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_MissingToolAndDuplicateRegistrationCannotExecuteTools()
    {
        Mock<ITool> first = CreateTool("sample");
        Mock<ITool> second = CreateTool("sample");
        HostFixture duplicate = CreateHost("sample", [first.Object, second.Object], TestContext.CancellationToken);
        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => duplicate.Host.RunAsync());
        Assert.AreEqual("Tool 'sample' is registered more than once.", error.Message);
        first.Verify(candidate => candidate.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
        HostFixture missing = CreateHost("missing", [first.Object], TestContext.CancellationToken);
        Assert.AreEqual(5, await missing.Host.RunAsync());
        Assert.Contains("Tool 'missing' not found", Assert.ContainsSingle(missing.Errors));
        first.Verify(candidate => candidate.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_UnknownOptionsAndArityErrorsReturnInvalidCommandLine()
    {
        Mock<ITool> tool = CreateTool("sample");
        Mock<IToolCommandLineOptionsProvider> provider = CreateRunProvider("input", ArgumentArity.ExactlyOne);
        HostFixture unknown = CreateHost("sample", [tool.Object], TestContext.CancellationToken, options: [new("unknown", [])], providers: [provider.Object]);
        Assert.AreEqual(5, await unknown.Host.RunAsync());
        Assert.Contains("Unknown option 'unknown'", Assert.ContainsSingle(unknown.Errors));
        HostFixture absent = CreateHost("sample", [tool.Object], TestContext.CancellationToken, providers: [provider.Object]);
        Assert.AreEqual(5, await absent.Host.RunAsync());
        Assert.Contains("must be specified at least 1 times", Assert.ContainsSingle(absent.Errors));
        HostFixture repeated = CreateHost("sample", [tool.Object], TestContext.CancellationToken, options: [new("input", ["one", "two"])], providers: [provider.Object]);
        Assert.AreEqual(5, await repeated.Host.RunAsync());
        Assert.Contains("between 1 and 1 times", Assert.ContainsSingle(repeated.Errors));
        tool.Verify(candidate => candidate.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_ArgumentAndCombinationValidationFailuresPreventExecution()
    {
        Mock<ITool> tool = CreateTool("sample");
        Mock<IToolCommandLineOptionsProvider> provider = CreateRunProvider("input", ArgumentArity.ExactlyOne);
        provider.Setup(candidate => candidate.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()))
            .ReturnsAsync(ValidationResult.Invalid("bad value"));
        HostFixture arguments = CreateHost("sample", [tool.Object], TestContext.CancellationToken, options: [new("input", ["value"])], providers: [provider.Object]);
        Assert.AreEqual(5, await arguments.Host.RunAsync());
        Assert.Contains("bad value", Assert.ContainsSingle(arguments.Errors));
        provider.Setup(candidate => candidate.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()))
            .ReturnsAsync(ValidationResult.Valid());
        provider.Setup(candidate => candidate.ValidateCommandLineOptionsAsync(It.IsAny<ICommandLineOptions>()))
            .ReturnsAsync(ValidationResult.Invalid("bad combination"));
        HostFixture combination = CreateHost("sample", [tool.Object], TestContext.CancellationToken, options: [new("input", ["value"])], providers: [provider.Object]);
        Assert.AreEqual(5, await combination.Host.RunAsync());
        Assert.AreEqual("bad combination", Assert.ContainsSingle(combination.Errors));
        tool.Verify(candidate => candidate.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_DuplicateProviderOptionsAreRejectedBeforeRunning()
    {
        Mock<ITool> tool = CreateTool("sample");
        HostFixture fixture = CreateHost("sample", [tool.Object], TestContext.CancellationToken, providers:
            [CreateRunProvider("input", ArgumentArity.Zero).Object, CreateRunProvider("input", ArgumentArity.Zero).Object]);
        Assert.AreEqual(5, await fixture.Host.RunAsync());
        Assert.Contains("declared by multiple providers", Assert.ContainsSingle(fixture.Errors));
        tool.Verify(candidate => candidate.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_ServerProtocolIsExitedEvenWhenToolThrows()
    {
        Mock<ITool> tool = CreateTool("sample");
        tool.Setup(candidate => candidate.RunAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("tool failure"));
        var protocol = new Mock<IPushOnlyProtocol>();
        protocol.SetupGet(candidate => candidate.IsServerMode).Returns(true);
        protocol.Setup(candidate => candidate.OnExitAsync()).Returns(Task.CompletedTask);
        HostFixture fixture = CreateHost("sample", [tool.Object], TestContext.CancellationToken, protocol: protocol.Object);

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => fixture.Host.RunAsync());
        Assert.AreEqual("tool failure", error.Message);
        protocol.Verify(candidate => candidate.OnExitAsync(), Times.Once);
    }

    [TestMethod]
    public async Task RunAsync_ArtifactProtocolIncompatibilityReturnsSpecificExitAndClosesProtocol()
    {
        Mock<ITool> tool = CreateTool(ArtifactPostProcessingDispatcherTool.ToolName);
        var protocol = new Mock<IPushOnlyProtocol>();
        protocol.SetupGet(candidate => candidate.IsServerMode).Returns(true);
        protocol.Setup(candidate => candidate.IsCompatibleProtocolAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<byte, string>>()))
            .ReturnsAsync(false);
        protocol.Setup(candidate => candidate.OnExitAsync()).Returns(Task.CompletedTask);
        HostFixture fixture = CreateHost(ArtifactPostProcessingDispatcherTool.ToolName, [tool.Object], TestContext.CancellationToken, protocol: protocol.Object);

        Assert.AreEqual((int)ExitCode.IncompatibleProtocolVersion, await fixture.Host.RunAsync());
        Assert.IsEmpty(fixture.Errors);
        protocol.Verify(candidate => candidate.OnExitAsync(), Times.Once);
        tool.Verify(candidate => candidate.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void AggregateArguments_CombinesRepeatedOptionOccurrences()
    {
        CommandLineParseOption[] occurrences =
        [
            new("input", ["a.trx"]),
            new("input", ["b.trx"]),
        ];

        Assert.AreSequenceEqual(["a.trx", "b.trx"], ToolsHost.AggregateArguments(occurrences));
    }

    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_ReturnsProviderFailure()
    {
        var provider = new Mock<IToolCommandLineOptionsProvider>();
        provider.Setup(candidate => candidate.ValidateCommandLineOptionsAsync(It.IsAny<ICommandLineOptions>()))
            .ReturnsAsync(ValidationResult.Invalid("invalid combination"));

        ValidationResult result = await ToolsHost.ValidateCommandLineOptionsAsync(
            [provider.Object],
            Mock.Of<ICommandLineOptions>());

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("invalid combination", result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateToolProviders_DuplicateToolOption_ReturnsInvalid()
    {
        IToolCommandLineOptionsProvider first = CreateToolProvider("input");
        IToolCommandLineOptionsProvider second = CreateToolProvider("input");

        ValidationResult result = CommandLineOptionsValidator.ValidateToolProviders([first, second], []);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("declared by multiple providers", result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateToolProviders_SystemOptionCollision_ReturnsInvalid()
    {
        IToolCommandLineOptionsProvider toolProvider = CreateToolProvider("system-option");
        var systemProvider = new Mock<ICommandLineOptionsProvider>();
        systemProvider.Setup(provider => provider.GetCommandLineOptions())
            .Returns([new("system-option", "system", ArgumentArity.Zero, isHidden: false)]);
        systemProvider.SetupGet(provider => provider.DisplayName).Returns("system");

        ValidationResult result = CommandLineOptionsValidator.ValidateToolProviders([toolProvider], [systemProvider.Object]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("reserved", result.ErrorMessage);
    }

    private static Mock<ITool> CreateTool(string name)
    {
        var tool = new Mock<ITool>();
        tool.SetupGet(candidate => candidate.Name).Returns(name);
        tool.SetupGet(candidate => candidate.DisplayName).Returns("sample tool");
        tool.Setup(candidate => candidate.RunAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        return tool;
    }

    private static Mock<IToolCommandLineOptionsProvider> CreateRunProvider(string name, ArgumentArity arity)
    {
        var provider = new Mock<IToolCommandLineOptionsProvider>();
        provider.SetupGet(candidate => candidate.ToolName).Returns("sample");
        provider.SetupGet(candidate => candidate.Uid).Returns("sample-provider");
        provider.SetupGet(candidate => candidate.DisplayName).Returns("sample provider");
        provider.Setup(candidate => candidate.GetCommandLineOptions()).Returns([new(name, "Description.", arity, false)]);
        provider.Setup(candidate => candidate.ValidateOptionArgumentsAsync(It.IsAny<CommandLineOption>(), It.IsAny<string[]>()))
            .ReturnsAsync(ValidationResult.Valid());
        provider.Setup(candidate => candidate.ValidateCommandLineOptionsAsync(It.IsAny<ICommandLineOptions>()))
            .ReturnsAsync(ValidationResult.Valid());
        return provider;
    }

    private static HostFixture CreateHost(
        string name, ITool[] tools, CancellationToken token, CommandLineParseOption[]? options = null,
        ICommandLineOptionsProvider[]? providers = null, IPushOnlyProtocol? protocol = null)
    {
        ServiceProvider services = new();
        services.AddService(Mock.Of<ITestApplicationCancellationTokenSource>(source => source.CancellationToken == token));
        services.AddService(Mock.Of<IConsole>());
        if (protocol is not null)
        {
            services.AddService(protocol);
        }

        var handler = new CommandLineHandler(
            new CommandLineParseResult(name, options ?? [], []), providers ?? [], [],
            Mock.Of<ITestApplicationModuleInfo>(), Mock.Of<IRuntimeFeature>());
        List<string> errors = [];
        var output = new Mock<IOutputDevice>();
        output.Setup(device => device.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) =>
            {
                if (data is ErrorMessageOutputDeviceData error)
                {
                    errors.Add(error.Message);
                }
            })
            .Returns(Task.CompletedTask);
        return new HostFixture(new ToolsHost(tools, services, handler, output.Object), errors);
    }

    private static IToolCommandLineOptionsProvider CreateToolProvider(string optionName)
    {
        var provider = new Mock<IToolCommandLineOptionsProvider>();
        provider.Setup(candidate => candidate.GetCommandLineOptions())
            .Returns([new(optionName, optionName, ArgumentArity.ExactlyOne, isHidden: false)]);
        provider.SetupGet(candidate => candidate.DisplayName).Returns(Guid.NewGuid().ToString());
        return provider.Object;
    }

    private sealed record HostFixture(ToolsHost Host, List<string> Errors);
}
