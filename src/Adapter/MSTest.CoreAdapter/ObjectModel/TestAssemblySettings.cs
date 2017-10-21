﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [Serializable]
    internal class TestAssemblySettings
    {
        /// <summary>
        /// Gets or sets the parallelization level.
        /// </summary>
        internal int ParallelLevel { get; set; }

        /// <summary>
        /// Gets or sets the mode of parallelization.
        /// </summary>
        internal TestParallelizationMode ParallelMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the assembly can be parallelized.
        /// </summary>
        internal bool CanParallelizeAssembly { get; set; }
    }
}
