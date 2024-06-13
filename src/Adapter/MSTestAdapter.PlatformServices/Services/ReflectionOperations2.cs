// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal class ReflectionOperations2 : ReflectionOperations, IReflectionOperations2
{
    public ReflectionOperations2()
    {
#if NET8_0_OR_GREATER
        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException("ReflectionOperations2 are not allowed when dynamic code is not supported, use NativeReflectionOperations instead");
        }
#endif
    }

#pragma warning disable IL2070 // this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
#pragma warning disable IL2067 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
#pragma warning disable IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'
    public IEnumerable<ConstructorInfo> GetDeclaredConstructors(Type classType)
        => classType.GetTypeInfo().DeclaredConstructors;

    public MethodInfo? GetDeclaredMethod(Type type, string methodName)
        => type.GetMethod(methodName);

    public IEnumerable<MethodInfo> GetDeclaredMethods(Type classType)
        => classType.GetTypeInfo().DeclaredMethods;

    public IEnumerable<PropertyInfo> GetDeclaredProperties(Type type)
        => type.GetTypeInfo().DeclaredProperties;

    public PropertyInfo? GetDeclaredProperty(Type type, string propertyName)
        => type.GetProperty(propertyName);

    public IReadOnlyList<Type> GetDefinedTypes(Assembly assembly)
        => assembly.DefinedTypes.ToList();

    public IEnumerable<MethodInfo> GetRuntimeMethods(Type type)
        => type.GetRuntimeMethods();

    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters)
        => declaringType.GetRuntimeMethod(methodName, parameters);

    public PropertyInfo? GetRuntimeProperty(Type classType, string testContextPropertyName)
        => classType.GetProperty(testContextPropertyName);

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
