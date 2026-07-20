// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

internal sealed class ArtifactPostProcessingManager : IArtifactPostProcessingManager
{
    private readonly List<Func<IServiceProvider, IArtifactPostProcessor>> _factories = [];

    public void AddArtifactPostProcessor(Func<IServiceProvider, IArtifactPostProcessor> factory)
        => _factories.Add(factory ?? throw new ArgumentNullException(nameof(factory)));

    public async Task<IReadOnlyList<IArtifactPostProcessor>> BuildAsync(IServiceProvider serviceProvider)
    {
        List<IArtifactPostProcessor> processors = [];
        for (int i = 0; i < _factories.Count; i++)
        {
            IArtifactPostProcessor processor = _factories[i](serviceProvider);
            if (!await processor.IsEnabledAsync().ConfigureAwait(false))
            {
                continue;
            }

            processors.ValidateUniqueExtension(processor);
            await processor.TryInitializeAsync().ConfigureAwait(false);
            processors.Add(processor);
        }

        return processors;
    }
}
