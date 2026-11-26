// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

internal sealed class ShardingExtension : IExtension
{
    public static ShardingExtension Instance { get; } = new();

    private ShardingExtension()
    {
    }

    public string Uid => "TestSharding";

    public string Version => PlatformVersion.Version;

    public string DisplayName => "Test sharding";

    public string Description => PlatformResources.ShardCountOptionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
