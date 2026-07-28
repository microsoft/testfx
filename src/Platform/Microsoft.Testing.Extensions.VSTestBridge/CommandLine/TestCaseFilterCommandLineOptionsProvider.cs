// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Extensions.VSTestBridge.CommandLine;

/// <summary>
/// A command line service provider bringing support for the VSTest test case.
/// </summary>
internal sealed class TestCaseFilterCommandLineOptionsProvider : TestCaseFilterCommandLineOptionsProviderBase
{
    public TestCaseFilterCommandLineOptionsProvider(IExtension extension)
        : base(extension, ExtensionResources.TestCaseFilterOptionDescription)
    {
    }
}
