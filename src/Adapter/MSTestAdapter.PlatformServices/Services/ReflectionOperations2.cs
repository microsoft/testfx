// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
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

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2070",
        Justification = "<Pending>")]
    public IEnumerable<ConstructorInfo> GetDeclaredConstructors(Type classType)
        => classType.GetTypeInfo().DeclaredConstructors;

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2070",
        Justification = "<Pending>")]
    public MethodInfo? GetDeclaredMethod(Type type, string methodName)
        => type.GetTypeInfo().GetDeclaredMethod(methodName);

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2070",
        Justification = "<Pending>")]
    public IEnumerable<MethodInfo> GetDeclaredMethods(Type classType)
        => classType.GetTypeInfo().DeclaredMethods;

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2070",
        Justification = "<Pending>")]
    public IEnumerable<PropertyInfo> GetDeclaredProperties(Type type)
        => type.GetTypeInfo().DeclaredProperties;

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2070",
        Justification = "<Pending>")]
    public PropertyInfo? GetDeclaredProperty(Type type, string propertyName)
        => type.GetTypeInfo().GetDeclaredProperty(propertyName);

    [UnconditionalSuppressMessage("Aot", "IL2026:DoNotUseGetDefinedTypes", Justification = "We access all the types we need in metadata, so this is preserved and works.")]

    public Type[] GetDefinedTypes(Assembly assembly)
        => assembly.DefinedTypes.ToArray();

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "<Pending>")]
    public IEnumerable<MethodInfo> GetRuntimeMethods(Type type)
        => type.GetRuntimeMethods();

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2067",
        Justification = "<Pending>")]
    public MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters)
        => declaringType.GetRuntimeMethod(methodName, parameters);

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2070",
        Justification = "<Pending>")]
    public PropertyInfo? GetRuntimeProperty(Type classType, string testContextPropertyName)
        => classType.GetProperty(testContextPropertyName);

    [UnconditionalSuppressMessage("Aot", "IL2026:DoNotUseGetDefinedTypes", Justification = "We access all the types we need in metadata, so this is preserved and works.")]
    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2057",
        Justification = "<Pending>")]
    public Type? GetType(string typeName)
        => Type.GetType(typeName);

    [UnconditionalSuppressMessage("Aot", "IL2026:DoNotUseGetDefinedTypes", Justification = "We access all the types we need in metadata, so this is preserved and works.")]
    public Type? GetType(Assembly assembly, string typeName)
        => assembly.GetType(typeName);

    [UnconditionalSuppressMessage(
    "ReflectionAnalysis",
    "IL2067",
    Justification = "<Pending>")]
    public object? CreateInstance(Type type, object?[] parameters)
        => Activator.CreateInstance(type, parameters);
}
