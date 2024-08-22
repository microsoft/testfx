// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.CommandLine;

internal class CommandLineOptionsProxy : ICommandLineOptions
{
    private ICommandLineOptions? _commandLineOptions;

    public bool IsOptionSet(string optionName)
        => _commandLineOptions?.IsOptionSet(optionName) ?? throw new InvalidOperationException("The ICommandLineOptions has not been built yet or is no more usable at this stage.");

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
        => _commandLineOptions?.TryGetOptionArgumentList(optionName, out arguments) ?? throw new InvalidOperationException("The ICommandLineOptions has not been built yet or is no more usable at this stage.");

    public void SetCommandLineOptions(ICommandLineOptions commandLineOptions)
    {
        Guard.NotNull(commandLineOptions);
        _commandLineOptions = commandLineOptions;
    }
}
