// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Extensions.VSTestBridge.CommandLine;

internal sealed class TestRunParametersCommandLineOptionsProvider : TestRunParametersCommandLineOptionsProviderBase
{
    public TestRunParametersCommandLineOptionsProvider(IExtension extension)
        : base(extension, ExtensionResources.TestRunParameterOptionDescription, ExtensionResources.TestRunParameterOptionArgumentIsNotParameter)
    {
    }
}
