// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class TestHostControllerConfiguration(ITestHostEnvironmentVariableProvider[] environmentVariableProviders,
    ITestHostProcessLifetimeHandler[] lifetimeHandlers,
    IDataConsumer[] dataConsumer,
    bool requireProcessRestart)
{
    public ITestHostEnvironmentVariableProvider[] EnvironmentVariableProviders { get; } = environmentVariableProviders;

    public ITestHostProcessLifetimeHandler[] LifetimeHandlers { get; } = lifetimeHandlers;

    public IDataConsumer[] DataConsumer { get; } = dataConsumer;

    public bool RequireProcessRestart { get; } = requireProcessRestart;
}
