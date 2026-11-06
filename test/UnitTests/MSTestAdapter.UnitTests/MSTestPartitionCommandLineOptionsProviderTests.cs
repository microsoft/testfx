// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestPartitionCommandLineOptionsProviderTests : TestContainer
{
    public void GetCommandLineOptions_ExposesOnlyPartitionWithExactlyOneArgument()
    {
        var provider = new MSTestPartitionCommandLineOptionsProvider(new MSTestExtension());
        CommandLineOption option = provider.GetCommandLineOptions().Should().ContainSingle().Which;

        option.Name.Should().Be("partition");
        option.Arity.Should().Be(ArgumentArity.ExactlyOne);
        option.IsHidden.Should().BeFalse();
        option.Description.Should().Be(PlatformAdapterResources.PartitionOptionDescription);
    }

    public void AddMSTest_RegistersPartitionAlongsideExistingNativeOptions()
    {
        var builder = new Mock<ITestApplicationBuilder> { DefaultValue = DefaultValue.Mock };
        List<Func<ICommandLineOptionsProvider>> factories = [];
        List<Func<IServiceProvider, ICommandLineOptionsProvider>> serviceFactories = [];
        Mock.Get(builder.Object.CommandLine)
            .Setup(value => value.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
            .Callback<Func<ICommandLineOptionsProvider>>(factories.Add);
        Mock.Get(builder.Object.CommandLine)
            .Setup(value => value.AddProvider(It.IsAny<Func<IServiceProvider, ICommandLineOptionsProvider>>()))
            .Callback<Func<IServiceProvider, ICommandLineOptionsProvider>>(serviceFactories.Add);

        builder.Object.AddMSTest(() => []);

        ICommandLineOptionsProvider[] providers =
        [
            .. factories.Select(factory => factory()),
            .. serviceFactories.Select(factory => factory(Mock.Of<IServiceProvider>())),
        ];
        providers.SelectMany(provider => provider.GetCommandLineOptions()).Select(option => option.Name)
            .Should().Equal("settings", "filter", "partition", "test-parameter", "maximum-failed-tests");
        providers.OfType<MSTestPartitionCommandLineOptionsProvider>().Should().ContainSingle();
    }

    public async Task ValidateOptionArguments_UsesPartitionGrammar()
    {
        var provider = new MSTestPartitionCommandLineOptionsProvider(new MSTestExtension());
        CommandLineOption option = provider.GetCommandLineOptions().Single();
        foreach (string value in new[] { "1/1", "4/4", "1/2147483647", "2147483647/2147483647" })
        {
            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [value]);
            result.IsValid.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
        }

        foreach ((string id, string[] arguments) in new (string, string[])[]
        {
            ("missing", []),
            ("extra", ["1/2", "2/2"]),
            ("malformed", ["1//2"]),
            ("zero-index", ["0/2"]),
            ("zero-count", ["1/0"]),
            ("reversed", ["3/2"]),
            ("overflow", ["1/2147483648"]),
            ("signed", ["+1/2"]),
            ("whitespace", ["1/2 "]),
            ("non-ascii", ["\u0661/2"]),
            ("control", ["1/2\0"]),
        })
        {
            ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, arguments);
            result.IsValid.Should().BeFalse(id);
            result.ErrorMessage.Should().Be(PlatformAdapterResources.InvalidPartitionOptionArgument, id);
        }
    }

    public async Task ValidateCommandLineOptions_WithoutPartitionOrWithDotnetTestCli_IsValid()
    {
        var provider = new MSTestPartitionCommandLineOptionsProvider(new MSTestExtension());
        foreach ((bool partition, bool server, string[]? protocol) in new (bool, bool, string[]?)[]
        {
            (false, false, null),
            (false, true, []),
            (false, true, ["jsonrpc"]),
            (true, false, null),
            (true, true, ["dotnettestcli"]),
            (true, true, ["DotNetTestCli"]),
        })
        {
            ValidationResult result = await provider.ValidateCommandLineOptionsAsync(Options(partition, server, protocol));
            result.IsValid.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
        }
    }

    public async Task ValidateCommandLineOptions_PartitionWithJsonRpcServer_IsRejected()
    {
        var provider = new MSTestPartitionCommandLineOptionsProvider(new MSTestExtension());
        foreach (string[]? protocol in new string[]?[] { null, [], ["jsonrpc"], ["JsOnRpC"] })
        {
            ValidationResult result = await provider.ValidateCommandLineOptionsAsync(Options(true, true, protocol));
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be(PlatformAdapterResources.PartitionNotSupportedInServerMode);
        }
    }

    private static ICommandLineOptions Options(bool partition, bool server, string[]? protocol)
    {
        var options = new Mock<ICommandLineOptions>();
        options.Setup(value => value.IsOptionSet("partition")).Returns(partition);
        options.Setup(value => value.TryGetOptionArgumentList("server", out protocol)).Returns(server);
        return options.Object;
    }
}
