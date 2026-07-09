// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.VSTestBridge.CommandLine;

internal sealed class TestRunParametersCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string TestRunParameterOptionName = "test-parameter";

    public TestRunParametersCommandLineOptionsProvider(IExtension extension)
        : base(
            extension,
            [
                new CommandLineOption(TestRunParameterOptionName, ExtensionResources.TestRunParameterOptionDescription, ArgumentArity.OneOrMore, false)
            ])
    {
    }

    /// <inheritdoc />
    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        string? invalidArgument = RunSettingsProviderHelper.FindInvalidTestParameter(arguments);
        return invalidArgument is not null
            ? ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.TestRunParameterOptionArgumentIsNotParameter, invalidArgument))
            : ValidationResult.ValidTask;
    }
}
