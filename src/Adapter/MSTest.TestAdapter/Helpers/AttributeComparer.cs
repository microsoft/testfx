// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal class AttributeComparer
{
    public static bool IsNonDerived<TAttribute>(Attribute attribute) =>
        attribute is TAttribute;

    public static bool IsDerived<TAttribute>(Attribute attribute)
    {
        Type attributeType = attribute.GetType();
        // IsSubclassOf returns false when the types are equal.
        return attributeType == typeof(TAttribute)
            || attributeType.IsSubclassOf(typeof(TAttribute));
    }
}
