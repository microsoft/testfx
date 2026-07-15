// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Resource-independent base for the command-line provider that supports the VSTest <c>--filter</c> (test case filter)
/// option. It owns the option registration; the concrete providers only supply the localized option description.
/// Shared by the VSTest bridge and the MSTest adapter's native Microsoft.Testing.Platform integration so both surface
/// an identical <c>--filter</c> option.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The shared helper is linked into projects that are allowed to use MTP APIs")]
internal abstract class TestCaseFilterCommandLineOptionsProviderBase : CommandLineOptionsProviderBase
{
    public const string TestCaseFilterOptionName = "filter";

    protected TestCaseFilterCommandLineOptionsProviderBase(IExtension extension, string optionDescription)
        : base(extension, [new CommandLineOption(TestCaseFilterOptionName, optionDescription, ArgumentArity.ExactlyOne, false)])
    {
    }
}
#endif
