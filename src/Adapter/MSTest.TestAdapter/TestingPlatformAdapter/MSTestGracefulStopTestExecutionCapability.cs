// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Capabilities.TestFramework;

using MSTest.PlatformServices;

namespace MSTest.TestAdapter;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestGracefulStopTestExecutionCapability : IGracefulStopTestExecutionCapability
{
    private MSTestGracefulStopTestExecutionCapability()
    {
    }

    public static MSTestGracefulStopTestExecutionCapability Instance { get; } = new();

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
    {
        PlatformServiceProvider.Instance.IsGracefulStopRequested = true;
        return Task.CompletedTask;
    }
}
#endif
