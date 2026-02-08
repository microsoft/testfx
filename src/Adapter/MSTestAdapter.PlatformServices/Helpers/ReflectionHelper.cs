// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

/// <summary>
/// Helper methods for extracting test metadata from reflection operations.
/// </summary>
internal static class ReflectionHelper
{
    /// <summary>
    /// Get categories applied to the test method.
    /// </summary>
    /// <param name="reflectionOperations">The reflection operations to use.</param>
    /// <param name="categoryAttributeProvider">The member to inspect.</param>
    /// <param name="owningType">The reflected type that owns <paramref name="categoryAttributeProvider"/>.</param>
    /// <returns>Categories defined.</returns>
    public static string[] GetTestCategories(this IReflectionOperations reflectionOperations, MemberInfo categoryAttributeProvider, Type owningType)
    {
        IEnumerable<TestCategoryBaseAttribute> methodCategories = reflectionOperations.GetAttributes<TestCategoryBaseAttribute>(categoryAttributeProvider);
        IEnumerable<TestCategoryBaseAttribute> typeCategories = reflectionOperations.GetAttributes<TestCategoryBaseAttribute>(owningType);
        IEnumerable<TestCategoryBaseAttribute> assemblyCategories = reflectionOperations.GetAttributes<TestCategoryBaseAttribute>(owningType.Assembly);

        return [.. methodCategories.Concat(typeCategories).Concat(assemblyCategories).SelectMany(c => c.TestCategories)];
    }

    /// <summary>
    /// KeyValue pairs that are provided by TestPropertyAttributes of the given test method.
    /// </summary>
    /// <param name="reflectionOperations">The reflection operations to use.</param>
    /// <param name="testPropertyProvider">The member to inspect.</param>
    /// <returns>List of traits.</returns>
    public static Trait[] GetTestPropertiesAsTraits(this IReflectionOperations reflectionOperations, MethodInfo testPropertyProvider)
    {
        Attribute[] attributesFromMethod = reflectionOperations.GetCustomAttributesCached(testPropertyProvider);
        Attribute[] attributesFromClass = testPropertyProvider.ReflectedType is { } testClass ? reflectionOperations.GetCustomAttributesCached(testClass) : [];
        int countTestPropertyAttribute = 0;
        foreach (Attribute attribute in attributesFromMethod)
        {
            if (attribute is TestPropertyAttribute)
            {
                countTestPropertyAttribute++;
            }
        }

        foreach (Attribute attribute in attributesFromClass)
        {
            if (attribute is TestPropertyAttribute)
            {
                countTestPropertyAttribute++;
            }
        }

        if (countTestPropertyAttribute == 0)
        {
            // This is the common case that we optimize for. This method used to be an iterator (uses yield return) which is allocating unnecessarily in common cases.
            return [];
        }

        var traits = new Trait[countTestPropertyAttribute];
        int index = 0;
        foreach (Attribute attribute in attributesFromMethod)
        {
            if (attribute is TestPropertyAttribute testProperty)
            {
                traits[index++] = new Trait(testProperty.Name, testProperty.Value);
            }
        }

        foreach (Attribute attribute in attributesFromClass)
        {
            if (attribute is TestPropertyAttribute testProperty)
            {
                traits[index++] = new Trait(testProperty.Name, testProperty.Value);
            }
        }

        return traits;
    }
}
