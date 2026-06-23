// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    /// Gets a delegate that invokes <paramref name="method"/> directly (without
    /// <c>MethodInfo.Invoke</c>), or <see langword="null"/> when no source-generated invoker is
    /// available and the caller must fall back to reflection.
    /// The delegate returns the method's raw result: a <see cref="Task"/>/<c>ValueTask</c> for
    /// async methods, the return value for sync methods, or <see langword="null"/> for <c>void</c>.
    /// </summary>
    /// <param name="method">The test or fixture method to invoke.</param>
    /// <returns>An invoker delegate, or <see langword="null"/> in reflection mode.</returns>
    Func<object?, object?[]?, object?>? GetTestMethodInvoker(MethodInfo method);

    /// <summary>
    /// Gets a delegate that constructs an instance of <paramref name="type"/> directly (without
    /// <see cref="Activator.CreateInstance(Type, object[])"/> or <c>ConstructorInfo.Invoke</c>),
    /// or <see langword="null"/> when no source-generated invoker is available.
    /// </summary>
    /// <param name="type">The test class type to instantiate.</param>
    /// <returns>A constructor invoker delegate, or <see langword="null"/> in reflection mode.</returns>
    Func<object?[]?, object>? GetConstructorInvoker(Type type);

    /// <summary>
    /// Gets a delegate that assigns <paramref name="property"/> directly (without
    /// <see cref="PropertyInfo.SetValue(object, object)"/>), or <see langword="null"/> when no
    /// source-generated setter is available.
    /// </summary>
    /// <param name="property">The property to set (today: the <c>TestContext</c> property).</param>
    /// <returns>A setter delegate, or <see langword="null"/> in reflection mode.</returns>
    Action<object?, object?>? GetPropertySetter(PropertyInfo property);

    /// <summary>
    /// Checks to see if a member or type is decorated with the given attribute, or an attribute that derives from it.
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to search for.</typeparam>
    /// <param name="attributeProvider">The type, assembly or method to inspect for attributes.</param>
    /// <returns>True if the attribute of the specified type is defined.</returns>
    bool IsAttributeDefined<TAttribute>(ICustomAttributeProvider attributeProvider)
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
    /// Checks whether the declaring type of the method is declared in the same assembly as the given type.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <param name="type">The type whose assembly to compare against.</param>
    /// <returns>True if the method's declaring type is in the same assembly as <paramref name="type"/>.</returns>
    bool IsMethodDeclaredInSameAssemblyAsType(MethodInfo method, Type type);
}
