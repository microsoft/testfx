// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Generic attributes are unusable on .NET Framework: its reflection stack throws
// NotSupportedException("Generic types are not valid.") as soon as a custom attribute with a
// generic type is materialized, and CustomAttributeData reports the shared __Canon instantiation
// instead of the real type argument. Exposing this type only on .NET turns what would be a hard
// runtime failure into a plain compile-time error for anyone targeting .NET Framework, who can
// still use the non-generic TestFilterProviderAttribute.
#if NET

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Strongly typed variant of <see cref="TestFilterProviderAttribute"/> that registers
/// <typeparamref name="TFilter"/> as the <see cref="ITestFilter"/> implementation the MSTest
/// adapter invokes for every test it is about to run.
/// </summary>
/// <typeparam name="TFilter">
/// The <see cref="ITestFilter"/> implementation to instantiate and invoke for every test in the
/// consuming test assembly.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the type-safe form of <c>[assembly: TestFilterProvider(typeof(MyFilter))]</c>. The
/// generic constraints make the compiler enforce, at build time, what the non-generic attribute can
/// only validate at run time: <typeparamref name="TFilter"/> must be a class, must implement
/// <see cref="ITestFilter"/>, and must expose a public parameterless constructor.
/// </para>
/// <para>
/// Everything else behaves exactly like <see cref="TestFilterProviderAttribute"/>, including the
/// "at most one per test assembly" rule — the two attributes count as one and the same
/// registration, so applying both to a single assembly is an error.
/// </para>
/// <para>
/// This attribute is only available when targeting .NET. On .NET Framework, use the non-generic
/// <see cref="TestFilterProviderAttribute"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [assembly: TestFilterProvider&lt;NightlyFilter&gt;]
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
public sealed class TestFilterProviderAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TFilter>
    : TestFilterProviderAttribute
    where TFilter : class, ITestFilter, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestFilterProviderAttribute{TFilter}"/> class.
    /// </summary>
    public TestFilterProviderAttribute()
        : base(typeof(TFilter))
    {
    }
}

#endif
