// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native command-line provider for the VSTest <c>--test-parameter</c> (TestRunParameters) option. Mirrors
/// the VSTest bridge's <c>TestRunParametersCommandLineOptionsProvider</c> (identical option name, description and
/// validation).
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestTestRunParametersCommandLineOptionsProvider : TestRunParametersCommandLineOptionsProviderBase
{
    public MSTestTestRunParametersCommandLineOptionsProvider(IExtension extension)
        : base(extension, PlatformAdapterResources.TestRunParameterOptionDescription, PlatformAdapterResources.TestRunParameterOptionArgumentIsNotParameter)
    {
    }
}
#endif
