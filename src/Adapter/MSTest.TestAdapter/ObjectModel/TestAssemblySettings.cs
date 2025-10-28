// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

[Serializable]
internal sealed class TestAssemblySettings
{
    public TestAssemblySettings() => Workers = -1;

    /// <summary>
    /// Gets or sets the parallelization level.
    /// </summary>
    internal int Workers { get; set; }

    /// <summary>
    /// Gets or sets the mode of parallelization.
    /// </summary>
    internal ExecutionScope Scope { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the assembly can be parallelized.
    /// </summary>
    internal bool CanParallelizeAssembly { get; set; }

    /// <summary>
    /// Gets or sets the class cleanup lifecycle timing.
    /// </summary>
    internal ClassCleanupBehavior ClassCleanupLifecycle { get; set; }
}
