// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// This service is responsible for platform specific reflection operations.
/// </summary>
internal sealed class ReflectionOperations : IReflectionOperations
{
    private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const BindingFlags Everything = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo)
#if NETFRAMEWORK
         => [.. ReflectionUtility.GetCustomAttributes(memberInfo)];
#else
    {
        object[] attributes = memberInfo.GetCustomAttributes(typeof(Attribute), inherit: true);

        // Ensures that when the return of this method is used here:
        // https://github.com/microsoft/testfx/blob/e101a9d48773cc935c7b536d25d378d9a3211fee/src/Adapter/MSTest.TestAdapter/Helpers/ReflectHelper.cs#L461
        // then we are already Attribute[] to avoid LINQ Cast and extra array allocation.
        // This assert is solely for performance. Nothing "functional" will go wrong if the assert failed.
        Debug.Assert(attributes is Attribute[], $"Expected Attribute[], found '{attributes.GetType()}'.");
        return attributes;
    }
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member info. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, Type type) =>
#if NETFRAMEWORK
        [.. ReflectionUtility.GetCustomAttributesCore(memberInfo, type)];
#else
        memberInfo.GetCustomAttributes(type, inherit: true);
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(Assembly assembly, Type type) =>
#if NETFRAMEWORK
        ReflectionUtility.GetCustomAttributes(assembly, type).ToArray();
#else
        assembly.GetCustomAttributes(type, inherit: true);
#endif

#pragma warning disable IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning disable IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'
    public ConstructorInfo[] GetDeclaredConstructors(Type classType)
        => classType.GetConstructors(DeclaredOnlyLookup);

    public MethodInfo[] GetDeclaredMethods(Type classType)
        => classType.GetMethods(DeclaredOnlyLookup);

    public PropertyInfo[] GetDeclaredProperties(Type type)
        => type.GetProperties(DeclaredOnlyLookup);

    public Type[] GetDefinedTypes(Assembly assembly)
        => assembly.GetTypes();

    public MethodInfo[] GetRuntimeMethods(Type type)
        => type.GetMethods(Everything);

    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters, bool includeNonPublic)
        => includeNonPublic
            ? declaringType.GetMethod(methodName, Everything, null, parameters, null)
            : declaringType.GetMethod(methodName, parameters);

    public PropertyInfo? GetRuntimeProperty(Type classType, string testContextPropertyName, bool includeNonPublic)
        => includeNonPublic
            ? classType.GetProperty(testContextPropertyName, Everything)
            : classType.GetProperty(testContextPropertyName);

    public Type? GetType(string typeName)
        => Type.GetType(typeName);

    public Type? GetType(Assembly assembly, string typeName)
        => assembly.GetType(typeName);

    public object? CreateInstance(Type type, object?[] parameters)
        => Activator.CreateInstance(type, parameters);
#pragma warning restore IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning restore IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning restore IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning restore IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'
}
