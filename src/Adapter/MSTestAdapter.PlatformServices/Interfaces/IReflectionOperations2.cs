// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

internal interface IReflectionOperations2 : IReflectionOperations
{
    ConstructorInfo[] GetDeclaredConstructors(Type classType);

    MethodInfo? GetDeclaredMethod(Type dynamicDataDeclaringType, string dynamicDataSourceName);

    MethodInfo[] GetDeclaredMethods(Type classType);

    PropertyInfo[] GetDeclaredProperties(Type type);

    PropertyInfo? GetDeclaredProperty(Type type, string propertyName);

    Type[] GetDefinedTypes(Assembly assembly);

    MethodInfo[] GetRuntimeMethods(Type type);

    MethodInfo? GetRuntimeMethod(Type declaringType, string methodName, Type[] parameters);

    PropertyInfo? GetRuntimeProperty(Type classType, string propertyName);

    Type? GetType(string typeName);

    Type? GetType(Assembly assembly, string typeName);

    object? CreateInstance(Type type, object?[] parameters);
}
