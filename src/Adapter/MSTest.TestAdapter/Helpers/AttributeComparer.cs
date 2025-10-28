// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class AttributeComparer
{
    public static bool IsNonDerived<TAttribute>(Attribute attribute) =>
        // We are explicitly checking the given type *exactly*. We don't want to consider inheritance.
        // So, this should NOT be refactored to 'attribute is TAttribute'.
        attribute.GetType() == typeof(TAttribute);

    public static bool IsDerived<TAttribute>(Attribute attribute) => attribute is TAttribute;
}
