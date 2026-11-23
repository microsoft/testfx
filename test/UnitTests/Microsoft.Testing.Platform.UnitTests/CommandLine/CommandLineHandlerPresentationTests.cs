// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineHandlerPresentationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PrintInfoAsync_RendersEverySectionWithSeparatorsInOrder()
    {
        AssemblyInformationalVersionAttribute? versionAttribute = typeof(CommandLineHandler).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Assert.IsNotNull(versionAttribute);
#if NETCOREAPP
        string runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
        string runtimeLocationLine = string.Empty;
#else
        string runtimeInformation = RuntimeInformation.FrameworkDescription;
        string runtimeLocationLine = $"  Runtime location: {typeof(object).Assembly.Location}{Environment.NewLine}";
#endif

        ICommandLineOptionsProvider system = CreateProvider("system-provider", [new("sys-opt", "system description", ArgumentArity.Zero, false)]);
        ICommandLineOptionsProvider extension = CreateProvider("ext-provider", [new("ext-opt", "extension description", new ArgumentArity(1, 2), true)]);
        var toolProvider = new Mock<IToolCommandLineOptionsProvider>();
        toolProvider.SetupGet(value => value.ToolName).Returns("alpha");
        toolProvider.SetupGet(value => value.Uid).Returns("alpha-provider");
        toolProvider.SetupGet(value => value.DisplayName).Returns("alpha-provider display");
        toolProvider.SetupGet(value => value.Version).Returns("1.2.3");
        toolProvider.SetupGet(value => value.Description).Returns("alpha-provider description");
        toolProvider.Setup(value => value.GetCommandLineOptions()).Returns([new("input", "input description", ArgumentArity.ExactlyOne, false)]);
        ITool[] tools = [CreateTool("b-tool", "omega"), CreateTool("a-tool", "alpha")];
        var runtimeFeature = new Mock<IRuntimeFeature>();
        runtimeFeature.SetupGet(value => value.IsDynamicCodeSupported).Returns(true);
        CommandLineHandler handler = CreateHandler([system], [extension, toolProvider.Object], runtimeFeature.Object);
        List<string> output = [];

        await handler.PrintInfoAsync(CreateOutputDevice(output), tools, TestContext.CancellationToken);

        string expected = $$"""
            Microsoft Testing Platform:
              Version: {{versionAttribute.InformationalVersion}}
              Dynamic Code Supported: True
              Runtime information: {{runtimeInformation}}
            {{runtimeLocationLine}}  Test module: sample-module

            Built-in command line providers:
              system-provider
                Name: system-provider display
                Version: 1.2.3
                Description: system-provider description
                Options:
                  --sys-opt
                    Arity: 0
                    Hidden: False
                    Description: system description

            Registered command line providers:
              ext-provider
                Name: ext-provider display
                Version: 1.2.3
                Description: ext-provider description
                Options:
                  --ext-opt
                    Arity: 1..2
                    Hidden: True
                    Description: extension description

            Registered tools:
              a-tool
                Command: alpha
                Name: alpha display
                Version: 1.2.3
                Description: alpha description
                Tool command line providers:
                  alpha-provider
                    Name: alpha-provider display
                    Version: 1.2.3
                    Description: alpha-provider description
                    Options:
                      --input
                        Arity: 1
                        Hidden: False
                        Description: input description
              b-tool
                Command: omega
                Name: omega display
                Version: 1.2.3
                Description: omega description
                Tool command line providers:
                  There are no registered command line providers.

            """;
        Assert.AreEqual(Normalize(expected), Normalize(string.Join("\n", output)));
    }

    [TestMethod]
    public async Task PrintInfoAsync_WithoutAnyRegistrationsRendersAbsenceMessagesBetweenSeparators()
    {
        CommandLineHandler handler = CreateHandler([], [], Mock.Of<IRuntimeFeature>());
        List<string> output = [];

        await handler.PrintInfoAsync(CreateOutputDevice(output), null, TestContext.CancellationToken);

        // Everything after the platform header is deterministic.
        int builtIn = output.IndexOf("Built-in command line providers:");
        Assert.IsGreaterThan(0, builtIn);
        Assert.AreEqual(string.Empty, output[builtIn - 1]);
        string actual = string.Join("\n", output.Skip(builtIn));
        string expected = """
            Built-in command line providers:
              There are no built-in command line providers.

            Registered command line providers:
              There are no registered command line providers.

            Registered tools:
              There are no registered tools.

            """;
        Assert.AreEqual(Normalize(expected), Normalize(actual));
    }

    [TestMethod]
    public async Task PrintHelpAsync_RendersUsageBuiltInAndExtensionOptionsWithSeparators()
    {
        ICommandLineOptionsProvider system = CreateProvider("system", [new("sys-opt", "system description", ArgumentArity.Zero, false, isBuiltIn: true)]);
        ICommandLineOptionsProvider extension = CreateProvider("extension", [new("ext-opt", "extension description", ArgumentArity.Zero, false)]);
        CommandLineHandler handler = CreateHandler([system], [extension], Mock.Of<IRuntimeFeature>());
        List<string> output = [];

        await handler.PrintHelpAsync(CreateOutputDevice(output), [], TestContext.CancellationToken);

        string expected = $$"""
            Usage <test application runner> [option providers] [extension option providers]

            Execute a .NET Test Application.

            Options:
                --sys-opt
                    system description


            Extension options:
                --ext-opt
                    extension description



            """;
        Assert.AreEqual(Normalize(expected), Normalize(string.Join("\n", output)));
    }

    [TestMethod]
    public async Task PrintHelpAsync_WithoutExtensionOptionsReportsNoExtensionRegistered()
    {
        CommandLineHandler handler = CreateHandler([], [], Mock.Of<IRuntimeFeature>());
        List<string> output = [];

        await handler.PrintHelpAsync(CreateOutputDevice(output), null, TestContext.CancellationToken);

        string expected = $$"""
            Usage <test application runner> [option providers] [extension option providers]

            Execute a .NET Test Application.

            Options:

            Extension options:
                No extension registered.


            """;
        Assert.AreEqual(Normalize(expected), Normalize(string.Join("\n", output)));
    }

    [TestMethod]
    public void TryGetOptionArgumentListOrDefault_WithoutConfiguration_ReadsTheParseResult()
    {
        var parseResult = new CommandLineParseResult(null, [new("name", ["explicit-value"])], []);
        var handler = new CommandLineHandler(parseResult, [], [], Mock.Of<ITestApplicationModuleInfo>(), Mock.Of<IRuntimeFeature>());

        Assert.IsTrue(handler.TryGetOptionArgumentListOrDefault("name", out string[]? arguments));
        Assert.AreSequenceEqual(["explicit-value"], arguments);
        Assert.IsFalse(handler.TryGetOptionArgumentListOrDefault("missing", out string[]? missing));
        Assert.IsNull(missing);
    }

    private static string Normalize(string value)
        => value.Replace("\r\n", "\n");

    private static CommandLineHandler CreateHandler(ICommandLineOptionsProvider[] system, ICommandLineOptionsProvider[] extensions, IRuntimeFeature runtimeFeature)
    {
        var module = new Mock<ITestApplicationModuleInfo>();
        module.Setup(value => value.GetDisplayName()).Returns("sample-module");
        return new CommandLineHandler(new CommandLineParseResult(null, [], []), extensions, system, module.Object, runtimeFeature);
    }

    private static ICommandLineOptionsProvider CreateProvider(string uid, CommandLineOption[] options)
    {
        var provider = new Mock<ICommandLineOptionsProvider>();
        provider.SetupGet(value => value.Uid).Returns(uid);
        provider.SetupGet(value => value.DisplayName).Returns(uid + " display");
        provider.SetupGet(value => value.Version).Returns("1.2.3");
        provider.SetupGet(value => value.Description).Returns(uid + " description");
        provider.Setup(value => value.GetCommandLineOptions()).Returns(options);
        return provider.Object;
    }

    private static ITool CreateTool(string uid, string name)
    {
        var tool = new Mock<ITool>();
        tool.SetupGet(value => value.Uid).Returns(uid);
        tool.SetupGet(value => value.Name).Returns(name);
        tool.SetupGet(value => value.DisplayName).Returns(name + " display");
        tool.SetupGet(value => value.Version).Returns("1.2.3");
        tool.SetupGet(value => value.Description).Returns(name + " description");
        return tool.Object;
    }

    private static IOutputDevice CreateOutputDevice(List<string> output)
    {
        var device = new Mock<IOutputDevice>();
        device.Setup(value => value.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) =>
                output.Add(data switch
                {
                    FormattedTextOutputDeviceData formatted => new string(' ', formatted.Padding ?? 0) + formatted.Text,
                    TextOutputDeviceData text => text.Text,
                    _ => throw new InvalidOperationException("Unexpected CLI presentation output."),
                }))
            .Returns(Task.CompletedTask);
        return device.Object;
    }
}
