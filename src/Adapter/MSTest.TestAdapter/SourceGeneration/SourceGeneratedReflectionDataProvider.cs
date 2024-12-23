// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.SourceGeneration;

/// <summary>
/// This type is used by MSTest SourceGenerator, its shape can change at any time. Do NOT depend on the shape of this API.
/// </summary>
public sealed class SourceGeneratedReflectionDataProvider
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

    public Dictionary<string, TypeLocation> TypeMethodLocations { get; init; }

    public Dictionary<Type, Dictionary<string, Attribute[]>> TypeMethodAttributes { get; init; }

    public Dictionary<Type, ConstructorInfo[]> TypeConstructors { get; init; }

    public Dictionary<Type, Dictionary<string, PropertyInfo>> TypePropertiesByName { get; init; }

    public Dictionary<Type, MyConstructorInfo[]> TypeConstructorsInvoker { get; init; }

    internal Assembly GetAssembly(string assemblyPath) => Path.GetFileNameWithoutExtension(assemblyPath) != AssemblyName
            ? throw new ArgumentException($"Assembly '{assemblyPath}' is not allowed. " +
                $"Only '{AssemblyName}' is allowed to run in source gen mode.")
            : Assembly;

    internal void GetNavigationData(string className, string methodName, out int minLineNumber, out string? fileName)
    {
        bool found = TypeMethodLocations.TryGetValue(className, out TypeLocation? typeLocation);

        if (!found || typeLocation == null)
        {
            minLineNumber = 0;
            fileName = null;
            return;
        }

        if (!typeLocation.MethodLocations.TryGetValue(methodName, out int lineNumber))
        {
            minLineNumber = 0;
            fileName = null;
            return;
        }

        fileName = typeLocation.FileName;
        minLineNumber = lineNumber;
    }

    /// <summary>
    /// This type is used by MSTest SourceGenerator, its shape can change at any time. Do NOT depend on the shape of this API.
    /// </summary>
    public class TypeLocation
    {
        public string FileName { get; init; }

        public Dictionary<string, int> MethodLocations { get; init; }
    }

    /// <summary>
    /// This type is used by MSTest SourceGenerator, its shape can change at any time. Do NOT depend on the shape of this API.
    /// </summary>
    public class MyConstructorInfo
    {
        // TODO, parameters can be `params` or optional, add special type to represent that, or structure to describe it
        public Type[] Parameters { get; internal set; }

        public Func<object?[], object> Invoker { get; internal set; }
    }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
#endif
