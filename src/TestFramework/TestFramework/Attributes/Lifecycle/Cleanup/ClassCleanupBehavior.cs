// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// When to run ClassCleanup during test execution.
/// </summary>
public enum ClassCleanupBehavior
{
    /// <summary>
    /// Run at end of class.
    /// </summary>
    EndOfClass,

    /// <summary>
    /// Run at end of assembly.
    /// </summary>
    EndOfAssembly,
}
