// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Extensions.PackagedApp;

// Only the sidecar registers this provider; the selected MSTest host owns filter evaluation.
internal sealed class PackagedAppControllerFilterCommandLineOptionsProvider : TestCaseFilterCommandLineOptionsProviderBase
{
    public PackagedAppControllerFilterCommandLineOptionsProvider(IExtension extension)
        : base(extension, ExtensionResources.TestCaseFilterOptionDescription)
    {
    }
}
