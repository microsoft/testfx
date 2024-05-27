// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Contoso.EnsureTestFramework;

internal sealed class EnsureTestFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => Array.Empty<ITestFrameworkCapability>();
}
