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
    public TestableReflectHelper()
        : base(new TestableReflectionAccessor())
    {
    }

    public void SetCustomAttribute(Type type, Attribute[] values, MemberTypes memberTypes)
    {
        var attributeProvider = (TestableReflectionAccessor)NotCachedAttributes;
        attributeProvider.AddData(type, values, memberTypes);
    }
}

internal class TestableReflectionAccessor : INotCachedAttributeHelper
{
    /// <summary>
    /// A collection to hold mock custom attributes.
    /// MemberTypes.All for assembly level
    /// MemberTypes.TypeInfo for class level
    /// MemberTypes.Method for method level.
    /// </summary>
    private readonly List<(Type Type, Attribute Attribute, MemberTypes MemberType)> _data = new();

    public object[] GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit)
    {
        var foundAttributes = new List<Attribute>();
        foreach ((Type Type, Attribute Attribute, MemberTypes MemberType) attributeData in _data)
        {
            if (attributeProvider is MethodInfo && (attributeData.MemberType == MemberTypes.Method))
            {
                foundAttributes.Add(attributeData.Attribute);
            }
            else if (attributeProvider is TypeInfo && (attributeData.MemberType == MemberTypes.TypeInfo))
            {
                foundAttributes.Add(attributeData.Attribute);
            }
            else if (attributeProvider is Assembly && attributeData.MemberType == MemberTypes.All)
            {
                foundAttributes.Add(attributeData.Attribute);
            }
        }

        return foundAttributes.ToArray();
    }

    internal void AddData(Type type, Attribute[] values, MemberTypes memberTypes)
    {
        foreach (Attribute attribute in values)
        {
            _data.Add((type, attribute, memberTypes));
        }
    }
}
