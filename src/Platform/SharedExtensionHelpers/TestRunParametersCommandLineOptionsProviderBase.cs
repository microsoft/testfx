// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Resource-independent base for the command-line provider that supports the VSTest <c>--test-parameter</c>
/// (TestRunParameters) option. It owns the option registration and the <c>name=value</c> argument validation; the
/// concrete providers only supply the localized option description and error message. Shared by the VSTest bridge and
/// the MSTest adapter's native Microsoft.Testing.Platform integration so both surface an identical
/// <c>--test-parameter</c> option.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The shared helper is linked into projects that are allowed to use MTP APIs")]
internal abstract class TestRunParametersCommandLineOptionsProviderBase : CommandLineOptionsProviderBase
{
    public const string TestRunParameterOptionName = "test-parameter";

    private readonly string _argumentIsNotParameterErrorFormat;

    protected TestRunParametersCommandLineOptionsProviderBase(
        IExtension extension,
        string optionDescription,
        string argumentIsNotParameterErrorFormat)
        : base(extension, [new CommandLineOption(TestRunParameterOptionName, optionDescription, ArgumentArity.OneOrMore, false)])
        => _argumentIsNotParameterErrorFormat = argumentIsNotParameterErrorFormat;

    /// <inheritdoc />
    public sealed override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        string? invalidArgument = RunSettingsProviderHelper.FindInvalidTestParameter(arguments);
        return invalidArgument is not null
            ? ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, _argumentIsNotParameterErrorFormat, invalidArgument))
            : ValidationResult.ValidTask;
    }
}
#endif
