// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

/// <summary>
/// A testable implementation of reflect helper.
/// </summary>
internal sealed class TestableReflectHelper : ReflectHelper
{
    /// <summary>
    /// A dictionary to hold mock custom attributes. The int represents a hash code of
    /// the Type of custom attribute and the level its applied at :
    /// MemberTypes.All for assembly level
    /// MemberTypes.TypeInfo for class level
    /// MemberTypes.Method for method level.
    /// </summary>
    private readonly Dictionary<int, Attribute[]> _customAttributes;

    public TestableReflectHelper()
        : base(new TestableReflectionAccessor())
    {
        _customAttributes = [];
    }

    public void SetCustomAttribute(Type type, Attribute[] values, MemberTypes memberTypes)
    {
        // tODO:  Add the info to ours;
        var ours = (TestableReflectionAccessor)this.NotCachedReflectHelper;


        int hashCode = type.FullName.GetHashCode() + memberTypes.GetHashCode();
        _customAttributes[hashCode] = _customAttributes.TryGetValue(hashCode, out Attribute[] value)
            ? value.Concat(values).ToArray()
            : values;
    }
}

internal class TestableReflectionAccessor : INotCachedReflectHelper
{
    public TestableReflectionAccessor()
    {
    }

    // TODO: fix to fix tests.
    public object[] GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit) => throw new NotImplementedException();

    public TAttribute[] GetCustomAttributesNotCached<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit) => throw new NotImplementedException();

    public bool IsDerivedAttributeDefinedNotCached<TAttribute>(ICustomAttributeProvider attributeProvider, bool inherit) => throw new NotImplementedException();
}
