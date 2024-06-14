// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#pragma warning disable RS0016 // Add public types and members to the declared API
public class SourceGeneratedReflectionDataProvider
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public Assembly Assembly { get; init; }

    public string AssemblyFileName { get; internal set; }

    public string AssemblyName { get; internal set; }

    public Type[] Types { get; internal set; }

    public Dictionary<string, Type> TypesByName { get; internal set; }

    public Dictionary<Type, Attribute[]> TypeAttributes { get; internal set; }

    public object[] AssemblyAttributes { get; internal set; }

    // All properties that are TestContext
    // All properties that are mentioned in DynamicData
    public Dictionary<Type, PropertyInfo[]> TypeProperties { get; internal set; }

    // All TestMethods, Initialize and Cleanup methods.
    public Dictionary<Type, MethodInfo[]> TypeMethods { get; internal set; }

    public Dictionary<Type, Dictionary<string, Attribute[]>> TypeMethodAttributes { get; internal set; }

    public Dictionary<Type, ConstructorInfo[]> TypeConstructors { get; internal set; }

    public Dictionary<Type, Dictionary<string, PropertyInfo>> TypePropertiesByName { get; internal set; }

    public Dictionary<Type, MyConstructorInfo[]> TypeConstructorsInvoker { get; internal set; }

    internal Assembly GetAssembly(string assemblyPath) => Path.GetFileName(assemblyPath) != AssemblyFileName
            ? throw new ArgumentException($"Assembly '{assemblyPath}' is not allowed. " +
                $"Only '{AssemblyFileName}' is allowed to run in source gen mode.")
            : Assembly;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
#pragma warning restore RS0016 // Add public types and members to the declared API
