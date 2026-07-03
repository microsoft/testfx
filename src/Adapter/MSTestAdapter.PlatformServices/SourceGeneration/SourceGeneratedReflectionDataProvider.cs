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
    public IReadOnlyDictionary<string, Type> TypesByName { get; init; } = new Dictionary<string, Type>();

    /// <summary>
    /// Gets attributes declared on each type. The array contains attribute instances
    /// already inflated by the source generator so no reflection call is required to read them.
    /// </summary>
    public IReadOnlyDictionary<Type, Attribute[]> TypeAttributes { get; init; } = new Dictionary<Type, Attribute[]>();

    /// <summary>
    /// Gets attribute instances declared at the assembly level.
    /// </summary>
    public object[] AssemblyAttributes { get; init; } = [];

    /// <summary>
    /// Gets the properties declared on each type that MSTest may inspect (for example
    /// <c>TestContext</c> properties or properties referenced by <c>DynamicData</c>).
    /// </summary>
    public IReadOnlyDictionary<Type, PropertyInfo[]> TypeProperties { get; init; } = new Dictionary<Type, PropertyInfo[]>();

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
    public IReadOnlyDictionary<Type, MethodInfo[]> TypeMethods { get; init; } = new Dictionary<Type, MethodInfo[]>();

    /// <summary>
    /// Gets source-location data for each type's methods so navigation in the IDE works
    /// without a PDB round-trip.
    /// </summary>
    public IReadOnlyDictionary<string, TypeLocation> TypeMethodLocations { get; init; } = new Dictionary<string, TypeLocation>();

    /// <summary>
    /// Gets attributes declared on each method, keyed by the <see cref="MethodInfo"/> instance
    /// that the source-generator resolved at startup. Keying by <see cref="MethodInfo"/>
    /// (rather than method name) preserves the ability to distinguish overloaded methods.
    /// </summary>
    public IReadOnlyDictionary<MethodInfo, Attribute[]> TypeMethodAttributes { get; init; } = new Dictionary<MethodInfo, Attribute[]>();

    /// <summary>
    /// Gets constructors declared on each type. These are returned by
    /// <c>GetDeclaredConstructors</c>.
    /// </summary>
    public IReadOnlyDictionary<Type, ConstructorInfo[]> TypeConstructors { get; init; } = new Dictionary<Type, ConstructorInfo[]>();

    /// <summary>
    /// Gets a lookup of properties on a type by property name.
    /// </summary>
    public IReadOnlyDictionary<Type, Dictionary<string, PropertyInfo>> TypePropertiesByName { get; init; } = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

    /// <summary>
    /// Gets the constructor invokers that allow instantiating types without reflection.
    /// </summary>
    public IReadOnlyDictionary<Type, ConstructorInvoker[]> TypeConstructorsInvoker { get; init; } = new Dictionary<Type, ConstructorInvoker[]>();

    /// <summary>
    /// Gets the delegate-based invokers for test methods and fixtures, keyed by the
    /// <see cref="MethodInfo"/> the adapter holds. Each delegate calls the method directly
    /// (no <c>MethodInfo.Invoke</c>) and returns a non-null <see cref="Task"/> representing the
    /// method's completion. The source generator normalizes every shape to a <see cref="Task"/> at
    /// generation time — <c>void</c> / synchronous methods return <see cref="Task.CompletedTask"/>,
    /// a <see cref="System.Threading.Tasks.ValueTask"/> is converted with <c>AsTask()</c>, and any
    /// return value is discarded — so callers can simply await the result.
    /// </summary>
    public IReadOnlyDictionary<MethodInfo, Func<object?, object?[]?, object?>> TypeMethodInvokers { get; init; } = new Dictionary<MethodInfo, Func<object?, object?[]?, object?>>();

    /// <summary>
    /// Gets the delegate-based property setters, keyed by the <see cref="PropertyInfo"/> the
    /// adapter holds (today: the <c>TestContext</c> property). Each delegate assigns the value
    /// directly instead of calling <see cref="PropertyInfo.SetValue(object, object)"/>.
    /// </summary>
    public IReadOnlyDictionary<PropertyInfo, Action<object?, object?>> TypePropertySetters { get; init; } = new Dictionary<PropertyInfo, Action<object?, object?>>();

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
    /// Looks up a type by <see cref="Type.FullName"/> scoped to a specific <paramref name="assembly"/>.
    /// The composite override routes the call to the per-assembly provider so that two assemblies
    /// containing a type with the same full name do not shadow each other in a merged dictionary.
    /// </summary>
    /// <remarks>
    /// Returning <see langword="false"/> instructs the caller to fall back to reflection. This is
    /// the right behavior when <paramref name="assembly"/> did not opt into source generation, when
    /// the type was skipped by the generator (open generic, file-local, inaccessible), or when the
    /// composite has no entry for the assembly yet.
    /// </remarks>
    internal virtual bool TryGetTypeByName(Assembly assembly, string typeName, [NotNullWhen(true)] out Type? type)
    {
        if (assembly.Equals(Assembly) && TypesByName.TryGetValue(typeName, out type))
        {
            return true;
        }

        type = null;
        return false;
    }

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

        public Func<object?[]?, object> Invoker { get; init; } = null!;
    }
}
