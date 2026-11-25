// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

internal sealed class PartitioningExtension : IExtension
{
    public static PartitioningExtension Instance { get; } = new();

    private PartitioningExtension()
    {
    }

    public string Uid => "TestPartitioning";

    public string Version => PlatformVersion.Version;

    public string DisplayName => "Test partitioning";

    public string Description => PlatformResources.PartitionCountOptionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
