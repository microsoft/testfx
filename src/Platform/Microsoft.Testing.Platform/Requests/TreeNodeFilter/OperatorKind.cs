// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

// Kinds are sorted in precedence order.
internal enum OperatorKind
{
    /// <summary>
    /// Not an operator.
    /// </summary>
    None,

    /// <summary>
    /// A separator operator, used to separate multiple filters.
    /// </summary>
    Separator,

    /// <summary>
    /// An opening brace operator.
    /// </summary>
    LeftBrace,

    /// <summary>
    /// A closing brace operator.
    /// </summary>
    RightBrace,

    /// <summary>
    /// Left hand side of a filter expression.
    /// </summary>
    LeftParameter,

    /// <summary>
    /// Right hand side of a filter expression.
    /// </summary>
    RightParameter,

    /// <summary>
    /// Filter equals operator.
    /// </summary>
    FilterEquals,

    /// <summary>
    /// Filter not equals operator.
    /// </summary>
    FilterNotEquals,

    /// <summary>
    /// Operator used for combining multiple filters with a logical OR.
    /// </summary>
    Or,

    /// <summary>
    /// Operator used for combining multiple filters with a logical AND.
    /// </summary>
    And,

    /// <summary>
    /// Operator used to negate an expression
    /// </summary>
    UnaryNot,
}
