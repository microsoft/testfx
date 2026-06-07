// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Registers a user-supplied <see cref="ITestFilter"/> implementation that the MSTest adapter
/// invokes for every test it is about to run, after any command-line filter (<c>--filter</c>,
/// Test Explorer selection, etc.) has been applied.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute at the assembly level on either the test assembly itself or on a referenced
/// infrastructure library. Every test assembly that ends up loading the marked assembly at runtime
/// will pick up the filter without the test project needing to declare anything itself.
/// </para>
/// <para>
/// The filter type must be a non-generic class with a public parameterless constructor that
/// implements <see cref="ITestFilter"/>. A single instance is created per test assembly per test
/// run and reused for every test of that assembly.
/// </para>
/// <para>
/// The attribute can be applied multiple times on the same assembly to register more than one
/// filter type. When multiple filters are registered, a test runs only if every filter returns
/// <see cref="TestFilterResult.Run"/>. The first non-<c>Run</c> result wins; the remaining filters
/// are not invoked for that test.
/// </para>
/// <para>
/// The filter runs <em>before</em> the test type is loaded, before <c>[AssemblyInitialize]</c>,
/// before <c>[ClassInitialize]</c>, and before the test constructor is invoked, so dropping or
/// skipping a test through <see cref="ITestFilter"/> avoids paying any of those costs.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In Contoso.TestInfra.dll
/// [assembly: TestFilterProvider(typeof(NightlyFilter))]
///
/// public sealed class NightlyFilter : ITestFilter
/// {
///     public TestFilterResult Filter(TestFilterContext context)
///         =&gt; context.Categories.Contains("Nightly")
///             &amp;&amp; Environment.GetEnvironmentVariable("RUN_NIGHTLY") != "1"
///             ? TestFilterResult.Skip("Set RUN_NIGHTLY=1 to run nightly tests.")
///             : TestFilterResult.Run;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class TestFilterProviderAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestFilterProviderAttribute"/> class.
    /// </summary>
    /// <param name="filterType">
    /// The <see cref="ITestFilter"/> implementation to instantiate and invoke for every test in
    /// the consuming test assembly. Must be a non-generic class with a public parameterless
    /// constructor.
    /// </param>
    public TestFilterProviderAttribute(Type filterType)
        => FilterType = filterType ?? throw new ArgumentNullException(nameof(filterType));

    /// <summary>
    /// Gets the <see cref="ITestFilter"/> implementation registered by this attribute.
    /// </summary>
    public Type FilterType { get; }
}
