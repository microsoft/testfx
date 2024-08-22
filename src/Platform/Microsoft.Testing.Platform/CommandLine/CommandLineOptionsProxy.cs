// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineOptionsProxy : ICommandLineOptions
{
    private ICommandLineOptions? _commandLineOptions;

    public bool IsOptionSet(string optionName)
        => _commandLineOptions is null
            ? throw new InvalidOperationException(Resources.PlatformResources.CommandLineOptionsNotReady)
            : _commandLineOptions.IsOptionSet(optionName);

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
        => _commandLineOptions is null
            ? throw new InvalidOperationException(Resources.PlatformResources.CommandLineOptionsNotReady)
            : _commandLineOptions.TryGetOptionArgumentList(optionName, out arguments);

    public void SetCommandLineOptions(ICommandLineOptions commandLineOptions)
    {
        Guard.NotNull(commandLineOptions);
        _commandLineOptions = commandLineOptions;
    }
}
