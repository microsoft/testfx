// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MSTest.PlatformServices.Interface;

namespace MSTest.PlatformServices;

internal sealed class ReflectionOperations2 : ReflectionOperations, IReflectionOperations2
{
    private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const BindingFlags Everything = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

#pragma warning disable IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning disable IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'
    public ConstructorInfo[] GetDeclaredConstructors(Type classType)
        => classType.GetConstructors(DeclaredOnlyLookup);

    public MethodInfo? GetDeclaredMethod(Type type, string methodName)
        => type.GetMethod(methodName, DeclaredOnlyLookup);

    public MethodInfo[] GetDeclaredMethods(Type classType)
        => classType.GetMethods(DeclaredOnlyLookup);

    public PropertyInfo[] GetDeclaredProperties(Type type)
        => type.GetProperties(DeclaredOnlyLookup);

    public PropertyInfo? GetDeclaredProperty(Type type, string propertyName)
        => type.GetProperty(propertyName, DeclaredOnlyLookup);

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
