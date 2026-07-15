// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native command-line provider for the VSTest <c>--filter</c> (test case filter) option. Mirrors the VSTest
/// bridge's <c>TestCaseFilterCommandLineOptionsProvider</c> (identical option name and description).
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestTestCaseFilterCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string TestCaseFilterOptionName = "filter";

    public MSTestTestCaseFilterCommandLineOptionsProvider(IExtension extension)
        : base(extension, [new CommandLineOption(TestCaseFilterOptionName, PlatformAdapterResources.TestCaseFilterOptionDescription, ArgumentArity.ExactlyOne, false)])
    {
    }
}
#endif
