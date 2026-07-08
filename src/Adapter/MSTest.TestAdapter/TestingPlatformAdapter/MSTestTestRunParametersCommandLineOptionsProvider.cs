// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native command-line provider for the VSTest <c>--test-parameter</c> (TestRunParameters) option. Mirrors
/// the VSTest bridge's <c>TestRunParametersCommandLineOptionsProvider</c> (identical option name, description and
/// validation).
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestTestRunParametersCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string TestRunParameterOptionName = "test-parameter";

    public MSTestTestRunParametersCommandLineOptionsProvider(IExtension extension)
        : base(extension, [new CommandLineOption(TestRunParameterOptionName, PlatformAdapterResources.TestRunParameterOptionDescription, ArgumentArity.OneOrMore, false)])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        foreach (string argument in arguments)
        {
            if (!argument.Contains('='))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, PlatformAdapterResources.TestRunParameterOptionArgumentIsNotParameter, argument));
            }
        }

        return ValidationResult.ValidTask;
    }
}
#endif
