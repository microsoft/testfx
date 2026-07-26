// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies how a test accesses a resource declared with <see cref="ResourceLockAttribute"/>.
/// </summary>
/// <remarks>
/// <see cref="ReadWrite"/> is deliberately the zero value, so that a default-initialized
/// <see cref="ResourceAccessMode"/> is the exclusive (safe) mode. Under-locking produces flaky races,
/// while over-locking merely runs slower, so any unspecified value must fail closed.
/// </remarks>
public enum ResourceAccessMode
{
    /// <summary>
    /// The test reads and writes the resource. The lock is exclusive: it blocks against all other
    /// readers and writers of the same resource. This is the default.
    /// </summary>
    ReadWrite = 0,

    /// <summary>
    /// The test only reads the resource. Read locks run concurrently with each other and block only
    /// against a <see cref="ReadWrite"/> holder.
    /// </summary>
    Read = 1,
}
