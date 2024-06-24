// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.VSTestBridge.CommandLine;

/// <summary>
/// A command line service provider bringing support for the VSTest test case.
/// </summary>
internal sealed class TestCaseFilterCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    public const string TestCaseFilterOptionName = "filter";

    public TestCaseFilterCommandLineOptionsProvider(IExtension extension)
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
    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new(TestCaseFilterOptionName, ExtensionResources.TestCaseFilterOptionDescription, ArgumentArity.ExactlyOne, false)
    ];

    /// <inheritdoc />
    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;
}
