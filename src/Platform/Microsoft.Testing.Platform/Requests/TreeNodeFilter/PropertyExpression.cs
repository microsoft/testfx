// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

internal sealed class PropertyExpression : FilterExpression
{
    public ValueExpression PropertyName { get; }

    public ValueExpression Value { get; }

    public PropertyExpression(ValueExpression propertyName, ValueExpression value)
    {
        PropertyName = propertyName;
        Value = value;
    }
}
