// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.VSTestBridge.CommandLine;

internal sealed class TestRunParametersCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    public const string TestRunParameterOptionName = "test-parameter";

    public TestRunParametersCommandLineOptionsProvider(IExtension extension)
    {
        Uid = extension.Uid;
        DisplayName = extension.DisplayName;
        Description = extension.Description;
        Version = extension.Version;
    }

    /// <inheritdoc />
    public string Uid { get; }

    /// <inheritdoc />
    public string Version { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => new[] { new CommandLineOption(TestRunParameterOptionName, ExtensionResources.TestRunParameterOptionDescription, ArgumentArity.OneOrMore, false) };

    /// <inheritdoc />
    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;

    /// <inheritdoc />
    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        foreach (string argument in arguments)
        {
            if (!argument.Contains('='))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.TestRunParameterOptionArgumentIsNotParameter, argument));
            }
        }

        return ValidationResult.ValidTask;
    }
}
