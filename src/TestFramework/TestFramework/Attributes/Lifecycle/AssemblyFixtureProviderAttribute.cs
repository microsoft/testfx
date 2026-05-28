// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Indicates that the methods on the specified <see cref="FixtureType"/> annotated with
/// <see cref="AssemblyInitializeAttribute"/> or <see cref="AssemblyCleanupAttribute"/> should be
/// discovered and executed once per consuming test assembly, even when the methods are not declared
/// in that test assembly itself.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute at the assembly level on the library that exposes the shared fixture. Every
/// test assembly that ends up loading the library at runtime will then pick up its
/// <see cref="AssemblyInitializeAttribute"/> / <see cref="AssemblyCleanupAttribute"/> methods
/// without the test project needing to declare anything itself.
/// </para>
/// <para>
/// If the consuming test assembly already declares its own <see cref="AssemblyInitializeAttribute"/>
/// or <see cref="AssemblyCleanupAttribute"/> method, the local declaration always wins; methods
/// contributed via <see cref="AssemblyFixtureProviderAttribute"/> only fill empty slots.
/// </para>
/// <para>
/// The attribute can be applied multiple times on the same assembly to expose more than one fixture
/// type. The attribute can also be applied on the consuming test assembly itself to opt into
/// fixtures defined in a third-party library that cannot be modified.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In Contoso.TestInfra.dll
/// [assembly: AssemblyFixtureProvider(typeof(GlobalFixtures))]
///
/// public static class GlobalFixtures
/// {
///     [AssemblyInitialize]
///     public static void Init(TestContext context) { /* ... */ }
///
///     [AssemblyCleanup]
///     public static void Cleanup() { /* ... */ }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class AssemblyFixtureProviderAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyFixtureProviderAttribute"/> class.
    /// </summary>
    /// <param name="fixtureType">
    /// The type whose <see cref="AssemblyInitializeAttribute"/> and
    /// <see cref="AssemblyCleanupAttribute"/> methods should be exposed to assemblies referencing
    /// the assembly carrying this attribute.
    /// </param>
    public AssemblyFixtureProviderAttribute(Type fixtureType)
        => FixtureType = fixtureType;

    /// <summary>
    /// Gets the type whose <see cref="AssemblyInitializeAttribute"/> and
    /// <see cref="AssemblyCleanupAttribute"/> methods are exposed.
    /// </summary>
    public Type FixtureType { get; }
}
