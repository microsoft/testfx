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
    {
        _customAttributes = [];
    }

    public void SetCustomAttribute(Type type, Attribute[] values, MemberTypes memberTypes)
    {
        int hashCode = type.FullName.GetHashCode() + memberTypes.GetHashCode();
        _customAttributes[hashCode] = _customAttributes.TryGetValue(hashCode, out Attribute[] value)
            ? value.Concat(values).ToArray()
            : values;
    }

    internal override TAttribute[] GetCustomAttributeForAssembly<TAttribute>(MemberInfo memberInfo)
    {
        int hashCode = MemberTypes.All.GetHashCode() + typeof(TAttribute).FullName.GetHashCode();

        return _customAttributes.TryGetValue(hashCode, out Attribute[] value)
            ? value.OfType<TAttribute>().ToArray()
            : [];
    }

    internal override TAttribute[] GetCustomAttributes<TAttribute>(MemberInfo memberInfo)
    {
        int hashCode = memberInfo.MemberType.GetHashCode() + typeof(TAttribute).FullName.GetHashCode();

        return _customAttributes.TryGetValue(hashCode, out Attribute[] value)
            ? value.OfType<TAttribute>().ToArray()
            : [];
    }
}
