﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Native;

#pragma warning disable RS0016 // Add public types and members to the declared API
public class SourceGeneratedReflectionDataProvider
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public Assembly Assembly { get; init; }

    public string AssemblyName { get; init; }

    public Type[] Types { get; init; }

    public Dictionary<string, Type> TypesByName { get; init; }

    public Dictionary<Type, Attribute[]> TypeAttributes { get; init; }

    public object[] AssemblyAttributes { get; init; }

    // All properties that are TestContext
    // All properties that are mentioned in DynamicData
    public Dictionary<Type, PropertyInfo[]> TypeProperties { get; init; }

    // All TestMethods, Initialize and Cleanup methods.
    public Dictionary<Type, MethodInfo[]> TypeMethods { get; init; }

    public Dictionary<Type, Dictionary<string, Attribute[]>> TypeMethodAttributes { get; init; }

    public Dictionary<Type, ConstructorInfo[]> TypeConstructors { get; init; }

    public Dictionary<Type, Dictionary<string, PropertyInfo>> TypePropertiesByName { get; init; }

    public Dictionary<Type, MyConstructorInfo[]> TypeConstructorsInvoker { get; init; }

    internal Assembly GetAssembly(string assemblyPath) => Path.GetFileNameWithoutExtension(assemblyPath) != AssemblyName
            ? throw new ArgumentException($"Assembly '{assemblyPath}' is not allowed. " +
                $"Only '{AssemblyName}' is allowed to run in source gen mode.")
            : Assembly;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
#pragma warning restore RS0016 // Add public types and members to the declared API