// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

// Allows to express an expression A & B or A | B
internal sealed class OperatorExpression(FilterOperator op, FilterExpression[] subExpressions) : FilterExpression
{
    // Stored as an array internally to allow zero-allocation indexed iteration in the
    // matching hot paths, but exposed as IReadOnlyList<FilterExpression> so callers
    // cannot mutate the expression tree after construction.
    private readonly FilterExpression[] _subExpressions = subExpressions;

    public FilterOperator Op { get; } = op;

    public IReadOnlyList<FilterExpression> SubExpressions => _subExpressions;
}
