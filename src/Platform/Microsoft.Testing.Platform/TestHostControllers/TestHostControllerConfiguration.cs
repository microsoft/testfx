// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal record TestHostControllerConfiguration(ITestHostEnvironmentVariableProvider[] EnvironmentVariableProviders,
    ITestHostProcessLifetimeHandler[] LifetimeHandlers,
    bool RequireProcessRestart);
