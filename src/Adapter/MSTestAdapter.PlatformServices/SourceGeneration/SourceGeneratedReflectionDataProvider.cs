// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Holds the pre-computed metadata that the MSTest source generator emits for a test assembly.
/// This data backs the source-generated implementations of the platform reflection and file
/// services so that test discovery and execution do not depend on runtime reflection.
/// </summary>
/// <remarks>
/// This type is intentionally internal: only the MSTest source generator constructs it (via
/// the public <see cref="ReflectionMetadataHook"/> builder API). The shape may evolve from
/// one MSTest version to the next without notice.
/// </remarks>
internal class SourceGeneratedReflectionDataProvider
{
    /// <summary>
    /// Gets the test assembly described by this metadata snapshot.
    /// </summary>
    public Assembly Assembly { get; init; } = null!;

    /// <summary>
    /// Gets the file-name (without extension) of the test assembly.
    /// </summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets all defined types in the test assembly that participate in test discovery.
    /// </summary>
    public Type[] Types { get; init; } = [];

    /// <summary>
    /// Gets a lookup of types by full name.
    /// </summary>
    public Dictionary<string, Type> TypesByName { get; init; } = [];

    /// <summary>
    /// Gets attributes declared on each type. The array contains attribute instances
    /// already inflated by the source generator so no reflection call is required to read them.
    /// </summary>
    public Dictionary<Type, Attribute[]> TypeAttributes { get; init; } = [];

    /// <summary>
    /// Gets attribute instances declared at the assembly level.
    /// </summary>
    public object[] AssemblyAttributes { get; init; } = [];

    /// <summary>
    /// Gets the properties declared on each type that MSTest may inspect (for example
    /// <c>TestContext</c> properties or properties referenced by <c>DynamicData</c>).
    /// </summary>
    public Dictionary<Type, PropertyInfo[]> TypeProperties { get; init; } = [];

    /// <summary>
    /// Gets the methods declared on each type that the source generator was able to surface
    /// (today: <c>[TestMethod]</c>-annotated methods only, including inherited ones).
    /// </summary>
    /// <remarks>
    /// This dictionary is intentionally partial — it never carries non-test methods, generic
    /// methods, or methods with by-ref parameters. Consumers must NOT use it as an authoritative
    /// source for <c>BindingFlags.DeclaredOnly</c>-style enumerations; the reflection-backed
    /// fallback is responsible for completeness.
    /// </remarks>
    public Dictionary<Type, MethodInfo[]> TypeMethods { get; init; } = [];

    /// <summary>
    /// Gets source-location data for each type's methods so navigation in the IDE works
    /// without a PDB round-trip.
    /// </summary>
    public Dictionary<string, TypeLocation> TypeMethodLocations { get; init; } = [];

    /// <summary>
    /// Gets attributes declared on each method, keyed by the <see cref="MethodInfo"/> instance
    /// that the source-generator resolved at startup. Keying by <see cref="MethodInfo"/>
    /// (rather than method name) preserves the ability to distinguish overloaded methods.
    /// </summary>
    public Dictionary<MethodInfo, Attribute[]> TypeMethodAttributes { get; init; } = [];

    /// <summary>
    /// Gets constructors declared on each type. These are returned by
    /// <c>GetDeclaredConstructors</c>.
    /// </summary>
    public Dictionary<Type, ConstructorInfo[]> TypeConstructors { get; init; } = [];

    /// <summary>
    /// Gets a lookup of properties on a type by property name.
    /// </summary>
    public Dictionary<Type, Dictionary<string, PropertyInfo>> TypePropertiesByName { get; init; } = [];

    /// <summary>
    /// Gets the constructor invokers that allow instantiating types without reflection.
    /// </summary>
    public Dictionary<Type, ConstructorInvoker[]> TypeConstructorsInvoker { get; init; } = [];

    /// <summary>
    /// Returns the snapshot of merged metadata that callers should read. Single-assembly
    /// providers return themselves; the composite returns its currently-published snapshot
    /// (so readers always see a consistent point-in-time view even when another assembly is
    /// being registered concurrently).
    /// </summary>
    internal virtual SourceGeneratedReflectionDataProvider GetSnapshot() => this;

    internal virtual bool TryGetAssembly(string assemblyPath, [NotNullWhen(true)] out Assembly? assembly)
    {
        if (Assembly is not null
            && string.Equals(Path.GetFileNameWithoutExtension(assemblyPath), AssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            assembly = Assembly;
            return true;
        }

        assembly = null;
        return false;
    }

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
    internal sealed class TypeLocation
    {
        public string FileName { get; init; } = string.Empty;

        public Dictionary<string, int> MethodLocations { get; init; } = [];
    }

    /// <summary>
    /// Describes one constructor and a delegate that invokes it. Source-generated code emits
    /// these to allow <c>Activator.CreateInstance</c>-free instantiation.
    /// </summary>
    internal sealed class ConstructorInvoker
    {
        public Type[] Parameters { get; init; } = [];

        public Func<object?[], object> Invoker { get; init; } = null!;
    }
}
