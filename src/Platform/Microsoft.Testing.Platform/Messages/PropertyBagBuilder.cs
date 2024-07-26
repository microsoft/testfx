// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public class PropertyBagBuilder
{
    public List<IProperty> Properties { get; init; } = [];

    public PropertyBagBuilder Add(IProperty property)
    {
        Properties.Add(property);
        return this;
    }

    public PropertyBag ToImmutablePropertyBag(TestNodeStateProperty currentState)
    {
        IEnumerable<IProperty> nonTestStateProperties = Properties.Where(x => x is not TestNodeStateProperty);

        return new PropertyBag(nonTestStateProperties, currentState);
    }
}
