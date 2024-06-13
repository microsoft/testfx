// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

internal interface IReflectionOperations2 : IReflectionOperations
{
    MethodInfo? GetDeclaredMethod(Type dynamicDataDeclaringType, string dynamicDataSourceName);

    PropertyInfo? GetDeclaredProperty(Type type, string propertyName);

    IReadOnlyList<Type> GetDefinedTypes(Assembly assembly);
}
