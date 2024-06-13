// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal class ReflectionOperations2 : ReflectionOperations, IReflectionOperations2
{
    public MethodInfo? GetDeclaredMethod(Type type, string methodName)
        => type.GetMethod(methodName);

    public PropertyInfo? GetDeclaredProperty(Type type, string propertyName)
        => type.GetProperty(propertyName);

    public IReadOnlyList<Type> GetDefinedTypes(Assembly assembly) =>
        assembly.DefinedTypes.ToList();
}
