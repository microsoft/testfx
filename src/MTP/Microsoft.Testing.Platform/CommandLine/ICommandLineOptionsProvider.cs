// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.Extensions.CommandLine;

/// <summary>
/// Represents an interface for providing command line options.
/// </summary>
public interface ICommandLineOptionsProvider : IExtension
{
    /// <summary>
    /// Gets the command line options provided by this extension.
    /// </summary>
    /// <returns>The collection of command line options.</returns>
    IReadOnlyCollection<CommandLineOption> GetCommandLineOptions();

    /// <summary>
    /// Validate the arguments for the given command option.
    /// </summary>
    /// <param name="commandOption">The command option.</param>
    /// <param name="arguments">The arguments provided for the command option.</param>
    /// <returns>The result of the validation.</returns>
    Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments);

    /// <summary>
    /// Validate that the command line options are valid in the context of each other.
    /// </summary>
    /// <param name="commandLineOptions">All command line options (including the ones provided by other extensions) are provided.</param>
    /// <returns>The result of the validation.</returns>
    Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions);
}
