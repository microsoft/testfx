// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    /// <summary>
    /// Where during test lifecycle should ClassCleanup happen
    /// </summary>
    public enum ClassCleanupLifecycle
    {
        /// <summary>
        /// Run at end of assembly
        /// </summary>
        EndOfAssembly,

        /// <summary>
        /// Run at end of class
        /// </summary>
        EndOfClass,
    }
}