// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Generic attributes are unusable on .NET Framework: its reflection stack throws
// NotSupportedException("Generic types are not valid.") as soon as a custom attribute with a
// generic type is materialized — and it does so for the whole assembly's attribute enumeration,
// not just this attribute — while CustomAttributeData reports the shared __Canon instantiation
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
/// only validate at run time: <typeparamref name="TFilter"/> must implement <see cref="ITestFilter"/>
/// and expose a public parameterless constructor.
/// </para>
/// <para>
/// Everything else behaves exactly like <see cref="TestFilterProviderAttribute"/>, including the
/// "at most one per test assembly" rule — the two attributes count as one and the same registration,
/// so applying both to a single assembly is an error. They are distinct types, so the compiler does
/// not report that as a duplicate attribute; <c>MSTEST0081</c> does.
/// </para>
/// <para>
/// This attribute is only available when targeting .NET. On .NET Framework, use the non-generic
/// <see cref="TestFilterProviderAttribute"/>. A test project that multi-targets both can select the
/// right form with <c>#if NET</c>:
/// <code>
/// #if NET
/// [assembly: TestFilterProvider&lt;MyFilter&gt;]
/// #else
/// [assembly: TestFilterProvider(typeof(MyFilter))]
/// #endif
/// </code>
/// </para>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
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
    : Attribute, ITestFilterProviderAttribute
    // Deliberately no 'class' constraint: the adapter instantiates the filter through
    // Activator.CreateInstance and only needs an ITestFilter back, so a struct filter is as valid here as
    // it already is through the non-generic attribute. Requiring a class would make a working non-generic
    // registration impossible to migrate to this form.
    where TFilter : ITestFilter, new()
{
    /// <summary>
    /// Gets the <see cref="ITestFilter"/> implementation registered by this attribute.
    /// </summary>
    /// <remarks>
    /// Implemented explicitly so it stays off the public surface: <typeparamref name="TFilter"/> already
    /// tells a consumer which filter is registered, and the property exists only for the adapter to read
    /// the two attribute shapes uniformly. The <see cref="DynamicallyAccessedMembersAttribute"/> annotation
    /// must match the interface declaration, or trimming reports IL2093.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    Type ITestFilterProviderAttribute.FilterType => typeof(TFilter);
}

#endif
