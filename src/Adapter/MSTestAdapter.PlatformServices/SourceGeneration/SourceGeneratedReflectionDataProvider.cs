// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Holds the pre-computed metadata that the MSTest source generator emits for a test assembly.
/// This data backs the source-generated implementations of the platform reflection and file
/// services so that test discovery and execution do not depend on runtime reflection.
/// </summary>
/// <remarks>
/// This type is intended to be populated only by the MSTest source generator and passed to
/// <see cref="ReflectionMetadataHook.SetMetadata(SourceGeneratedReflectionDataProvider)"/>.
/// Its shape is intentionally simple so that source-generated code can construct it without
/// reflection, but the shape may evolve. Hand-written code should not depend on it.
/// </remarks>
public class SourceGeneratedReflectionDataProvider
{
    /// <summary>
    /// Gets or sets the test assembly described by this metadata snapshot.
    /// </summary>
    public Assembly Assembly { get; set; } = null!;

    /// <summary>
    /// Gets or sets the file-name (without extension) of the test assembly.
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets all defined types in the test assembly that participate in test discovery.
    /// </summary>
    public Type[] Types { get; set; } = [];

    /// <summary>
    /// Gets or sets a lookup of types by full name.
    /// </summary>
    public Dictionary<string, Type> TypesByName { get; set; } = [];

    /// <summary>
    /// Gets or sets attributes declared on each type. The array contains attribute instances
    /// already inflated by the source generator so no reflection call is required to read them.
    /// </summary>
    public Dictionary<Type, Attribute[]> TypeAttributes { get; set; } = [];

    /// <summary>
    /// Gets or sets attribute instances declared at the assembly level.
    /// </summary>
    public object[] AssemblyAttributes { get; set; } = [];

    /// <summary>
    /// Gets or sets the properties declared on each type that MSTest may inspect (for example
    /// <c>TestContext</c> properties or properties referenced by <c>DynamicData</c>).
    /// </summary>
    public Dictionary<Type, PropertyInfo[]> TypeProperties { get; set; } = [];

    /// <summary>
    /// Gets or sets the methods declared on each type that MSTest may inspect (test methods,
    /// initialize / cleanup hooks, dynamic-data methods).
    /// </summary>
    public Dictionary<Type, MethodInfo[]> TypeMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets source-location data for each type's methods so navigation in the IDE works
    /// without a PDB round-trip.
    /// </summary>
    public Dictionary<string, TypeLocation> TypeMethodLocations { get; set; } = [];

    /// <summary>
    /// Gets or sets attributes declared on each method, keyed by the method name.
    /// </summary>
    public Dictionary<Type, Dictionary<string, Attribute[]>> TypeMethodAttributes { get; set; } = [];

    /// <summary>
    /// Gets or sets constructors declared on each type. These are returned by
    /// <c>GetDeclaredConstructors</c>.
    /// </summary>
    public Dictionary<Type, ConstructorInfo[]> TypeConstructors { get; set; } = [];

    /// <summary>
    /// Gets or sets a lookup of properties on a type by property name.
    /// </summary>
    public Dictionary<Type, Dictionary<string, PropertyInfo>> TypePropertiesByName { get; set; } = [];

    /// <summary>
    /// Gets or sets the constructor invokers that allow instantiating types without reflection.
    /// </summary>
    public Dictionary<Type, ConstructorInvoker[]> TypeConstructorsInvoker { get; set; } = [];

    internal virtual Assembly GetAssembly(string assemblyPath)
        => !string.Equals(Path.GetFileNameWithoutExtension(assemblyPath), AssemblyName, StringComparison.OrdinalIgnoreCase)
            ? throw new ArgumentException($"Assembly '{assemblyPath}' is not allowed. Only '{AssemblyName}' is allowed to run in source-generator mode.")
            : Assembly;

    internal virtual void GetNavigationData(string className, string methodName, out int minLineNumber, out string? fileName)
    {
        if (!TypeMethodLocations.TryGetValue(className, out TypeLocation? typeLocation) || typeLocation is null)
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
    /// Returns the assembly-level attributes that belong to <paramref name="assembly"/>. Returns
    /// the single-provider <see cref="AssemblyAttributes"/> when the assembly matches this provider's
    /// <see cref="Assembly"/>; otherwise an empty array. The composite override fans out the lookup
    /// to the owning provider.
    /// </summary>
    internal virtual object[] GetAssemblyAttributes(Assembly assembly)
        => assembly.Equals(Assembly) ? AssemblyAttributes : [];

    /// <summary>
    /// Per-type source-location information used by IDE / explorer navigation.
    /// </summary>
    public sealed class TypeLocation
    {
        /// <summary>
        /// Gets or sets the source file containing the type.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the line number for each method, keyed by method name.
        /// </summary>
        public Dictionary<string, int> MethodLocations { get; set; } = [];
    }

    /// <summary>
    /// Describes one constructor and a delegate that invokes it. Source-generated code emits
    /// these to allow <c>Activator.CreateInstance</c>-free instantiation.
    /// </summary>
    public sealed class ConstructorInvoker
    {
        /// <summary>
        /// Gets or sets the parameter types, in declaration order.
        /// </summary>
        public Type[] Parameters { get; set; } = [];

        /// <summary>
        /// Gets or sets the delegate that invokes the constructor.
        /// </summary>
        public Func<object?[], object> Invoker { get; set; } = null!;
    }
}
