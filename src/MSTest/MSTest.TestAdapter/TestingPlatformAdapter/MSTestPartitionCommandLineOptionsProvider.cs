// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestPartitionCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string PartitionOptionName = "partition";

    public MSTestPartitionCommandLineOptionsProvider(IExtension extension)
        : base(extension, [new CommandLineOption(PartitionOptionName, PlatformAdapterResources.PartitionOptionDescription, ArgumentArity.ExactlyOne, isHidden: false)])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name != PartitionOptionName
            || (arguments is [string value] && TestPartition.TryParse(value, out _))
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(PlatformAdapterResources.InvalidPartitionOptionArgument);

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => !commandLineOptions.IsOptionSet(PartitionOptionName)
            ? ValidationResult.ValidTask
            : OperatingSystem.IsBrowser() || OperatingSystem.IsWasi()
                ? ValidationResult.InvalidTask(PlatformAdapterResources.PartitionNotSupportedOnWebAssembly)
                : commandLineOptions.TryGetOptionArgumentList("server", out string[]? serverArguments)
                    && !(serverArguments is [string protocol] && protocol.Equals("dotnettestcli", StringComparison.OrdinalIgnoreCase))
                        ? ValidationResult.InvalidTask(PlatformAdapterResources.PartitionNotSupportedInServerMode)
                        : ValidationResult.ValidTask;
}
