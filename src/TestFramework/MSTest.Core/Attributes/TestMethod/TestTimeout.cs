// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System.Diagnostics.CodeAnalysis;
#pragma warning disable SA1402 // FileMayOnlyContainASingleType
#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// Enumeration for timeouts, that can be used with the <see cref="TimeoutAttribute"/> class.
    /// The type of the enumeration must match
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "Compat reasons")]
    public enum TestTimeout
    {
        /// <summary>
        /// The infinite.
        /// </summary>
        Infinite = int.MaxValue
    }
}
