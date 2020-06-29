// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [Serializable]
    internal class TestAssemblySettings
    {
        public TestAssemblySettings()
        {
            this.Workers = -1;
        }

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
        internal ClassCleanupLifecycle ClassCleanupLifecycle { get; set; }
    }
}
