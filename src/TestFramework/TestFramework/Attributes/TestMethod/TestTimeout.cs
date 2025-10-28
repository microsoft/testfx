// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Enumeration for timeouts, that can be used with the <see cref="TimeoutAttribute"/> class.
/// The type of the enumeration must match.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "Compat reasons")]
public enum TestTimeout
{
    /// <summary>
    /// The infinite.
    /// </summary>
    Infinite = int.MaxValue,
}
