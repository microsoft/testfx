// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal sealed class AttributeComparer
{
    public static bool IsNonDerived<TAttribute>(Attribute attribute) =>
        attribute is TAttribute;

    public static bool IsDerived<TAttribute>(Attribute attribute)
    {
        Type attributeType = attribute.GetType();

        // IsSubclassOf returns false when the types are equal.
        if (attributeType == typeof(TAttribute))
        {
            return true;
        }

        // IsAssignableFrom also does this internally, but later falls to check generic
        // and we don't need that.
        if (!typeof(TAttribute).IsInterface)
        {
            // This returns false when TAttribute is interface (like ITestDataSource).
            return attributeType.IsSubclassOf(typeof(TAttribute));
        }

        return typeof(TAttribute).IsAssignableFrom(attributeType);
    }
}
