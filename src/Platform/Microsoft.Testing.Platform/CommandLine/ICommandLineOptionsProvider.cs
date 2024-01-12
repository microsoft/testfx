// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.Extensions.CommandLine;

public interface ICommandLineOptionsProvider : IExtension
{
    IReadOnlyCollection<CommandLineOption> GetCommandLineOptions();

    bool OptionArgumentsAreValid(CommandLineOption commandOption, string[] arguments, out string? errorMessage);

    bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage);
}
