// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace Microsoft.Testing.Internal.Framework;

internal interface ITestFrameworkManager
{
    Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> TestFrameworkFactory { get; }

    Func<IServiceProvider, ITestFrameworkCapabilities> TestFrameworkCapabilitiesFactory { get; }
}
