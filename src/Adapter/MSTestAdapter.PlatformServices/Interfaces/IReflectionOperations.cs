// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// This service is responsible for platform specific reflection operations.
/// </summary>
internal interface IReflectionOperations
{
    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    object[]? GetCustomAttributes(MemberInfo memberInfo);

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    object[] GetCustomAttributes(Assembly assembly, Type type);

    ConstructorInfo[] GetDeclaredConstructors(Type classType);

    MethodInfo[] GetDeclaredMethods(Type classType);

    PropertyInfo[] GetDeclaredProperties(Type type);

    Type[] GetDefinedTypes(Assembly assembly);

    MethodInfo[] GetRuntimeMethods(Type type);

    MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters, bool includeNonPublic);

    PropertyInfo? GetRuntimeProperty(Type classType, string propertyName, bool includeNonPublic);

    Type? GetType(string typeName);

    Type? GetType(Assembly assembly, string typeName);

    object? CreateInstance(Type type, object?[] parameters);

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute, or an attribute that derives from it.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="memberInfo">Member to inspect for attributes.</param>
    /// <returns>True if the attribute of the specified type is defined on this member or a class.</returns>
    bool IsAttributeDefined<TAttribute>(MemberInfo memberInfo)
        where TAttribute : Attribute;

    /// <summary>
    /// Gets first attribute that matches the type.
    /// Use this together with attribute that does not allow multiple and is sealed. In such case there cannot be more attributes, and this will avoid the cost of
    /// checking for more than one attribute.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method.</param>
    /// <returns>The attribute that is found or null.</returns>
    TAttribute? GetFirstAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute;

    /// <summary>
    /// Gets first attribute that matches the type or is derived from it.
    /// Use this together with attribute that does not allow multiple. In such case there cannot be more attributes, and this will avoid the cost of
    /// checking for more than one attribute.
    /// </summary>
    /// <typeparam name="TAttribute">Type of the attribute to find.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method.</param>
    /// <returns>The attribute that is found or null.</returns>
    /// <exception cref="InvalidOperationException">Throws when multiple attributes are found (the attribute must allow multiple).</exception>
    TAttribute? GetSingleAttributeOrDefault<TAttribute>(ICustomAttributeProvider attributeProvider)
        where TAttribute : Attribute;

    /// <summary>
    /// Get attribute defined on a member which is of given type of subtype of given type.
    /// </summary>
    /// <typeparam name="TAttributeType">The attribute type.</typeparam>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <returns>An instance of the attribute.</returns>
    IEnumerable<TAttributeType> GetAttributes<TAttributeType>(ICustomAttributeProvider attributeProvider)
        where TAttributeType : Attribute;

    /// <summary>
    /// Gets and caches the attributes for the given type, or method.
    /// </summary>
    /// <param name="attributeProvider">The member to inspect.</param>
    /// <returns>Attributes defined.</returns>
    Attribute[] GetCustomAttributesCached(ICustomAttributeProvider attributeProvider);

    /// <summary>
    /// Returns true when the method is declared in the assembly where the type is declared.
    /// </summary>
    /// <param name="method">The method to check for.</param>
    /// <param name="type">The type declared in the assembly to check.</param>
    /// <returns>True if the method is declared in the assembly where the type is declared.</returns>
    bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type);

    /// <summary>
    /// Get categories applied to the test method.
    /// </summary>
    /// <param name="categoryAttributeProvider">The member to inspect.</param>
    /// <param name="owningType">The reflected type that owns <paramref name="categoryAttributeProvider"/>.</param>
    /// <returns>Categories defined.</returns>
    string[] GetTestCategories(MemberInfo categoryAttributeProvider, Type owningType);

    /// <summary>
    /// Get the parallelization behavior for a test method.
    /// </summary>
    /// <param name="testMethod">Test method.</param>
    /// <param name="owningType">The type that owns <paramref name="testMethod"/>.</param>
    /// <returns>True if test method should not run in parallel.</returns>
    bool IsDoNotParallelizeSet(MemberInfo testMethod, Type owningType);

    /// <summary>
    /// Priority if any set for test method. Will return priority if attribute is applied to TestMethod
    /// else null.
    /// </summary>
    /// <param name="priorityAttributeProvider">The member to inspect.</param>
    /// <returns>Priority value if defined. Null otherwise.</returns>
    int? GetPriority(MemberInfo priorityAttributeProvider);

    /// <summary>
    /// KeyValue pairs that are provided by TestPropertyAttributes of the given test method.
    /// </summary>
    /// <param name="testPropertyProvider">The member to inspect.</param>
    /// <returns>List of traits.</returns>
    Trait[] GetTestPropertiesAsTraits(MethodInfo testPropertyProvider);
}
