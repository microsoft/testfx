// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// This represents an expression that combines a value and a property expression,
/// for instance <c>MethodName[Trait=Foo]</c>.
/// </summary>
[DebuggerDisplay("{Value}[{Properties}]")]
internal sealed class ValueAndPropertyExpression : FilterExpression
{
    public FilterExpression Value { get; }

    public FilterExpression Properties { get; }

    public ValueAndPropertyExpression(FilterExpression value, FilterExpression properties)
    {
        Value = value;
        Properties = properties;
    }
}
