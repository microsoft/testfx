// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

internal sealed class PropertyExpression(ValueExpression propertyName, ValueExpression value) : FilterExpression
{
    public ValueExpression PropertyName { get; } = propertyName;

    public ValueExpression Value { get; } = value;
}
