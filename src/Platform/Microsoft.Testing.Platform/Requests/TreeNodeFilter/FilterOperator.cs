// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

internal enum FilterOperator
{
    /// <summary>
    /// Negate the following expression.
    /// </summary>
    Not,

    /// <summary>
    /// Combine the following expressions with a logical AND.
    /// </summary>
    And,

    /// <summary>
    /// Combine the following expressions with a logical OR.
    /// </summary>
    Or,
}
