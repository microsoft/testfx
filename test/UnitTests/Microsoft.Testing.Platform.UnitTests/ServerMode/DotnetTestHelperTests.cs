// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DotnetTestHelperTests
{
    private static readonly ICommandLineOptionsProvider[] SystemCommandLineOptionsProviders = [new PlatformCommandLineProvider()];
    private static readonly ICommandLineOptionsProvider[] ExtensionCommandLineOptionsProviders = [];

    [TestMethod]
    [DataRow(new string[] { "--server", "dotnettestcli" }, true)]
    [DataRow(new string[] { "--server", "DOTNETTESTCLI" }, true, DisplayName = "CaseInsensitive")]
    [DataRow(new string[] { "--server", "vstestprovider" }, false, DisplayName = "DifferentProtocolName")]
    [DataRow(new string[] { "--server" }, false, DisplayName = "NoArgument")]
    [DataRow(new string[0], false, DisplayName = "OptionNotSet")]
    public void HasDotnetTestServerOption_ReturnsExpectedResult(string[] args, bool expected)
    {
        CommandLineHandler handler = CreateCommandLineHandler(args);

        bool actual = handler.HasDotnetTestServerOption();

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void HasDotnetTestServerOption_MultipleArgumentsForServerOption_ReturnsFalse()
    {
        // --server only accepts a single argument. Passing two arguments makes the option's argument list
        // length != 1, so HasDotnetTestServerOption must reject it even though the first value matches.
        CommandLineHandler handler = CreateCommandLineHandler(["--server", "dotnettestcli", "extra"]);

        Assert.IsFalse(handler.HasDotnetTestServerOption());
    }

    [TestMethod]
    public void TryGetDotnetTestTransport_HttpTransportArgument_ReturnsTrueWithHttpKind()
    {
        CommandLineHandler handler = CreateCommandLineHandler(["--dotnet-test-transport", "http"]);

        bool result = handler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport);

        Assert.IsTrue(result);
        Assert.AreEqual(DotnetTestTransportKind.Http, transport);
    }

    [TestMethod]
    public void TryGetDotnetTestTransport_HttpTransportArgumentIsCaseInsensitive_ReturnsTrueWithHttpKind()
    {
        CommandLineHandler handler = CreateCommandLineHandler(["--dotnet-test-transport", "HTTP"]);

        bool result = handler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport);

        Assert.IsTrue(result);
        Assert.AreEqual(DotnetTestTransportKind.Http, transport);
    }

    [TestMethod]
    public void TryGetDotnetTestTransport_PipeOptionSetWithoutTransportOption_ReturnsTrueWithNamedPipeKind()
    {
        CommandLineHandler handler = CreateCommandLineHandler(["--dotnet-test-pipe", "42"]);

        bool result = handler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport);

        Assert.IsTrue(result);
        Assert.AreEqual(DotnetTestTransportKind.NamedPipe, transport);
    }

    [TestMethod]
    public void TryGetDotnetTestTransport_TransportOptionWithNonHttpArgumentAndNoPipeOption_ReturnsFalse()
    {
        // The transport option's argument is validated by PlatformCommandLineProvider to be either "pipe" or
        // "http", but TryGetDotnetTestTransport itself only special-cases "http"; any other single argument
        // (e.g. the explicit "pipe" value) falls through to the pipe-option check, which is not set here.
        CommandLineHandler handler = CreateCommandLineHandler(["--dotnet-test-transport", "pipe"]);

        bool result = handler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport);

        Assert.IsFalse(result);
        Assert.AreEqual(default, transport);
    }

    [TestMethod]
    public void TryGetDotnetTestTransport_NeitherOptionSet_ReturnsFalse()
    {
        CommandLineHandler handler = CreateCommandLineHandler([]);

        bool result = handler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport);

        Assert.IsFalse(result);
        Assert.AreEqual(default, transport);
    }

    [TestMethod]
    public void TryGetDotnetTestTransport_HttpTransportTakesPrecedenceOverPipeOption_ReturnsHttpKind()
    {
        // When both the http transport and the pipe option are present, the http transport check runs first
        // and short-circuits before the pipe-option fallback is even evaluated.
        CommandLineHandler handler = CreateCommandLineHandler(["--dotnet-test-transport", "http", "--dotnet-test-pipe", "42"]);

        bool result = handler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport);

        Assert.IsTrue(result);
        Assert.AreEqual(DotnetTestTransportKind.Http, transport);
    }

    private static CommandLineHandler CreateCommandLineHandler(string[] args)
    {
        CommandLineParseResult parseResult = CommandLineParser.Parse(args, new SystemEnvironment());
        return new CommandLineHandler(
            parseResult,
            ExtensionCommandLineOptionsProviders,
            SystemCommandLineOptionsProviders,
            new Mock<ITestApplicationModuleInfo>().Object,
            new Mock<IRuntimeFeature>().Object);
    }
}
