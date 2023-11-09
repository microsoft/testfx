// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class TreeNodeFilterCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    public const string TreenodeFilter = "treenode-filter";

    public TreeNodeFilterCommandLineOptionsProvider(IExtension extension)
    {
        Uid = extension.Uid;
        DisplayName = extension.DisplayName;
        Description = extension.Description;
    }

    /// <inheritdoc />
    public string Uid { get; }

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public CommandLineOption[] GetCommandLineOptions()
        => new CommandLineOption[]
        {
            new(TreenodeFilter, "Filter the tests treenodes to be executed.", ArgumentArity.ZeroOrOne, false),
        };

    public bool OptionArgumentsAreValid(CommandLineOption option, string[] arguments, out string error)
    {
        error = string.Empty;

        if (option.Name == TreenodeFilter && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{TreenodeFilter}, expression expected e.g. /MyAssembly/MyNamespace/MyClass/MyTestMethod*[OS=Linux]";
            return false;
        }

        return true;
    }

    public bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }
}
