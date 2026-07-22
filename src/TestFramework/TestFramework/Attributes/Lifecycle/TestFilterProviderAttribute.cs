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
/// Apply this attribute at the assembly level on the test assembly itself. At most one
/// <see cref="TestFilterProviderAttribute"/> may be applied per test assembly; this is intentional
/// so that filter ordering is not part of the public API. If multiple filtering strategies are
/// needed, compose them explicitly inside a single <see cref="ITestFilter"/> implementation.
/// </para>
/// <para>
/// The filter type must be a non-generic class with a public parameterless constructor that
/// implements <see cref="ITestFilter"/>. A single instance is created per test assembly per test
/// run and reused for every test of that assembly.
/// </para>
/// <para>
/// The filter runs <em>before</em> the test type is loaded, before <c>[AssemblyInitialize]</c>,
/// before <c>[ClassInitialize]</c>, and before the test constructor is invoked, so dropping or
/// skipping a test through <see cref="ITestFilter"/> avoids paying any of those costs.
/// </para>
/// </remarks>
/// <example>
/// <code>
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
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
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
    public TestFilterProviderAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type filterType)
        => FilterType = filterType ?? throw new ArgumentNullException(nameof(filterType));

    /// <summary>
    /// Gets the <see cref="ITestFilter"/> implementation registered by this attribute.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type FilterType { get; }
}
