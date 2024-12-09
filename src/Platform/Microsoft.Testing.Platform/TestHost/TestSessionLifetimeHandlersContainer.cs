// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHost;

internal sealed class TestSessionLifetimeHandlersContainer
{
    public TestSessionLifetimeHandlersContainer(IEnumerable<ITestSessionLifetimeHandler> testSessionLifetimeHandlers) => TestSessionLifetimeHandlers = testSessionLifetimeHandlers;

    public IEnumerable<ITestSessionLifetimeHandler> TestSessionLifetimeHandlers { get; }
}
