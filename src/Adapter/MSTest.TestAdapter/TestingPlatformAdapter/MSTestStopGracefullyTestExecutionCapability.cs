// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal sealed class MSTestStopGracefullyTestExecutionCapability : IStopGracefullyTestExecutionCapability
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    // TODO:
    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
