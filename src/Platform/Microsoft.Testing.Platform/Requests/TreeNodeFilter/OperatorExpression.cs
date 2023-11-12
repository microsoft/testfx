// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

// Allows to express an expression A & B or A | B
internal sealed class OperatorExpression(FilterOperator op, IReadOnlyCollection<FilterExpression> subExpressions) : FilterExpression
{
    public FilterOperator Op { get; } = op;

    public IReadOnlyCollection<FilterExpression> SubExpressions { get; } = subExpressions;
}
