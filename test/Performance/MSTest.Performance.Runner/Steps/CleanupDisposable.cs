// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Performance.Runner.Steps;

internal class CleanupDisposable : IStep<Files, NoInputOutput>
{
    public string Description => "Cleanup disposable";

    public Task<NoInputOutput> ExecuteAsync(Files files, IContext context)
    {
        ((IDisposable)context).Dispose();
        return Task.FromResult(NoInputOutput.NoInputOutputInstance);
    }
}
