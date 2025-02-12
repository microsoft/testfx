// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Framework;

public interface ITestNodesBuilder : ITestFrameworkCapabilities
{
    Task<TestNode[]> BuildAsync(ITestSessionContext testSessionContext);
}
