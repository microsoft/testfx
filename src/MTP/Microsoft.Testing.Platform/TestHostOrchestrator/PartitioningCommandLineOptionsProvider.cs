// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

internal sealed class PartitioningCommandLineOptionsProvider()
    : CommandLineOptionsProviderBase(
        PartitioningExtension.Instance,
        [new CommandLineOption(PartitionCountOptionName, PlatformResources.PartitionCountOptionDescription, ArgumentArity.ExactlyOne, isHidden: false)])
{
    internal const string PartitionCountOptionName = "partition-count";
    internal const string PartitionOptionName = "partition";

    private static bool IsProcessTreeTerminationSupported
#if NETCOREAPP
        => true;
#else
        => false;
#endif

    // The platform registers the option for every application, but only exposes it when the test framework
    // declares that each child test host can run a single partition.
    internal static bool IsSupportedBy(ITestFrameworkCapabilities? capabilities)
        => capabilities?.GetCapability<ITestPartitioningCapability>()?.IsSupported == true;

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name != PartitionCountOptionName
            || (arguments is [string value]
                && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0)
                    ? ValidationResult.ValidTask
                    : ValidationResult.InvalidTask(PlatformResources.InvalidPartitionCountOptionArgument);

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => !commandLineOptions.IsOptionSet(PartitionCountOptionName)
            || IsPartitionedChildInvocation(commandLineOptions)
            ? ValidationResult.ValidTask
            : commandLineOptions.IsOptionSet(PartitionOptionName)
            ? ValidationResult.InvalidTask(PlatformResources.PartitionCountAndPartitionAreExclusive)
            : !IsProcessTreeTerminationSupported
            || OperatingSystem.IsBrowser() || OperatingSystem.IsWasi() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS()
            || (commandLineOptions.TryGetOptionArgumentList("server", out string[]? serverArguments)
                && !(serverArguments is [string protocol] && protocol.Equals("dotnettestcli", StringComparison.OrdinalIgnoreCase)))
            ? ValidationResult.InvalidTask(PlatformResources.PartitionCountUnsupportedMode)
            : ValidationResult.ValidTask;

    // A child receives --partition on the CLI but may inherit --partition-count from testconfig.json
    // or another configuration source. Only that explicit child selector may override the setting.
    private static bool IsPartitionedChildInvocation(ICommandLineOptions commandLineOptions)
        => commandLineOptions is CommandLineHandler commandLineHandler
            && !commandLineHandler.ParseResult.IsOptionSet(PartitionCountOptionName)
            && commandLineHandler.ParseResult.IsOptionSet(PartitionOptionName);
}
